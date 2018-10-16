using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mongoose.Common;
using MQTTnet;
using MQTTnet.Server;
using Newtonsoft.Json;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public class RetainedMqttMessageHandler : IMqttServerStorage
    {
        private const string Filename = "\\SmartConnector\\Extensions\\EboIotEdgeConnector\\RetainedMqttMessages.json";

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

        #region SaveRetainedMessagesAsync - IMqttServerStorage Member
        public Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            File.WriteAllText(RetainFilePath, JsonConvert.SerializeObject(messages));
            return Task.FromResult(0);
        }
        #endregion
        #region LoadRetainedMessagesAsync - IMqttServerStorage Member
        public Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            IList<MqttApplicationMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}{Filename}");
                retainedMessages = JsonConvert.DeserializeObject<List<MqttApplicationMessage>>(json);
            }
            else
            {
                retainedMessages = new List<MqttApplicationMessage>();
            }

            return Task.FromResult(retainedMessages);
        } 
        #endregion
    }
}