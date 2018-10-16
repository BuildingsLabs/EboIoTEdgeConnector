using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Gateway;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using NLog;

namespace EboIotEdgeConnector.IotEdge
{
    public class EboIotEdgeConnectorModule : IGatewayModule
    {
        private IManagedMqttClient _managedMqttClient;
        private ModuleConfiguration _moduleConfiguration;
        private Broker _broker;
        internal Logger Logger;

        #region Create - IGatewayModule Member
        public void Create(Broker broker, byte[] configuration)
        {
            ConfigureLogging();
            Logger.Debug("Create Module Called..");
            _broker = broker;
            Logger.Trace(Encoding.UTF8.GetString(configuration));
            _moduleConfiguration = ModuleConfiguration.FromJson(Encoding.UTF8.GetString(configuration));
            StartMqttClient().Wait();
        } 
        #endregion
        #region Destroy - IGatewayModule Member
        public void Destroy()
        {
            Logger.Debug("Destory Module Called..");
            _managedMqttClient.Dispose();
        } 
        #endregion
        #region Receive - IGatewayModule Member
        public async void Receive(Message receivedMessage)
        {
            Logger.Debug($"IoT Edge Message Received from Broker..");
            Logger.Debug(JsonConvert.SerializeObject(receivedMessage));
            try
            {
                var messageString = Encoding.ASCII.GetString(receivedMessage.Content);
                Logger.Debug($"Message: {messageString}");
                // TODO: Convert message to a MqttValueWrite Object
                var fortesting = MqttValueWrite.FromJson(messageString);
                //var valueWrite = new MqttValueWrite();
                await _managedMqttClient.PublishAsync(_moduleConfiguration.MqttValueSendTopic, fortesting.ToJson(), MqttQualityOfServiceLevel.AtLeastOnce, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        } 
        #endregion

        #region StartMqttClient
        public virtual async Task StartMqttClient()
        {
            Logger.Info("Starting MQTT client..");
            _managedMqttClient = new MqttFactory().CreateManagedMqttClient();

            _managedMqttClient.Connected += (sender, args) =>
            {
                // TODO: Anything we should be sending every time when we connect to the broker?
            };

            _managedMqttClient.Disconnected += async (sender, args) =>
            {
                // TODO: Anything we should be doing every time we disconnect (Managed client will automatically reconnect)
            };

            // This is event is hit when we receive a message from the broker.
            _managedMqttClient.ApplicationMessageReceived += (s, a) =>
            {
                var topic = a.ApplicationMessage.Topic;
                var decodedString = Encoding.UTF8.GetString(a.ApplicationMessage.Payload);
                Logger.Trace($"Message from topic '{topic}' received.");
                Logger.Trace($"Decoded Message: {decodedString}");
                HandleMqttApplicationMessageReceived(topic, decodedString);
            };

            // This just tells us that a message we sent was received succesfully by the broker.
            _managedMqttClient.ApplicationMessageProcessed += (s, a) =>
            {
                Logger.Trace("Client Message Processed by Broker", a);
                if (a.HasSucceeded == false)
                {
                    // TODO: What to do here?
                    // Add to a file? And retry later?
                }
            };

            await _managedMqttClient.SubscribeAsync(new List<TopicFilter> { new TopicFilterBuilder().WithTopic(_moduleConfiguration.MqttValuePushTopic).WithAtLeastOnceQoS().Build() });

            await _managedMqttClient.StartAsync(GetMqttClientOptions());
        }
        #endregion
        #region GetMqttClientOptions
        private ManagedMqttClientOptions GetMqttClientOptions()
        {
            var clientOptions = new MqttClientOptionsBuilder();
            if (_moduleConfiguration.UseSecureCommunication)
            {
                clientOptions.WithTls();
            }

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(clientOptions
                    .WithClientId(_moduleConfiguration.MqttClientId)
                    .WithTcpServer(_moduleConfiguration.MqttBrokerAdress, _moduleConfiguration.MqttBrokerPort)
                    .WithCredentials(_moduleConfiguration.MqttBrokerUserName, _moduleConfiguration.MqttBrokerPassword).Build())
                .Build();
            return options;
        }
        #endregion
        #region HandleMqttApplicationMessageReceived
        private void HandleMqttApplicationMessageReceived(string topic, string decodedMessageString)
        {
            // TODO: Convert to schema if needed.. but in theory will just be forwarded from the MQTT message
            if (topic == _moduleConfiguration.MqttValuePushTopic)
            {
                try
                {
                    Logger.Trace($"Sending updated values to Gateway Broker: {decodedMessageString}");
                    var properties = new Dictionary<string, string>();
                    var message = new Message(decodedMessageString, properties);
                    _broker.Publish(message);
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString);
                }
            }
            else
            {
                Logger.Info($"Unknown topic received: {topic}. Ignoring this.");
            }
        }
        #endregion
        #region ConfigureLogging
        private void ConfigureLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "EboIoTEdgeConnectorLog.log" };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);
            NLog.LogManager.Configuration = config;

            Logger = NLog.LogManager.GetCurrentClassLogger();

            MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
            {
                if (e.TraceMessage.Exception != null)
                {
                    Logger.Error(e.TraceMessage.Source, e.TraceMessage.Message);
                    Logger.Error(e.TraceMessage.Source, e.TraceMessage.Exception.ToString());
                }
                else
                {
                    Logger.Debug(e.TraceMessage.Source, e.TraceMessage.Message);
                }
            };
        }
        #endregion
    }
}
