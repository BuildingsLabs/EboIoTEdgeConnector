﻿using System;
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

        #region Constructor
        public MqttBrokerProcessor()
        {
            EboEwsSettings = new EboEwsSettings();
        }
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            ConfigureMqttLogging();
            StartMqttServer().Wait();

            for (; ; )
            {
                try
                {
                    CheckCancellationToken();
                    if (!_mqttServer.IsStarted)
                    {
                        Logger.LogError(LogCategory.Processor, this.Name, "Found that the MQTT Broker has stopped unexpectedly, restarting..");
                        StartMqttServer().Wait();
                    }
                    Task.Delay(5 * 1000, CancellationToken).Wait();
                }
                catch (OperationCanceledException ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                    return _prompts;
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                }
            }
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
        #region CleanupBeforeCancellation - Override
        protected override void CleanupBeforeCancellation()
        {
            Logger.LogInfo(LogCategory.Processor, this.Name, "Shutting down MQTT Broker..");
            _mqttServer.StopAsync().Wait();
            base.CleanupBeforeCancellation();
        }
        #endregion

        #region ConfigureMqttLogging
        private static void ConfigureMqttLogging()
        {
            if (!MqttNetGlobalLogger.HasListeners)
            {
                MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
                {
                    switch (e.LogMessage.Level)
                    {
                        case MqttNetLogLevel.Verbose:
                            Logger.LogTrace("MQTT", "MQTTNet", e.LogMessage.Source, $"Thread: {e.LogMessage.ThreadId}", e.LogMessage.Message,
                                e.LogMessage.Exception?.ToJSON());
                            break;
                        case MqttNetLogLevel.Info:
                            Logger.LogInfo("MQTT", "MQTTNet", e.LogMessage.Source, $"Thread: {e.LogMessage.ThreadId}", e.LogMessage.Message,
                                e.LogMessage.Exception?.ToJSON());
                            break;
                        case MqttNetLogLevel.Warning:
                            Logger.LogStatus("MQTT", "MQTTNet", e.LogMessage.Source, $"Thread: {e.LogMessage.ThreadId}", e.LogMessage.Message,
                                e.LogMessage.Exception?.ToJSON());
                            break;
                        case MqttNetLogLevel.Error:
                            Logger.LogError("MQTT", "MQTTNet", e.LogMessage.Source, $"Thread: {e.LogMessage.ThreadId}", e.LogMessage.Message,
                                e.LogMessage.Exception?.ToJSON());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                };
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