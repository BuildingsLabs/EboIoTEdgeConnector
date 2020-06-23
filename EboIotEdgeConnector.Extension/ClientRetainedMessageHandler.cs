using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public class ClientRetainedMessageHandler : IManagedMqttClientStorage
    {
        #region FileName
        private string _fileName;
        public string Filename
        {
            get => _fileName;
            set => _fileName = $"\\SmartConnector\\Extensions\\EboIotEdgeConnector\\{value}-RetainedMqttMessages.json";
        } 
        #endregion

        #region Constructor
        public ClientRetainedMessageHandler(string processorName)
        {
            Filename = processorName;
        } 
        #endregion

        #region SaveQueuedMessagesAsync - IManagedMqttClientStorage Member
        public Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages)
        {
            File.WriteAllText(Filename, JsonConvert.SerializeObject(messages));
            return Task.FromResult(0);
        }
        #endregion
        #region LoadQueuedMessagesAsync - IManagedMqttClientStorage Member
        public Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
        {
            IList<ManagedMqttApplicationMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = JsonConvert.DeserializeObject<List<ManagedMqttApplicationMessage>>(json);
            }
            else
            {
                retainedMessages = new List<ManagedMqttApplicationMessage>();
            }

            return Task.FromResult(retainedMessages);
        } 
        #endregion
    }
}
