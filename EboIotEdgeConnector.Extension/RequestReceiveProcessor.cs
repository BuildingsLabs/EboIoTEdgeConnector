using System.Collections.Generic;
using System.Threading.Tasks;
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
            StartMqttClient().Wait();

            for (;;)
            {
                if (IsCancellationRequested) return Prompts;
                Task.Delay(5000).Wait();
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