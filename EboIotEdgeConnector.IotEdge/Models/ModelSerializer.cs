using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public static class ModelSerializer
    {
        #region ToJson - ModuleConfiguration
        public static string ToJson(this ModuleConfiguration self) => JsonConvert.SerializeObject(self, JsonConverter.Settings);
        #endregion
        #region ToJson - MqttValueWrite
        public static string ToJson(this MqttValueWrite self) => JsonConvert.SerializeObject(self, JsonConverter.Settings); 
        #endregion
    }
}
