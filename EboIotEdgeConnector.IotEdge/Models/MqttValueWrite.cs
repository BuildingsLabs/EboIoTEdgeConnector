using System.Collections.Generic;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class MqttValueWrite
    {
        #region ValuesToWrite
        public List<ValueTypeStateless> ValuesToWrite { get; set; }
        #endregion
        #region FromJson
        public static MqttValueWrite FromJson(string json) => JsonConvert.DeserializeObject<MqttValueWrite>(json, JsonConverter.Settings); 
        #endregion
    }
}
