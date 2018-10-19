using System;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public class Observation
    {
        #region ObservationTime
        [JsonProperty("observationTime")]
        public DateTimeOffset ObservationTime { get; set; }
        #endregion
        #region Value
        [JsonProperty("value")]
        public dynamic Value { get; set; }
        #endregion
        #region SensorId
        [JsonProperty("sensorId")]
        public string SensorId { get; set; } 
        #endregion
    }
}
