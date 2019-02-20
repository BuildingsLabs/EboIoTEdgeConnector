using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Mongoose.Common.Attributes;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public class MqttBroker : ITraversable
    {
        #region BrokerAddress
        [Tooltip("The MQTT broker address."), DefaultValue("127.0.0.1"), Required]
        public string BrokerAddress { get; set; }
        #endregion
        #region Port
        [Tooltip("The port of the MQTT Broker"), DefaultValue(1883), Required]
        public int Port { get; set; }
        #endregion
        #region IsEncryptedCommunication
        [Tooltip("Sets if encrypted communication with the MQTT broker is used."), Required]
        public bool IsEncryptedCommunication { get; set; }
        #endregion
    }
}