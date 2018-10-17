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
                    var valueToWrite = new MqttValueWrite
                    {
                        ValuesToWrite = new List<ValueTypeStateless>
                        {
                            new ValueTypeStateless
                            {
                                Id = "01/Server 1/Fake Air Handler 1/AV1",
                                Value = "1337"
                            },
                            new ValueTypeStateless
                            {
                                Id = "01/Server 1/Fake Air Handler 1/AV15",
                                Value = "1337"
                            },
                            new ValueTypeStateless
                            {
                                Id = "01/Server 1/Fake Air Handler 1/AV1337",
                                Value = "1337"
                            }
                        }
                    };
                    var message = new Message(valueToWrite.ToJson(), new Dictionary<string, string>());
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
