using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Gateway;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.IotEdge
{
    public class TestSendDataModule : IGatewayModule
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        #region Create - IGatewayModule Member
        public void Create(Broker broker, byte[] configuration)
        {
            Task.Run(() =>
            {
                for (; ; )
                {

                    var iotMessage = new IotEdgeMessage
                    {
                        Format = "rec2.3",
                        DeviceId = "/Server 1/Fake Air Handler 1",
                        Actuations = new List<Actuation>
                        {
                            new Actuation
                            {
                                ActuatorId = "AV1",
                                Value = "1337"
                            },
                            new Actuation
                            {
                                ActuatorId = "AV15",
                                Value = "1337"
                            },
                            new Actuation
                            {
                                ActuatorId = "AV1337",
                                Value = "1337"
                            }
                        }

                    };
              
                    var message = new Message(iotMessage.ToJson(), new Dictionary<string, string>());
                    Console.WriteLine($"Sending message to Gateway Broker: {JsonConvert.SerializeObject(message)}");
                    broker.Publish(message);

                    iotMessage = new IotEdgeMessage
                    {
                        Format = "rec2.3",
                        DeviceId = "/BigDataAS/IO Bus/DO-FA-12",
                        Actuations = new List<Actuation>
                        {
                            new Actuation
                            {
                                ActuatorId = "Digital Output",
                                Value = "1"
                            }
                        }
                    };

                    message = new Message(iotMessage.ToJson(), new Dictionary<string, string>());
                    Console.WriteLine($"Sending message to Gateway Broker: {JsonConvert.SerializeObject(message)}");
                    broker.Publish(message);
                    Task.Delay(10000).Wait();
                }
            }, _cts.Token);
        } 
        #endregion
        #region Destroy - IGatewayModule Member
        public void Destroy()
        {
            _cts.Cancel();
        }
        #endregion
        #region Receive - IGatewayModule Member
        public void Receive(Message received_message)
        {
            //Intentionally Empty
        } 
        #endregion
    }
}