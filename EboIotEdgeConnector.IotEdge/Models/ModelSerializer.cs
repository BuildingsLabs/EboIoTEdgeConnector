using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public static class ModelSerializer
    {
        #region ToJson - ModuleConfiguration
        public static string ToJson(this ModuleConfiguration self) => JsonConvert.SerializeObject(self, JsonConverter.Settings);
        #endregion
        #region ToJson - IotEdgeMessage
        public static string ToJson(this IotEdgeMessage self) => JsonConvert.SerializeObject(self, JsonConverter.Settings);
        #endregion
    }
}
