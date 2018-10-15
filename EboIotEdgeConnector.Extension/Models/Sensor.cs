using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public class Sensor
    {
        #region Type
        [JsonProperty("Type")]
        public string Type { get; set; } 
        #endregion
        #region Path
        [JsonProperty("Path")]
        public string Path { get; set; } 
        #endregion
        #region Value
        [JsonProperty("Value")]
        public string Value { get; set; }
        #endregion
        #region Unit
        [JsonProperty("Unit")]
        public string Unit { get; set; } 
        #endregion
    }
}
