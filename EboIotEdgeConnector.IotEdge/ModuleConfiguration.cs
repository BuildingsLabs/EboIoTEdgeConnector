using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EboIotEdgeConnector.IotEdge
{
    public partial class ModuleConfiguration
    {
        #region MqttBrokerAdress
        [JsonProperty("MqttBrokerAdress")]
        public string MqttBrokerAdress { get; set; }
        #endregion
        #region MqttBrokerPort
        [JsonProperty("MqttBrokerPort")]
        public int MqttBrokerPort { get; set; }
        #endregion
        #region MyRegion
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
    }
    public partial class ModuleConfiguration
    {
        public static ModuleConfiguration FromJson(string json) => JsonConvert.DeserializeObject<ModuleConfiguration>(json, EboIotEdgeConnector.IotEdge.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this ModuleConfiguration self) => JsonConvert.SerializeObject(self, EboIotEdgeConnector.IotEdge.Converter.Settings);
        public static string ToJson(this MqttValueWrite self) => JsonConvert.SerializeObject(self, EboIotEdgeConnector.IotEdge.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}