using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Ews.Client;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using MQTTnet;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using MQTTnet.Server;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("MQTT Broker Processor", "This processor stands up an MQTT broker, which will broker requests between Smart Connector and the IoT Edge.")]
    public class MqttBrokerProcessor : Processor, ILongRunningProcess
    {
        private List<Prompt> _prompts = new List<Prompt>();
        private IMqttServer _mqttServer;

        #region IsLicensed
        public override bool IsLicensed
        {
            get
            {
                #if DEBUG
                return false;
                #else
                return true;
                #endif
            }
        }
        #endregion
        #region SecureCommunicationCertLocation
        [Tooltip("If a path is specified, secure communication will attempt to be used. If empty unsecure communication will be used.")]
        public string SecureCommunicationCertLocation { get; set; }
        #endregion
        #region BrokerPort
        [DefaultValue(1883)]
        public int BrokerPort { get; set; }
        #endregion
        #region EncryptedBrokerPort
        [DefaultValue(443)]
        public int EncryptedBrokerPort { get; set; }
        #endregion
        #region EboEwsSettings
        public EboEwsSettings EboEwsSettings { get; set; }
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            // Sets up ALL MQTT logging
            MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
            {
                if (e.TraceMessage.Exception != null)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, e.TraceMessage.Source, e.TraceMessage.Message);
                    Logger.LogError(LogCategory.Processor, this.Name, e.TraceMessage.Source, e.TraceMessage.Exception.ToJSON());
                }
                else
                {
                    Logger.LogTrace(LogCategory.Processor, this.Name, e.TraceMessage.Source, e.TraceMessage.Message.ToJSON());
                }
            };
            StartMqttServer().Wait();
            MainLoop().Wait();
            return _prompts;
        }
        #endregion
        #region Validate - Override
        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(SecureCommunicationCertLocation))
            {
                if (!IsValidFilePath(SecureCommunicationCertLocation))
                {
                    yield return new ValidationResult("Certificate Location is not a valid file path");
                }
                else
                {
                    if (!CertExists(SecureCommunicationCertLocation))
                    {
                        yield return new ValidationResult("Certicate does not exist at the path supplied.");
                    }
                }
            }
        }
        #endregion

        #region MainLoop
        private async Task MainLoop()
        {
            for (;;)
            {
                try
                {
                    if (IsCancellationRequested)
                    {
                        await _mqttServer.StopAsync();
                        return;
                    }
                    await Task.Delay(5 * 1000, CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                    return;
                }
            }
        }
        #endregion

        #region StartMqttServer
        private async Task StartMqttServer()
        {
            // Start a MQTT server.
            Logger.LogInfo(LogCategory.Processor, this.Name, "Starting MQTT Server..");

            _mqttServer = new MqttFactory().CreateMqttServer();

            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithDefaultEndpointPort(BrokerPort)
                .WithConnectionValidator(AuthenticateUser)
                .WithStorage(new RetainedMqttMessageHandler());

            if (!string.IsNullOrEmpty(SecureCommunicationCertLocation))
            {
                optionsBuilder.WithEncryptedEndpoint().WithEncryptedEndpointPort(EncryptedBrokerPort);
            }

            var options = optionsBuilder.Build();

            if (!string.IsNullOrEmpty(SecureCommunicationCertLocation))
            {
                var certificate = new X509Certificate(SecureCommunicationCertLocation, "");
                options.TlsEndpointOptions.Certificate = certificate.Export(X509ContentType.Cert);
            }

            _mqttServer.Started += (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Started", a.ToJSON());
            };

            _mqttServer.ApplicationMessageReceived += async (s, a) =>
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Server Received Message", a.ToJSON());
            };

            _mqttServer.ClientSubscribedTopic += async (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Client Subscribed to Topic", a.ToJSON());
            };

            _mqttServer.ClientUnsubscribedTopic += async (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Client Unsubscribed to Topic", a.ToJSON());
            };

            _mqttServer.ClientConnected += async (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Client Connected", a.ToJSON());
            };

            _mqttServer.ClientDisconnected += async (s, a) =>
            {
                //Logger.LogTrace(LogCategory.Processor, this.Name, "MQTT Client Disconnected", a.ToJSON());
            };

            await _mqttServer.StartAsync(options);
        }
        #endregion
        #region AuthenticateUser
        private void AuthenticateUser(MqttConnectionValidatorContext c)
        {
            Logger.LogDebug(LogCategory.RestServe, $"Attempting to authenticate {EboEwsSettings.UserName} with password {EboEwsSettings.Password}");
            try
            {
                var client = MongooseObjectFactory.Current.GetInstance<IManagedEwsClient>();
                client.EwsVersionImplemented(EboEwsSettings);
                c.ReturnCode = MqttConnectReturnCode.ConnectionAccepted;
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.RestServe, ex);
                c.ReturnCode = MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
            }
        }
        #endregion
        #region IsValidFilePath
        private bool IsValidFilePath(string filePath)
        {
            try
            {
                var file = new FileInfo(SecureCommunicationCertLocation);
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
        #endregion
        #region CertExists
        private bool CertExists(string filePath)
        {
            try
            {
                var file = new FileInfo(SecureCommunicationCertLocation);
                return file.Exists;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion
    }
}