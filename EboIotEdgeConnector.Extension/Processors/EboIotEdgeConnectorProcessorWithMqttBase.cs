using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public abstract class EboIotEdgeConnectorProcessorWithMqttBase : EboIotEdgeConnectorProcessorBase, IApplicationMessageProcessedHandler, IMqttApplicationMessageReceivedHandler
    {
        #region ManagedMqttClient
        internal IManagedMqttClient ManagedMqttClient;
        #endregion
        #region MqttBrokerSettings
        [Tooltip("The settings required to connect to the MQTT Broker for IoT Edge.")]
        public MqttBroker MqttBrokerSettings { get; set; }
        #endregion
        #region MqttClientId
        public string MqttClientId { get; set; }
        #endregion
        #region ValuePushTopic
        [Required, DefaultValue("eboiotedgeconnector/newvalues")]
        public string ValuePushTopic { get; set; }
        #endregion

        #region Constructor
        public EboIotEdgeConnectorProcessorWithMqttBase()
        {
            MqttBrokerSettings = new MqttBroker();
        } 
        #endregion

        #region HandleMqttApplicationMessageReceived - Abstract
        public abstract void HandleMqttApplicationMessageReceived(string topic, string decodedMessage);
        #endregion
        #region SubscribeToMqttTopics - Abstract
        public abstract void SubscribeToMqttTopics();
        #endregion

        #region ToConvertedTypeValue - Static
        public static (bool wasValidValue, dynamic value) ToConvertedTypeValue(Signal signal)
        {
            switch (signal.Type)
            {
                case EwsValueTypeEnum.DateTime:
                    if (!DateTime.TryParse(signal.Value, out DateTime dateTimeResult))
                    {
                        return (false, signal.Value);
                    }
                    else
                    {
                        return (true, dateTimeResult);
                    }
                case EwsValueTypeEnum.Boolean:
                    if (signal.Value == "0") signal.Value = "false";
                    if (signal.Value == "1") signal.Value = "true";
                    if (!bool.TryParse(signal.Value, out bool boolResult))
                    {
                        return (false, signal.Value);
                    }
                    else
                    {
                        return (true, boolResult);
                    }
                case EwsValueTypeEnum.String:
                    return (true, signal.Value);
                case EwsValueTypeEnum.Double:
                    if (!double.TryParse(signal.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleResult))
                    {
                        return (false, signal.Value);
                    }
                    else
                    {
                        return (true, doubleResult);
                    }
                case EwsValueTypeEnum.Long:
                case EwsValueTypeEnum.Integer:
                    if (!int.TryParse(signal.Value, out int intResult))
                    {
                        return (false, signal.Value);
                    }
                    else
                    {
                        return (true, intResult);
                    }
                case EwsValueTypeEnum.Duration:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
        #region HandleAddingToObservationsList
        public virtual void HandleAddingToObservationsList(List<Observation> observations, Signal signal, bool sendAdditionalProperties = false)
        {
            // This means a value has never come back from EWS for this singal, it could be an invalid point ID or some other reason..
            if (!signal.LastUpdateTime.HasValue)
            {
                Logger.LogDebug(LogCategory.Processor, this.Name, $"Signal {signal.EwsId} has never gotten an updated value from the EWS server, skipping this one..");
                return;
            }

            var (wasValidValue, value) = ToConvertedTypeValue(signal);
            if (wasValidValue)
            {
                observations.Add(new Observation
                {
                    SensorId = signal.PointName,
                    ObservationTime = signal.LastUpdateTime.Value.ToUniversalTime(),
                    Value = value,
                    Writeable = sendAdditionalProperties ? signal.IsWriteable : null,
                    Forceable = sendAdditionalProperties ? signal.IsForceable : null
                });
                signal.LastSendTime = DateTime.UtcNow;
            }
            else
            {
                Logger.LogInfo(LogCategory.Processor, this.Name, $"A value of '{signal.Value}' is not a valid {signal.Type}. Signal with ID {signal.EwsId}");
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Value of signal could not be converted to it's correct type: {signal.ToJSON()}");
            }
        }
        #endregion

        #region StartMqttClient - Virtual
        public virtual async Task StartMqttClient()
        {
            Logger.LogDebug(LogCategory.Processor, this.Name, "Starting MQTT client..");
            ManagedMqttClient = new MqttFactory().CreateManagedMqttClient();
            ManagedMqttClient.ApplicationMessageProcessedHandler = this;
            ManagedMqttClient.ApplicationMessageReceivedHandler = this;

            SubscribeToMqttTopics();

            await ManagedMqttClient.StartAsync(GetMqttClientOptions());
        }
        #endregion
        #region GetMqttClientOptions - Virtual
        public virtual ManagedMqttClientOptions GetMqttClientOptions()
        {
            var clientOptions = new MqttClientOptionsBuilder();
            if (MqttBrokerSettings.IsEncryptedCommunication)
            {
                clientOptions.WithTls();
            }

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(clientOptions
                    .WithClientId(MqttClientId)
                    .WithTcpServer(MqttBrokerSettings.BrokerAddress, MqttBrokerSettings.Port)
                    .WithCredentials(EboEwsSettings.UserName, EboEwsSettings.Password).Build())
                .WithStorage(new ClientRetainedMessageHandler(this.Name))
                .Build();
            return options;
        }
        #endregion
        #region HandleApplicationMessageProcessedAsync - IApplicationMessageProcessedHandler Member
        public async Task HandleApplicationMessageProcessedAsync(ApplicationMessageProcessedEventArgs eventArgs)
        {
            // This just tells us that a message we sent was received successfully by the broker.
            await Task.Run(() =>
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, "Client Message Processed by Broker", eventArgs.ToJSON());
                if (eventArgs.HasSucceeded == false)
                {
                    // TODO: What to do here?
                    // Add to a file? And retry later?
                }
            });
        }
        #endregion
        #region HandleApplicationMessageReceivedAsync - IMqttApplicationMessageReceivedHandler
        public async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            // This is event is hit when we receive a message from the broker.
            await Task.Run(() =>
            {
                var topic = eventArgs.ApplicationMessage.Topic;
                var decodedString = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Message from topic '{topic}' received.");
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Decoded Message: {decodedString}");
                HandleMqttApplicationMessageReceived(topic, decodedString);
            });
        } 
        #endregion
    }
}