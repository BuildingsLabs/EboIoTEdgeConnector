using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public static class ModelSerializer
    {
        #region ToJson - IotEdgeMessage
        public static string ToJson(this IotEdgeMessage self) => JsonConvert.SerializeObject(self, JsonConverter.Settings); 
        #endregion
    }
}