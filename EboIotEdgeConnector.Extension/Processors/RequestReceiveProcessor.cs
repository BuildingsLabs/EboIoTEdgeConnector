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
                if (IsCancellationRequested)
                {
                    ManagedMqttClient.StopAsync().Wait();
                    return Prompts;
                }
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
                    return;
                }
                var iotEdgeMessage = JsonConvert.DeserializeObject<IotEdgeMessage>(decodedString);
                if (iotEdgeMessage == null || !iotEdgeMessage.Actuations.Any()) return;

                var toSet = GetListOfValuesToSetInEbo(iotEdgeMessage);
                SetValuesInEbo(toSet, iotEdgeMessage);
                SendConfirmationResponseToIotEdge(iotEdgeMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.Processor, this.Name, ex.ToJSON());
            }
        }
        #endregion
        #region GetListOfValuesToSetInEbo
        private List<(ValueTypeStateless valueTypeStateless, Signal signal)> GetListOfValuesToSetInEbo(IotEdgeMessage iotEdgeMessage)
        {
            var toSet = new List<(ValueTypeStateless valueTypeStateless, Signal signal)>();
            foreach (var option in iotEdgeMessage.Actuations)
            {
                var theSignalToSet = Signals.FirstOrDefault(a => a.DatabasePath == $"{iotEdgeMessage.DeviceId}/{option.ActuatorId}");

                if (theSignalToSet == null)
                {
                    Logger.LogInfo(LogCategory.Processor, this.Name, $"{iotEdgeMessage.DeviceId}/{option.ActuatorId} does not exist in the list of monitored values. Skipping.");
                    if (iotEdgeMessage.Exceptions == null) iotEdgeMessage.Exceptions = new List<ExceptionElement>();
                    iotEdgeMessage.Exceptions.Add(new ExceptionElement
                    {
                        ExceptionTime = DateTimeOffset.UtcNow,
                        Exception = $"{iotEdgeMessage.DeviceId}/{option.ActuatorId} does not exist in the list of monitored values. Please make sure that the configuration CSV file contains this point",
                        Retry = 1,
                        SensorId = option.ActuatorId
                    });
                }
                else
                {
                    toSet.Add(new ValueTuple<ValueTypeStateless, Signal>(
                        new ValueTypeStateless { Id = theSignalToSet.EwsIdForWrite, Value = option.Value },
                        theSignalToSet));
                }
            }
            return toSet;
        }
        #endregion
        #region SetValuesInEbo
        private void SetValuesInEbo(List<(ValueTypeStateless valueTypeStateless, Signal signal)> toSet, IotEdgeMessage iotEdgeMessage)
        {
            if (toSet.Any())
            {
                SetValuesResponse setValues;
                try
                {
                    setValues = ManagedEwsClient.SetValues(EboEwsSettings,toSet.Select(a => a.valueTypeStateless).ToArray());
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                    return;
                }

                foreach (var result in setValues.SetValuesResults.Where(b => !b.Success))
                {
                    var signal = toSet.FirstOrDefault(a => a.signal.EwsIdForWrite == result.Id).signal;
                    Logger.LogError(LogCategory.Processor, this.Name, $"Did not successfully set value {result.Id} with error message {result.Message}");
                    if (iotEdgeMessage.Exceptions == null) iotEdgeMessage.Exceptions = new List<ExceptionElement>();
                    iotEdgeMessage.Exceptions.Add(new ExceptionElement
                    {
                        ExceptionTime = DateTimeOffset.UtcNow,
                        Exception = result.Message,
                        Retry = 1,
                        SensorId = signal.PointName
                    });
                }

                foreach (var result in setValues.SetValuesResults.Where(b => b.Success))
                {
                    var signal = toSet.FirstOrDefault(a => a.signal.EwsIdForWrite == result.Id).signal;
                    var valueTypeStateless = toSet.FirstOrDefault(a => a.valueTypeStateless.Id == signal.EwsIdForWrite).valueTypeStateless;
                    signal.Value = valueTypeStateless.Value;
                    signal.LastUpdateTime = DateTime.UtcNow.ToUniversalTime();
                    if (iotEdgeMessage.Observations == null) iotEdgeMessage.Observations = new List<Observation>();
                    HandleAddingToObservationsList(iotEdgeMessage.Observations, signal);
                }
            }
        }
        #endregion
        #region SendConfirmationResponseToIotEdge
        private void SendConfirmationResponseToIotEdge(IotEdgeMessage iotEdgeMessage)
        {
            iotEdgeMessage.Actuations = null;
            var messageBuilder = new MqttApplicationMessageBuilder();
            var message = messageBuilder.WithRetainFlag().WithAtLeastOnceQoS().WithTopic(ValuePushTopic).WithPayload(iotEdgeMessage.ToJson()).Build();
            ManagedMqttClient.PublishAsync(message).Wait();
            // Update the cache with new values..
            Signals = Signals;
        }
        #endregion
    }
}