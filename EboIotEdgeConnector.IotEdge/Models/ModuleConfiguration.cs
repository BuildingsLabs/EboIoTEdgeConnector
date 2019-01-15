using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class ModuleConfiguration
    {
        #region MqttBrokerAddress
        [JsonProperty("MqttBrokerAddress")]
        public string MqttBrokerAddress { get; set; }
        #endregion
        #region MqttBrokerPort
        [JsonProperty("MqttBrokerPort")]
        public int MqttBrokerPort { get; set; }
        #endregion
        #region MqttClientId
        [JsonProperty("MqttClientId")]
        public string MqttClientId { get; set; } 
        #endregion
        #region UseSecureCommunication
        [JsonProperty("UseSecureCommunication")]
        public bool UseSecureCommunication { get; set; }
        #endregion
        #region MqttBrokerUserName
        [JsonProperty("MqttBrokerUserName")]
        public string MqttBrokerUserName { get; set; }
        #endregion
        #region MqttBrokerPassword
        [JsonProperty("MqttBrokerPassword")]
        public string MqttBrokerPassword { get; set; }
        #endregion
        #region MqttValuePushTopic
        [JsonProperty("MqttValuePushTopic")]
        public string MqttValuePushTopic { get; set; }
        #endregion
        #region MqttValueSendTopic
        [JsonProperty("MqttValueSendTopic")]
        public string MqttValueSendTopic { get; set; }
        #endregion
        #region LoggingLevel
        [JsonProperty("LoggingLevel")]
        public string LoggingLevel { get; set; }
        #endregion
        #region ActuationSource
        [JsonProperty("ActuationSource")]
        public string ActuationSource { get; set; }
        #endregion

        #region FromJson
        public static ModuleConfiguration FromJson(string json) => JsonConvert.DeserializeObject<ModuleConfiguration>(json, JsonConverter.Settings);
        #endregion
    }
}