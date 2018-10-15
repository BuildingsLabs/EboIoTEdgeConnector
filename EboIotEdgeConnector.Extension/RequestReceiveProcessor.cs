using System.Collections.Generic;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Request Receive Processor", "This processor runs forever, and manages requests from the IoT Edge for setting values back into EBO.")]
    public class RequestReceiveProcessor : EboIotEdgeConnectorProcessorWithMqttBase, ILongRunningProcess
    {
        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            //RegistryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(IotHubSettings.AzureConnectionString);

            //var deviceMappings = ProcessorValueSource.Items.Where(a => a.Key == "AzureDeviceMapping" && a.Group == IotHubSettings.IotHubName).ToList();

            //if (!deviceMappings.Any())
            //{
            //    Logger.LogInfo(LogCategory.Processor, this.Name, "There are no mapped devices, please run this processor again when devices have been mapped (hint: run EboIotEdgeConnector.SetupProcessor)");
            //    Prompts.Add(new Prompt {Message = "There are no mapped devices, please run this processor again when devices have been mapped (hint: run EboIotEdgeConnector.SetupProcessor)", Severity = PromptSeverity.MayNotContinue});
            //    return Prompts;
            //}

            //foreach (var device in deviceMappings)
            //{
            //    var azureDevice = AddDeviceAsync(device.Value).Result;
            //    var accessKey = azureDevice.Authentication.SymmetricKey.PrimaryKey;
            //    var deviceConnectionString = $"{IotHubSettings.AzureConnectionString.Split(';')[0]};DeviceId={device.Value};SharedAccessKey={accessKey}";
            //    Logger.LogTrace(LogCategory.Processor, this.Name, $"Connecting to IoT Hub for device {deviceConnectionString}");
            //    var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);
            //    Task.Run(() => ReceiveCloudToDeviceAsync(deviceClient, device.Value));
            //}

            StartMqttClient().Wait();

            for (;;)
            {
                if (IsCancellationRequested) return Prompts;
            }
        }
        #endregion

        #region HandleMqttApplicationMessageReceived - Override
        public override void HandleMqttApplicationMessageReceived(string topic, string decodedMessage)
        {
            if (topic == "eboiotedgeconnector/writevalue")
            {
                HandleWriteToEboFromIotEdge(decodedMessage);
            }
        }
        #endregion
        #region SubscribeToMqttTopics - Override
        public override async void SubscribeToMqttTopics()
        {
            await ManagedMqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"eboiotedgeconnector/writevalue").WithAtLeastOnceQoS().Build());
        }
        #endregion

        #region HandleWriteToEboFromIotEdge
        private void HandleWriteToEboFromIotEdge(string message)
        {
            // TODO: Implement this once schema is known
        } 
        #endregion
    }
}