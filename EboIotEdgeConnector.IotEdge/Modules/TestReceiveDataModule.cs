using System;
using Microsoft.Azure.Devices.Gateway;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class TestReceiveDataModule : IGatewayModule
    {
        public void Create(Broker broker, byte[] configuration)
        {
            //throw new NotImplementedException();
        }

        public void Destroy()
        {
            //throw new NotImplementedException();
        }

        public void Receive(Message receivedMessage)
        {
            Console.WriteLine($"Received Message from IoT Gateway Broker: {JsonConvert.SerializeObject(receivedMessage)}");
        }
    }
}
