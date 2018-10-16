using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Ews.Client;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using MQTTnet;
using Newtonsoft.Json;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Request Receive Processor", "This processor runs forever, and manages requests from the IoT Edge for setting values back into EBO.")]
    public class RequestReceiveProcessor : EboIotEdgeConnectorProcessorWithMqttBase, ILongRunningProcess
    {
        #region ValueReceiveTopic
        [Required, DefaultValue("eboiotedgeconnector/sendvalues")]
        public string ValueReceiveTopic { get; set; } 
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            StartMqttClient().Wait();

            for (;;)
            {
                if (IsCancellationRequested) return Prompts;
                Task.Delay(5000).Wait();
            }
        }
        #endregion

        #region HandleMqttApplicationMessageReceived - Override
        public override void HandleMqttApplicationMessageReceived(string topic, string decodedMessage)
        {
            if (topic == ValueReceiveTopic)
            {
                HandleWriteToEboFromIotEdge(decodedMessage);
            }
            else
            {
                Logger.LogInfo(LogCategory.Processor, this.Name, $"Unknown topic was received: {topic}. Ignoring it.");
            }
        }
        #endregion
        #region SubscribeToMqttTopics - Override
        public override async void SubscribeToMqttTopics()
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Subscribing to topic: {ValueReceiveTopic}");
            await ManagedMqttClient.SubscribeAsync(new List<TopicFilter> {new TopicFilterBuilder().WithTopic(ValueReceiveTopic).WithAtLeastOnceQoS().Build()});
        }
        #endregion

        #region HandleWriteToEboFromIotEdge
        private void HandleWriteToEboFromIotEdge(string decodedString)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Starting to write values sent from ");
            try
            {
                if (Signals == null)
                {
                    Logger.LogInfo(LogCategory.Processor, this.Name, $"There are no Signals in the in-memory cache. Please check that the Setup Processor has been run.");
                }
                var message = JsonConvert.DeserializeObject<MqttValueWrite>(decodedString);
                if (message == null) return;
                var toSet = new List<ValueTypeStateless>();
                foreach (var option in message.ValuesToWrite)
                {
                    if (Signals.Select(a => a.EwsId).Contains(option.Id))
                    {
                        toSet.Add(option);
                    }
                    else
                    {
                        Logger.LogInfo(LogCategory.Processor, this.Name, $"{option.Id} does not exist in the list of monitored values. Skipping.");
                    }
                }

                //var successfulWrites = new MqttDeviceConfig { MqttDeviceConfigOptions = new List<ValueTypeStateless>() };

                if (toSet.Any())
                {
                    var setValues = ManagedEwsClient.SetValues(EboEwsSettings, toSet.ToArray());

                    foreach (var result in setValues.SetValuesResults.Where(b => !b.Success))
                    {
                        Logger.LogError(LogCategory.Processor, this.Name, $"Did not successfully set value {result.Id} with error message {result.Message}");
                    }

                    //foreach (var result in setValues.SetValuesResults.Where(b => b.Success))
                    //{
                    //    successfulWrites.MqttDeviceConfigOptions.Add(message.MqttDeviceConfigOptions.FirstOrDefault(b => b.Id == result.Id));
                    //}
                }

                // TODO: Do I need to write back that it was successfull in some way?
                //if (successfulWrites.MqttDeviceConfigOptions.Any()) await ManagedMqttClient.PublishAsync($"/devices/{EboIotDevice.GoogleIotDeviceId}/state", successfulOptions.ToJSON());
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.Processor, this.Name, ex.ToJSON());
            }
        } 
        #endregion
    }
}