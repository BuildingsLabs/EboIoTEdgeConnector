using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using Mongoose.Process.Ews;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Value Push Processor", "This processor gets runtime values from EBO, and pushes them to Azure as defined by the signal CSV file.")]
    public class ValuePushProcessor : EboIotEdgeConnectorProcessorWithMqttBase, ILongRunningProcess
    {
        private const int MaxItemsPerSubscription = 500;

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            try
            {
                StartMqttClient().Wait();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.Processor, this.Name, $"Starting MQTT Client failed");
                Prompts.Add(ex.ToPrompt());
                return Prompts;
            }

            if (Signals == null)
            {
                Prompts.Add(new Prompt
                {
                    Message = "There are no signals in the cache, please run the SetupProcessor or verify that it has run successfully.",
                    Severity = PromptSeverity.MayNotContinue
                });
                return Prompts;
            }

            // Read existing subscriptions
            if (!ReadExistingSubscriptions(Signals).Result)
            {
                Prompts.Add(new Prompt {Message = $"Did not successfully read all existing subscriptions."});
            }

            // Subscribe and read new subscriptions
            if (!SubscribeAndReadNew(Signals).Result)
            {
                Prompts.Add(new Prompt {Message = $"Did not successfully read all new subscriptions."});
            }

            Logger.LogTrace(LogCategory.Processor, this.Name, "Waiting for all messages be be published...");
            while (ManagedMqttClient.PendingApplicationMessagesCount > 0)
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, $"{ManagedMqttClient.PendingApplicationMessagesCount} messages left waiting to be published..");
                if (this.IsCancellationRequested) return new List<Prompt>();
                Task.Delay(1000).Wait();
            }
            ManagedMqttClient.StopAsync().Wait();
            ManagedMqttClient.Dispose();
    
            // Update the cache with new values..
            Signals = Signals;
            return Prompts;
        }
        #endregion

        #region ReadExistingSubscription
        private async Task<bool> ReadExistingSubscriptions(List<Signal> signals)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Reading existing subscriptions..");

            var activeSubscriptions = Cache.RetrieveItem($"ActiveSubscriptions", () => new List<string>(), CacheTenantId, 0) as List<string>;
            var activeSubscriptionsToIterate = activeSubscriptions.ToList();
            foreach (var sub in activeSubscriptionsToIterate)
            {
                if (IsCancellationRequested) return false;
                var subscription = Cache.RetrieveItem($"ActiveSubscriptions#{sub}", CacheTenantId);
                Logger.LogDebug(LogCategory.Processor, $"Reading existing subscription: {sub}");
                try
                {
                    CheckCancellationToken();

                    var si = new SubscriptionReader
                    {
                        Address = EboEwsSettings.Address,
                        UserName = EboEwsSettings.UserName,
                        Password = EboEwsSettings.Password,
                        SubscriptionEventType = EwsSubscriptionEventTypeEnum.ValueItemChanged,
                        SubscriptionId = sub
                    };
                    var results = si.ReadData();
                    // Attempt to update the values by reading the subscription, if this fails return all Prompts
                    if (!await UpdateValues(si, results))
                    {
                        if (!si.IsResubscribeRequired) return false;
                        activeSubscriptions.Remove(sub);
                        Cache.DeleteItem($"ActiveSubscriptions#{sub}", CacheTenantId);
                    }

                    // It's possible that the subscription id has changed if it failed to be renewed/ updated... reset it here
                    if (si.SubsciptionChanged)
                    {
                        Logger.LogDebug(LogCategory.Processor, $"Subscription Id {sub} has changed to {si.SubscriptionId}, updating cache values to represent this");
                        activeSubscriptions.Remove(sub);
                        activeSubscriptions.Add(si.SubscriptionId);
                        Cache.DeleteItem($"ActiveSubscriptions#{sub}", CacheTenantId);
                        Cache.AddOrUpdateItem(subscription, $"ActiveSubscriptions#{si.SubscriptionId}", CacheTenantId, 0);
                    }
                }

                catch (Exception)
                {
                    activeSubscriptions.Remove(sub);
                    Cache.DeleteItem($"ActiveSubscriptions#{sub}", CacheTenantId);
                }
            }

            // Save any changes to cache
            Cache.AddOrUpdateItem(activeSubscriptions, $"ActiveSubscriptions", CacheTenantId, 0);
            return true;
        }
        #endregion
        #region SubscribeAndReadNew
        private async Task<bool> SubscribeAndReadNew(List<Signal> signals)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Creating and reading new subscriptions..");
            var activeSubscriptions = Cache.RetrieveItem($"ActiveSubscriptions", () => new List<string>(), CacheTenantId, 0) as List<string>;

            var subscribedIds = new List<string>();

            foreach (var subscription in activeSubscriptions)
            {
                var itemsToAdd = Cache.RetrieveItem<List<string>>($"ActiveSubscriptions#{subscription}", null, CacheTenantId);
                if (itemsToAdd != null)
                {
                    subscribedIds.AddRange(itemsToAdd);
                }
            }

            var unsubscribedIds = signals.Select(a => a.EwsId).Where(a => !subscribedIds.Contains(a)).ToList();

            Logger.LogDebug(LogCategory.Processor, this.Name, $"Found {unsubscribedIds.Count} points that are not currently subscribed to.");
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Unsubscribed Point Ids: {unsubscribedIds.ToJSON()}");

            while (unsubscribedIds.Any())
            {
                if (IsCancellationRequested) return false;
                try
                {
                    var idsToSubscribeTo = unsubscribedIds.Take(MaxItemsPerSubscription).ToList();
                    CheckCancellationToken();
                    var si = new SubscriptionReader
                    {
                        Address = EboEwsSettings.Address,
                        UserName = EboEwsSettings.UserName,
                        Password = EboEwsSettings.Password,
                        SubscriptionEventType = EwsSubscriptionEventTypeEnum.ValueItemChanged,
                        Ids = idsToSubscribeTo
                    };

                    // Attempt to update the values by reading the subscription, if this fails return all false as this could go on forever.
                    var results = si.ReadData();
                    // If all the ids we subscribed to failed, just continue on.. nothing to see here..
                    if (si.FailedSubscribedItems.Count == idsToSubscribeTo.Count) return true;
                    if (!await UpdateValues(si, results, true)) return false;

                    Cache.AddOrUpdateItem(si.SubscribedItems, $"ActiveSubscriptions#{si.SubscriptionId}", CacheTenantId, 0);
                    unsubscribedIds = unsubscribedIds.Skip(MaxItemsPerSubscription).ToList();

                    activeSubscriptions.Add(si.SubscriptionId);
                    Cache.AddOrUpdateItem(activeSubscriptions, $"ActiveSubscriptions", CacheTenantId, 0);

                    // Add any prompts generated from reader to the list of prompts
                    Prompts.AddRange(si.ReadData().Prompts);

                    if (si.FailedSubscribedItems.Any()) Logger.LogInfo(LogCategory.Processor, this.Name, $"Some items failed to be subscribed to: {si.FailedSubscribedItems.ToJSON()}");
                }

                catch (Exception ex)
                {
                    Prompts.Add(ex.ToPrompt());
                    break;
                }
            }

            return true;

            // TODO: How to handle subscriptions to value items that keep failing?
        }
        #endregion
        #region UpdateValues
        /// <summary>
        /// Sends the Observations to the MQTT broker
        /// </summary>
        /// <param name="sendAdditionalProperties">If true, the 'Writeable' and 'Forceable' properties will be sent in the Observation</param>
        /// <returns></returns>
        private async Task<bool> UpdateValues(SubscriptionReader si, ReadResult<SubscriptionResultItem> results, bool sendAdditionalProperties = false)
        {
            if (!results.Success)
            {
                Prompts.AddRange(results.Prompts);
                return false;
            }

            var devices = results.DataRead.GroupBy(a => a.ValueItemChangeEvent.Id.Remove(a.ValueItemChangeEvent.Id.LastIndexOf('/')).Remove(0,2));

            foreach (var device in devices)
            {
                var observations = new List<Observation>();
                var deviceMessage = new IotEdgeMessage
                {
                    Format = "rec2.3",
                    Observations = observations,
                    DeviceId = device.Key
                };

                AddUpdatedValuesToMessage(observations, device.Key, device.ToList(), si.CachedSubscribedItems, sendAdditionalProperties);

                var messageBuilder = new MqttApplicationMessageBuilder();
                var message = messageBuilder.WithRetainFlag().WithAtLeastOnceQoS().WithTopic(ValuePushTopic).WithPayload(deviceMessage.ToJson()).Build();
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Sending Message to MQTT Broker: {deviceMessage.ToJson()}");
                await ManagedMqttClient.PublishAsync(message);
            }

            return true;
        }
        #endregion
        #region AddUpdatedValuesToMessage
        private void AddUpdatedValuesToMessage(List<Observation> observations, string devicePath, List<SubscriptionResultItem> pointsToAdd, List<string> pointsMonitoredBySub, bool sendAdditionalProperties = false)
        {
            foreach (var eventz in pointsToAdd)
            {
                var signal = Signals.FirstOrDefault(a => a.EwsId == eventz.ValueItemChangeEvent.Id);
                if (signal == null)
                {
                    Logger.LogInfo(LogCategory.Processor, this.Name, $"Signal with EWS ID of {eventz.ValueItemChangeEvent.Id} does not exist.. Skipping this..");
                    continue;
                }

                signal.Value = eventz.ValueItemChangeEvent.Value;
                signal.LastUpdateTime = eventz.ValueItemChangeEvent.TimeStamp.ToUniversalTime();
                // TODO: What to do if State is Error?
                if (signal.SendOnUpdate)
                {
                    HandleAddingToObservationsList(observations, signal, sendAdditionalProperties);
                }
            }

            Signals = Signals;

            foreach (var signal in Signals.Where(a => pointsMonitoredBySub.Contains(a.EwsId) && a.DatabasePath.Remove(a.DatabasePath.LastIndexOf('/')) == devicePath))
            {
                if (signal.LastSendTime != null && signal.LastSendTime.Value.AddSeconds(signal.SendTime) > DateTime.UtcNow) continue;
                if (observations.All(a => $"{devicePath}/{a.SensorId}" != signal.DatabasePath))
                {
                    HandleAddingToObservationsList(observations, signal, sendAdditionalProperties);
                }
            }

            Signals = Signals;
        }
        #endregion

        #region HandleMqttApplicationMessageReceived - Override
        public override void HandleMqttApplicationMessageReceived(string topic, string decodedMessage)
        {
            // In theory, this should not be receiving observations, just log this was unexpected
            Logger.LogInfo(LogCategory.Processor, this.Name, $"{this.Name} unexpectedly received a message..");
        }
        #endregion
        #region SubscribeToMqttTopics - Override
        public override void SubscribeToMqttTopics()
        {
            // Not topics to subscribe to, intentionally blank
        }
        #endregion
    }
}