using System.Collections.Generic;
using Ews.Client;

namespace EboIotEdgeConnector.Extension
{
    public class MqttValueWrite
    {
        #region ValuesToWrite
        public List<ValueTypeStateless> ValuesToWrite { get; set; } 
        #endregion
    }
}
