using System.ComponentModel;

namespace EboIotEdgeConnector.IotEdge
{
    public class MqttBroker
    {
        #region BrokerAddress
        public string BrokerAddress { get; set; }
        #endregion
        #region Port
        public int Port { get; set; }
        #endregion
    }
}
