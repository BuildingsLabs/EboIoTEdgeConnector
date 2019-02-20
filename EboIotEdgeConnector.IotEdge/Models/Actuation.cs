using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class Actuation
    {
        #region ActuatorId
        [JsonProperty("actuatorId")]
        public string ActuatorId { get; set; }
        #endregion
        #region Value
        [JsonProperty("value")]
        public string Value { get; set; } 
        #endregion
    }
}