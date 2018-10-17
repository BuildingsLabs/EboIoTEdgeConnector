using System;
using System.Text;
using System.Threading.Tasks;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.ManagedClient;
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