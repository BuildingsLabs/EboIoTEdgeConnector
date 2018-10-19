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
        private Logger _logger;

        #region Create - IGatewayModule Member
        public void Create(Broker broker, byte[] configuration)
        {
            _moduleConfiguration = ModuleConfiguration.FromJson(Encoding.UTF8.GetString(configuration));
            ConfigureLogging(_moduleConfiguration.LoggingLevel);
            _logger.Debug($"Create Module Called with Configuration: {Encoding.UTF8.GetString(configuration)}");
            _broker = broker;

            StartMqttClient().Wait();
        } 
        #endregion
        #region Destroy - IGatewayModule Member
        public void Destroy()
        {
            _logger.Debug("Destory Module Called..");
            _managedMqttClient.Dispose();
        } 
        #endregion
        #region Receive - IGatewayModule Member
        public async void Receive(Message receivedMessage)
        {
            _logger.Debug($"IoT Edge Message Received from Broker: {JsonConvert.SerializeObject(receivedMessage)}");
            try
            {
                // Let's forward it along to Smart Connector.
                var messageString = Encoding.ASCII.GetString(receivedMessage.Content);
                _logger.Debug($"Message: {messageString}");
                await _managedMqttClient.PublishAsync(_moduleConfiguration.MqttValueSendTopic, messageString, MqttQualityOfServiceLevel.AtLeastOnce, true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }
        } 
        #endregion

        #region StartMqttClient
        public virtual async Task StartMqttClient()
        {
            _logger.Info("Starting MQTT client..");
            _managedMqttClient = new MqttFactory().CreateManagedMqttClient();

            // This is event is hit when we receive a message from the broker.
            _managedMqttClient.ApplicationMessageReceived += (s, a) =>
            {
                var topic = a.ApplicationMessage.Topic;
                var decodedString = Encoding.UTF8.GetString(a.ApplicationMessage.Payload);
                _logger.Trace($"Message from topic '{topic}' received.");
                _logger.Trace($"Decoded Message: {decodedString}");
                HandleMqttApplicationMessageReceived(topic, decodedString);
            };

            // This just tells us that a message we sent was received successfully by the broker.
            _managedMqttClient.ApplicationMessageProcessed += (s, a) =>
            {
                _logger.Trace("Client Message Processed by Broker", a);
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
            if (topic == _moduleConfiguration.MqttValuePushTopic)
            {
                // This message was received in theory from Smart Connector in the correct format. Forward it to the IoT Edge Broker
                try
                {
                    _logger.Trace($"Sending updated values to Gateway Broker: {decodedMessageString}");
                    var properties = new Dictionary<string, string>();
                    var message = new Message(decodedMessageString, properties);
                    _broker.Publish(message);
                }
                catch (Exception e)
                {
                    _logger.Error(e.ToString);
                }
            }
            else
            {
                _logger.Info($"Unknown topic received: {topic}. Ignoring this.");
            }
        }
        #endregion
        #region ConfigureLogging
        private void ConfigureLogging(string loggingLevel)
        {
            // Set logging level
            LogLevel minLoggingLevel;
            switch (loggingLevel)
            {
                case "Fatal":
                    minLoggingLevel = LogLevel.Fatal;
                    break;
                case "Error":
                    minLoggingLevel = LogLevel.Error;
                    break;
                case "Warn":
                    minLoggingLevel = LogLevel.Warn;
                    break;
                case "Info":
                    minLoggingLevel = LogLevel.Info;
                    break;
                case "Debug":
                    minLoggingLevel = LogLevel.Debug;
                    break;
                case "Trace":
                    minLoggingLevel = LogLevel.Trace;
                    break;
                default:
                    minLoggingLevel = LogLevel.Info;
                    break;
            }

            var config = new NLog.Config.LoggingConfiguration();
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "EboIoTEdgeConnectorLog.log" };
            config.AddRule(minLoggingLevel, LogLevel.Fatal, logfile);
            NLog.LogManager.Configuration = config;

            _logger = NLog.LogManager.GetCurrentClassLogger();

            // Configure logging for MQTT client.
            MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
            {
                if (e.TraceMessage.Exception != null)
                {
                    _logger.Error(e.TraceMessage.Source, e.TraceMessage.Message);
                    _logger.Error(e.TraceMessage.Source, e.TraceMessage.Exception.ToString());
                }
                else
                {
                    _logger.Debug(e.TraceMessage.Source, e.TraceMessage.Message);
                }
            };
        }
        #endregion
    }
}