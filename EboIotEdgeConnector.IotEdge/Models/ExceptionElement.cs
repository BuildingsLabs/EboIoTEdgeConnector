using System;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class ExceptionElement
    {
        #region ExceptionTime
        [JsonProperty("exceptionTime")]
        public DateTimeOffset ExceptionTime { get; set; }
        #endregion
        #region SensorId
        [JsonProperty("sensorId")]
        public string SensorId { get; set; }
        #endregion
        #region Exception
        [JsonProperty("exception")]
        public string Exception { get; set; }
        #endregion
        #region Retry
        [JsonProperty("retry")]
        public string Retry { get; set; } 
        #endregion
    }
}