using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Devices.Gateway;

namespace EboIotEdgeConnector.IotEdge
{
    public class EboIotEdgeConnectorModule : IGatewayModule
    {
        public void Create(Broker broker, byte[] configuration)
        {
            throw new NotImplementedException();
        }

        public void Destroy()
        {
            throw new NotImplementedException();
        }

        public void Receive(Message received_message)
        {
            throw new NotImplementedException();
        }
    }
}
