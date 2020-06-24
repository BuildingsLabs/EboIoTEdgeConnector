using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mongoose.Common;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using SxL.Common;

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

        #region RetainFilePath
        private string _retainFilePath;
        private string RetainFilePath
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(_retainFilePath))
                    {
                        var filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}{Filename}";
                        var file = new FileInfo(filePath);
                        file.Directory.Create();
                        _retainFilePath = filePath;
                    }
                    return _retainFilePath;
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, ex.ToString());
                    return null;
                }
            }
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
            File.WriteAllText(RetainFilePath, JsonConvert.SerializeObject(messages));
            return Task.FromResult(0);
        }
        #endregion
        #region LoadQueuedMessagesAsync - IManagedMqttClientStorage Member
        public Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
        {
            IList<ManagedMqttApplicationMessage> retainedMessages;
            if (File.Exists(RetainFilePath))
            {
                var json = File.ReadAllText(RetainFilePath);
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
