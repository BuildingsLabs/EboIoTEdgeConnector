using System;
using System.Collections.Generic;
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
        private List<Signal> _tempSignals;

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            if (Signals == null)
            {
                GetSignalNullReason();
                return Prompts;
            }

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

            _tempSignals = Signals.ToList();
            // Read existing subscriptions
            if (!ReadExistingSubscriptions(_tempSignals).Result)
            {
                Prompts.Add(new Prompt { Message = $"Did not successfully read all existing subscriptions." });
            }

            // Subscribe and read new subscriptions
            if (!SubscribeAndReadNew(_tempSignals).Result)
            {
                Prompts.Add(new Prompt { Message = $"Did not successfully read all new subscriptions." });
            }

            Logger.LogTrace(LogCategory.Processor, this.Name, "Waiting for all messages be be published...");
            while (ManagedMqttClient.PendingApplicationMessagesCount > 0)
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, $"{ManagedMqttClient.PendingApplicationMessagesCount} messages left waiting to be published..");
                if (this.IsCancellationRequested) return new List<Prompt>();
                Task.Delay(1000).Wait();
            }

            Logger.LogTrace(LogCategory.Processor, this.Name, "Stopping Managed MQTT Client..");
            ManagedMqttClient.StopAsync().Wait();
            while (ManagedMqttClient.IsStarted)
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, "Still waiting for MQTT Client to Stop...");
                Task.Delay(1000).Wait();
            }
            ManagedMqttClient.Dispose();

            // Update the cache with new values..
            Signals = _tempSignals;
            return Prompts;
        }
        #endregion

        #region GetSignalNullReason
        /// <summary>
        /// Tries to determine why the Signal cache is empty, and attempts to resolve it if possible
        /// </summary>
        private void GetSignalNullReason()
        {
            try
            {
                var setupProcessorId = Cache.RetrieveItem("SetupProcessorConfigurationId", tenantId: CacheTenantId);
                if (setupProcessorId == null)
                {
                    Prompts.Add(new Prompt
                    {
                        Message = "The Setup Processor has not been run. Make sure the Setup Processor has been configured correctly and run it again.",
                        Severity = PromptSeverity.MayNotContinue
                    });
                }
                else
                {
                    if (ActionBroker.IsConfigurationRunning((int) setupProcessorId))
                    {
                        Prompts.Add(new Prompt
                        {
                            Message = "The Setup Processor is currently running, once it has completed this will run successfully.",
                            Severity = PromptSeverity.MayNotContinue
                        });
                    }
                    else
                    {
                        // For some reason the Setup Processor failed to run.. let's force it to run again, and hope it completes!
                        Logger.LogInfo(LogCategory.Processor, this.Name, "Force starting the Setup Processor, as it has failed to run for some reason, please check the logs for additional information.");
                        ActionBroker.StartConfiguration((int) setupProcessorId, DerivedFromConfigurationType.Processor);
                        Prompts.Add(new Prompt
                        {
                            Message = "The Setup Processor processor has been forced to start, the Value Push Processor cannot run to completion until it has run successfully.",
                            Severity = PromptSeverity.MayNotContinue
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Prompts.Add(new Prompt
                {
                    Message = ex.ToString(),
                    Severity = PromptSeverity.MayNotContinue
                });
            }
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
                Logger.LogDebug(LogCategory.Processor, $"Reading existing subscription: {sub}");
                if (IsCancellationRequested) return false;
                var subscription = Cache.RetrieveItem($"ActiveSubscriptions#{sub}", CacheTenantId);
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
                finally
                {
                    Logger.LogDebug(LogCategory.Processor, $"Finished handling {sub}");
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
                    // We want to continue with the list even if we fail here
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

            var signalChanges = results.DataRead.GroupBy(a => a.ValueItemChangeEvent.Id.Remove(a.ValueItemChangeEvent.Id.LastIndexOf('/')).Remove(0,2)).ToList();
            var devices = _tempSignals.GroupBy(a => a.DatabasePath.Remove(a.DatabasePath.LastIndexOf('/')));

            foreach (var device in devices)
            {
                var observations = new List<Observation>();
                var deviceMessage = new IotEdgeMessage
                {
                    Format = "rec2.3",
                    Observations = observations,
                    DeviceId = device.Key
                };
                var signalChangesForDevice = signalChanges.FirstOrDefault(a => a.Key == device.Key);
                AddUpdatedValuesToMessage(observations, device.Key, signalChangesForDevice == null || !signalChangesForDevice.ToList().Any() ? new List<SubscriptionResultItem>() : signalChangesForDevice.ToList(), si.CachedSubscribedItems, sendAdditionalProperties);

                if (deviceMessage.Observations != null && deviceMessage.Observations.Count > 0)
                {
                    var messageBuilder = new MqttApplicationMessageBuilder();
                    var managedMessageBuilder = new ManagedMqttApplicationMessageBuilder();
                    var message = messageBuilder.WithRetainFlag().WithAtLeastOnceQoS().WithTopic(ValuePushTopic).WithPayload(deviceMessage.ToJson()).Build();
                    Logger.LogTrace(LogCategory.Processor, this.Name, $"Sending Message to MQTT Broker: {deviceMessage.ToJson()}");
                    await ManagedMqttClient.PublishAsync(managedMessageBuilder.WithApplicationMessage(message).Build());
                }
            }

            return true;
        }
        #endregion
        #region AddUpdatedValuesToMessage
        private void AddUpdatedValuesToMessage(List<Observation> observations, string devicePath, List<SubscriptionResultItem> pointsToAdd, List<string> pointsMonitoredBySub, bool sendAdditionalProperties = false)
        {
            foreach (var eventz in pointsToAdd)
            {
                var signal = _tempSignals.FirstOrDefault(a => a.EwsId == eventz.ValueItemChangeEvent.Id);
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
                    Logger.LogTrace(LogCategory.Processor, this.Name, $"Adding {signal.ToJSON()} to the message for device: {devicePath}.");
                    HandleAddingToObservationsList(observations, signal, sendAdditionalProperties);
                }
            }

            //Signals = Signals;

            foreach (var signal in _tempSignals.Where(a => pointsMonitoredBySub.Contains(a.EwsId) && a.DatabasePath.Remove(a.DatabasePath.LastIndexOf('/')) == devicePath))
            {
                if (signal.LastSendTime != null && signal.LastSendTime.Value.AddSeconds(signal.SendTime) > DateTime.UtcNow) continue;
                if (observations.All(a => $"{devicePath}/{a.SensorId}" != signal.DatabasePath))
                {
                    Logger.LogTrace(LogCategory.Processor, this.Name, $"Adding {signal.ToJSON()} to the message device: {devicePath}..");
                    HandleAddingToObservationsList(observations, signal, sendAdditionalProperties);
                }
            }

            //Signals = Signals;
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