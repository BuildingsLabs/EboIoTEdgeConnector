using System.Collections.Generic;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public partial class MqttValueWrite
    {
        public List<ValueTypeStateless> ValuesToWrite { get; set; }
    }

    public class ValueTypeStateless
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }

    public partial class MqttValueWrite
    {
        public static MqttValueWrite FromJson(string json) => JsonConvert.DeserializeObject<MqttValueWrite>(json, EboIotEdgeConnector.IotEdge.Converter.Settings);
    }
}
