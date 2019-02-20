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
        #region Writeable
        [JsonProperty("writeable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Writeable { get; set; }
        #endregion
        #region Forceable
        [JsonProperty("forceable", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Forceable { get; set; }
        #endregion
    }
}