using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public static class Serialize
    {
        #region ToJson
        public static string ToJson(this DeviceData self) => JsonConvert.SerializeObject(self, Converter.Settings); 
        #endregion
    }
}