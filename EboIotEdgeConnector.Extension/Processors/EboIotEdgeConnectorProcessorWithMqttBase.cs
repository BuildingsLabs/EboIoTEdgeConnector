using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public abstract class EboIotEdgeConnectorProcessorWithMqttBase : EboIotEdgeConnectorProcessorBase
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
        protected EboIotEdgeConnectorProcessorWithMqttBase()
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
                    if (!double.TryParse(signal.Value, out double doubleResult))
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

            var toSetValue = ToConvertedTypeValue(signal);
            if (toSetValue.wasValidValue == true)
            {
                observations.Add(new Observation
                {
                    SensorId = signal.PointName,
                    ObservationTime = signal.LastUpdateTime.Value.ToUniversalTime(),
                    Value = toSetValue.value,
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
            Logger.LogInfo(LogCategory.Processor, this.Name, "Starting MQTT client..");
            ManagedMqttClient = new MqttFactory().CreateManagedMqttClient();

            ManagedMqttClient.Connected += (sender, args) =>
            {
                // TODO: Anything we should be sending every time when we connect to the broker?
            };

            ManagedMqttClient.Disconnected += async (sender, args) =>
            {
                // TODO: Anything we should be doing every time we disconnect (Managed client will automatically reconnect)
            };

            // This is event is hit when we receive a message from the broker.
            ManagedMqttClient.ApplicationMessageReceived += (s, a) =>
            {
                var topic = a.ApplicationMessage.Topic;
                var decodedString = Encoding.UTF8.GetString(a.ApplicationMessage.Payload);
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Message from topic '{topic}' received.");
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Decoded Message: {decodedString}");
                HandleMqttApplicationMessageReceived(topic, decodedString);
            };

            // This just tells us that a message we sent was received succesfully by the broker.
            ManagedMqttClient.ApplicationMessageProcessed += (s, a) =>
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, "Client Message Processed by Broker", a.ToJSON());
                if (a.HasSucceeded == false)
                {
                    // TODO: What to do here?
                    // Add to a file? And retry later?
                }
            };

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
                .Build();
            return options;
        }
        #endregion
    }
}