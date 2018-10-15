using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.IoT.Gateway;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace EboIotEdgeConnector.IotEdge
{
    public class EboIotEdgeConnectorModule : IGatewayModule
    {
        private IManagedMqttClient _mqttClient;

        public void Create(Broker broker, byte[] configuration)
        {
            StartMqttClient();
            throw new NotImplementedException();
        }

        public void Destroy()
        {
            throw new NotImplementedException();
        }

        public void Receive(Message received_message)
        {
            throw new NotImplementedException();
        }


        private async Task<bool> StartMqttClient()
        {
            //Logger.LogInfo(LogCategory, "Starting MQTT client..");
            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            _mqttClient.Connected += (sender, args) =>
            {
                // TODO: Anything we should be sending every time when we connect to the broker?
            };

            _mqttClient.Disconnected += async (sender, args) =>
            {
                // TODO: Anything we should be doing every time we disconnect (Managed client will automatically reconnect)
            };

            // This is event is hit when we receive a message from the broker.
            _mqttClient.ApplicationMessageReceived += (s, a) =>
            {
                var topic = a.ApplicationMessage.Topic;
                var decodedString = Encoding.UTF8.GetString(a.ApplicationMessage.Payload);
                //Logger.LogTrace(LogCategory, $"Message from topic '{topic}' received.");
                //Logger.LogTrace(LogCategory, $"Decoded Message: {decodedString}");

                //if (a.ClientId != SecureCareMqttClient.ClientId)
                //{
                //    Logger.LogError($"Wrong client ID received, was {a.ClientId}, should be {SecureCareMqttClient.ClientId}");
                //    return;
                //}

                //if (topic == $"{SecureCareMqttClient.ClientId}/securecare/hierarchy")
                //{
                //    if (!HandleHierarchyMessage(decodedString))
                //    {
                //        Logger.LogError(LogCategory.Processor, this.Name, "Handling of Hierarchy message was NOT successful.");
                //    }

                //    Logger.LogTrace(LogCategory.Processor, $"HandledHierarchy Complete");
                //}
                //else if (topic == $"{SecureCareMqttClient.ClientId}/securecare/allactivealarms")
                //{
                //    if (!HandleAllActiveAlarmsMesssage(decodedString))
                //    {
                //        Logger.LogError(LogCategory.Processor, this.Name, "Handling of All Active Alarms message was NOT successful.");
                //    }
                //}
                //else if (topic == $"{SecureCareMqttClient.ClientId}/securecare/activealarm")
                //{
                //    if (!HandleActiveAlarmMessage(decodedString))
                //    {
                //        Logger.LogError(LogCategory.Processor, this.Name, "Handling of Active Alarm message was NOT successful.");
                //    }
                //}
                //else
                //{
                //    Logger.LogInfo(LogCategory.Processor, this.Name, $"{a.ApplicationMessage.Topic} is not a handled topic");
                //    return;
                //}
            };

            // This just tells us that a message we sent was received succesfully by the broker.
            _mqttClient.ApplicationMessageProcessed += (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "Client Message Processed by Broker", a.ToJSON());
                if (a.HasSucceeded == false)
                {
                    // TODO: What to do here?
                    // Add to a file? And retry later?
                }
            };

            //await _mqttClient.SubscribeAsync(new TopicFilterBuilder()
            //    .WithTopic($"{SecureCareMqttClient.ClientId}/securecare/hierarchy").WithAtLeastOnceQoS().Build());
            //await _mqttClient.SubscribeAsync(new TopicFilterBuilder()
            //    .WithTopic($"{SecureCareMqttClient.ClientId}/securecare/allactivealarms").WithAtLeastOnceQoS().Build());
            //await _mqttClient.SubscribeAsync(new TopicFilterBuilder()
            //    .WithTopic($"{SecureCareMqttClient.ClientId}/securecare/activealarm").WithAtLeastOnceQoS().Build());

            //await _mqttClient.StartAsync(GetMqttClientOptions());

            return true;
        }

        #region GetMqttClientOptions
        private ManagedMqttClientOptions GetMqttClientOptions()
        {
            var clientOptions = new MqttClientOptionsBuilder();
            //if (MqttBroker.IsEncryptedCommunication)
            //{
            //    clientOptions.WithTls();
            //}

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                //.WithClientOptions(clientOptions
                //    .WithClientId(SmartConnectorMqttClient.ClientId)
                //    .WithTcpServer(MqttBroker.BrokerAddress, MqttBroker.Port)
                //    .WithCredentials(SmartConnectorMqttClient.UserName, SmartConnectorMqttClient.Password).Build())
                .Build();
            return options;
        }
        #endregion
    }
}
