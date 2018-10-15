using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    public class DeviceData
    {
        #region Format
        [JsonProperty("format")]
        public string Format { get; set; }
        #endregion
        #region PowerSource
        [JsonProperty("powerSource")]
        public string PowerSource { get; set; }
        #endregion
        #region EventTime
        [JsonProperty("eventTime")]
        public DateTimeOffset EventTime { get; set; }
        #endregion
        #region DeviceId
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }
        #endregion
        #region RoomId
        [JsonProperty("roomId")]
        public string RoomId { get; set; }
        #endregion
        #region Sensors
        [JsonProperty("sensors")]
        public List<Sensor> Sensors { get; set; }
        #endregion

        #region FromJson
        public static DeviceData FromJson(string json) => JsonConvert.DeserializeObject<DeviceData>(json, EboIotEdgeConnector.Extension.Converter.Settings);
        #endregion
    }
}