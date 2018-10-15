using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using Mongoose.Process.Ews;
using MQTTnet.Extensions.ManagedClient;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Value Push Processor", "This processor gets runtime values from EBO, and pushes them to Azure as defined by the signal CSV file.")]
    public class ValuePushProcessor : EboIotEdgeConnectorProcessorWithMqttBase
    {
        private const int MaxItemsPerSubscription = 500;
        private IManagedMqttClient _mqttClient;

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            //RegistryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(IotHubSettings.AzureConnectionString);
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

            var signals = Cache.RetrieveItem("CurrentSignalValues", tenantId: CacheTenantId);
            if (signals == null || !(signals is List<Signal>))
            {
                Prompts.Add(new Prompt { Message = "There are no signals in the cache, please run the SetupProcessor or verify that it has run successfully.", Severity = PromptSeverity.MayNotContinue });
                return Prompts;

            }
            var rawSignals = ((List<Signal>)signals);

            
            // Read existing subscriptions
            if (!ReadExistingSubscriptions(rawSignals).Result)
            {
                Prompts.Add(new Prompt { Message = $"Did not successfully read all existing subscriptions."});
            }

            // Subscribe and read new subscriptions
            if (!SubscribeAndReadNew(rawSignals).Result)
            {
                Prompts.Add(new Prompt { Message = $"Did not successfully read all new subscriptions." });
            }

            // Update the cache with new values..
            Cache.AddOrUpdateItem(rawSignals, "CurrentSignalValues", CacheTenantId, 0);
            return Prompts;
        }
        #endregion

        #region ReadExistingSubscription
        private async Task<bool> ReadExistingSubscriptions(List<Signal> signals)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Reading existing subscriptions..");
            //var deviceMapping = ProcessorValueSource.Items.FirstOrDefault(a => a.Key == "EboIotEdgeConnectorDeviceMapping" && a.Group == CacheTenantId && a.Aggregate == currentDevice.Key);
            //if (deviceMapping == null)
            //{
            //    Logger.LogInfo(LogCategory.Processor, this.Name, $"Azure IoT Hub Device Mapping does not exist for device {currentDevice.Key}, please make sure that the SetupProcessor has been successfully run.");
            //    Prompts.Add(new Prompt { Message = $"Azure IoT Hub Device Mapping does not exist for device {currentDevice.Key}, please make sure that the SetupProcessor has been successfully run.", Severity = PromptSeverity.MayContinue });
            //    return false;
            //}

            var activeSubscriptions = Cache.RetrieveItem($"ActiveSubscriptions", () => new List<string>(), CacheTenantId, 0) as List<string>;
            var activeSubscriptionsToIterate = activeSubscriptions.ToList();
            foreach (var sub in activeSubscriptionsToIterate)
            {
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

                    // Attempt to update the values by reading the subscription, if this fails return all Prompts
                    if (!await UpdateValues(si))
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
            //var deviceMapping = ProcessorValueSource.Items.FirstOrDefault(a => a.Key == "EboIotEdgeConnectorDeviceMapping" && a.Group == CacheTenantId && a.Aggregate == currentDevice.Key);
            //if (deviceMapping == null)
            //{
            //    Logger.LogInfo(LogCategory.Processor, this.Name, $"Azure IoT Hub Device Mapping does not exist for device {currentDevice.Key}, please make sure that the SetupProcessor has been successfully run.");
            //    Prompts.Add(new Prompt {Message = $"Azure IoT Hub Device Mapping does not exist for device {currentDevice.Key}, please make sure that the SetupProcessor has been successfully run.", Severity = PromptSeverity.MayContinue});
            //    return false;
            //}
            var activeSubscriptions = Cache.RetrieveItem($"ActiveSubscriptions", () => new List<string>(), CacheTenantId, 0);

            var subscribedIds = new List<string>();

            foreach (var subscription in activeSubscriptions)
            {
                if (Cache.RetrieveItem($"ActiveSubscriptions#{subscription}", CacheTenantId) is List<string> currentSub) subscribedIds.AddRange(currentSub);
            }

            var unsubscribedIds = signals.Select(a => a.EwsId).Where(a => !subscribedIds.Contains(a)).ToList();

            while (unsubscribedIds.Any())
            {
                try
                {
                    CheckCancellationToken();
                    var si = new SubscriptionReader
                    {
                        Address = EboEwsSettings.Address,
                        UserName = EboEwsSettings.UserName,
                        Password = EboEwsSettings.Password,
                        SubscriptionEventType = EwsSubscriptionEventTypeEnum.ValueItemChanged,
                        Ids = signals.Select(a => a.EwsId).Take(MaxItemsPerSubscription).ToList()
                    };

                    // Attempt to update the values by reading the subscription, if this fails return all false as this could go on forever.
                    if (!await UpdateValues(si)) return false;

                    Cache.AddOrUpdateItem(si.SubscribedItems, $"ActiveSubscriptions#{si.SubscriptionId}", CacheTenantId, 0);
                    unsubscribedIds = unsubscribedIds.Skip(MaxItemsPerSubscription).ToList();

                    activeSubscriptions.Add(si.SubscriptionId);
                    Cache.AddOrUpdateItem(activeSubscriptions, $"ActiveSubscriptions", CacheTenantId, 0);

                    // Add any prompts generated from reader to the list of prompts
                    Prompts.AddRange(si.ReadData().Prompts);
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
        private async Task<bool> UpdateValues(SubscriptionReader si)
        {
            var results = si.ReadData();
            if (!results.Success)
            {
                Prompts.AddRange(results.Prompts);
                return false;
            }

            var messages = new List<Sensor>();
            var deviceMessage = new DeviceData
            {
                //DeviceId = deviceMapping.Value,
                EventTime = DateTimeOffset.Now,
                Format = "vkcore0.1",
                PowerSource = "net230",
                //RoomId = currentDevice.Key,
                Sensors = messages
            };

            //AddUpdatedValuesToMessage(currentDevice, results, messages);
            //await ManagedMqttClient.PublishAsync(new ManagedMqttApplicationMessage())
            //await SendMessageToEboIotEdgeConnector(currentDevice, deviceMessage, messages);
            return true;
        }
        #endregion

        #region AddUpdatedValuesToMessage
        private void AddUpdatedValuesToMessage(IGrouping<string, Signal> currentDevice, ReadResult<SubscriptionResultItem> results, List<Sensor> messages)
        {
            foreach (var eventz in results.DataRead)
            {
                var signal = currentDevice.FirstOrDefault(a => a.EwsId == eventz.ValueItemChangeEvent.Id);
                if (signal == null)
                {
                    Logger.LogInfo(LogCategory.Processor, this.Name, $"Signal with EWS ID of {eventz.ValueItemChangeEvent.Id} does not exist.. Skipping this..");
                    continue;
                }

                if (signal.SendOnUpdate)
                {
                    messages.Add(new Sensor
                    {
                        Path = signal.EwsId,
                        Type = signal.Type,
                        Unit = signal.Unit,
                        Value = eventz.ValueItemChangeEvent.Value
                    });
                }

                signal.Value = eventz.ValueItemChangeEvent.Value;
            }

            foreach (var signal in currentDevice)
            {
                if (signal.LastSendTime != null &&
                    signal.LastSendTime.Value.AddSeconds(signal.SendTime) > DateTimeOffset.Now) continue;
                if (messages.All(a => a.Path != signal.EwsId))
                {
                    messages.Add(new Sensor
                    {
                        Path = signal.EwsId,
                        Type = signal.Type,
                        Unit = signal.Unit,
                        Value = signal.Value
                    });
                }
            }
        }
        #endregion
        #region SendMessageToEboIotEdgeConnector
        private async Task SendMessageToEboIotEdgeConnector(IGrouping<string, Signal> currentDevice, DeviceData deviceMessage, List<Sensor> messages)
        {
            //var rawMessage = new Message(Encoding.UTF8.GetBytes(deviceMessage.ToJSON()));

            //var azureDevice = AddDeviceAsync(deviceMapping.Value).Result;
            //var accessKey = azureDevice.Authentication.SymmetricKey.PrimaryKey;
            //var deviceConnectionString = $"{IotHubSettings.AzureConnectionString.Split(';')[0]};DeviceId={deviceMapping.Value};SharedAccessKey={accessKey}";
            //Logger.LogTrace(LogCategory.Processor, this.Name, $"Connecting to IoT Hub for device {deviceConnectionString}");
            //var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            //try
            //{
            //    Logger.LogDebug(LogCategory.Processor, this.Name, $"Sending message to Azure IoT Hub for device {currentDevice.Key}");
            //    await deviceClient.SendEventAsync(rawMessage);
            //    foreach (var sensor in messages)
            //    {
            //        var signal = currentDevice.FirstOrDefault(a => a.EwsId == sensor.Path);
            //        if (signal == null)
            //        {
            //            Logger.LogInfo(LogCategory.Processor, this.Name, $"");
            //            continue;
            //        }

            //        signal.LastSendTime = DateTimeOffset.Now;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
            //    Prompts.Add(new Prompt
            //    {
            //        Message = "Error sending message to Azure IoT Hub.",
            //        Severity = PromptSeverity.MayNotContinue
            //    });
            //}

            //deviceClient.Dispose();
        }
        #endregion

        #region HandleMqttApplicationMessageReceived
        public override void HandleMqttApplicationMessageReceived(string topic, string decodedMessage)
        {
            // In theory, this should not be receiving messages, just log this was unexpected

            Logger.LogInfo(LogCategory.Processor, this.Name, $"{this.Name} unexpectedely received a message..");
            throw new NotImplementedException();
        }
        #endregion

        #region MyRegion
        public override void SubscribeToMqttTopics()
        {
            // Not topics to subscribe to, intentionally blank
        } 
        #endregion
    }
}