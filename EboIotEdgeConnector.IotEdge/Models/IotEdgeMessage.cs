using System.Collections.Generic;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class IotEdgeMessage
    {
        #region Format
        [JsonProperty("format")]
        public string Format { get; set; }
        #endregion
        #region DeviceId
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }
        #endregion
        #region Observations
        [JsonProperty("observations")]
        public List<Observation> Observations { get; set; }
        #endregion
        #region Actuations
        [JsonProperty("actuations")]
        public List<Actuation> Actuations { get; set; }
        #endregion
        #region Exceptions
        [JsonProperty("exceptions")]
        public List<ExceptionElement> Exceptions { get; set; }
        #endregion

        #region FromJson
        public static IotEdgeMessage FromJson(string json) => JsonConvert.DeserializeObject<IotEdgeMessage>(json, JsonConverter.Settings); 
        #endregion
    }
}