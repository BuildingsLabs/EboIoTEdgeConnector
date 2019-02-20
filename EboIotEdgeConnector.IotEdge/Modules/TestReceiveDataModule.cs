using System;
using Microsoft.Azure.Devices.Gateway;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class TestReceiveDataModule : IGatewayModule
    {
        #region Create - IGatewayModule Member
        public void Create(Broker broker, byte[] configuration)
        {
            // Intentionally Empty
        }
        #endregion
        #region Destroy - IGatewayModule Member
        public void Destroy()
        {
            // Intentionally Empty
        }
        #endregion
        #region Receive - IGatewayModule Member
        public void Receive(Message receivedMessage)
        {
            Console.WriteLine($"Received Message from IoT Gateway Broker: {JsonConvert.SerializeObject(receivedMessage)}");
        } 
        #endregion
    }
}