using System;
using System.Collections.Generic;
using System.Linq;
using Mongoose.Common;
using Mongoose.Test;
using Mongoose.Test.Processors;
using NUnit.Framework;
using SxL.Common;
using SmartConnector = Mongoose.Service.Mongoose;

namespace EboIotEdgeConnector.Extension.Test
{
    public class ValuePushProcessorFixture : SmartConnectorTestFixtureBase, IProcessorTestFixture<ValuePushProcessor>
    {
        #region FixtureOneTimeSetup_Base - Override
        protected override void FixtureOneTimeSetup_Base()
        {
            try
            {
                SmartConnector.InitIoC();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.Testing, ex);
                throw new NotImplementedException();
            }
        } 
        #endregion
        #region CreateTestableProcessor - IProcessorTestFixture Member
        public ValuePushProcessor CreateTestableProcessor()
        {
            var processor = this.CreateProccessorInstanceWithDefaultValues();
            processor.EboEwsSettings = new EboEwsSettings
            {
                Address = "http://localhost:8020/EcoStruxure/DataExchange",
                UserName = "admin",
                Password = "Admin!23"
            };
            processor.MqttBrokerSettings = new MqttBroker
            {
                BrokerAddress = "127.0.0.1",
                IsEncryptedCommunication = false,
                Port = 1883
            };
            processor.MqttClientId = "ValuePusher";
            var cache = MongooseObjectFactory.Current.GetInstance<ICache>();
            var signals = new List<Signal> {
                new Signal
                {
                    DatabasePath = "/Server 1/Fake Air Handler 1",
                    DeviceName = "DeviceTest1",
                    ItemId = "AV1",
                    SendTime = 600
                },
                new Signal
                {
                    DatabasePath = "/Server 1/Fake Air Handler 1",
                    DeviceName = "DeviceTest1",
                    ItemId = "AV5",
                    SendTime = 600
                },
                new Signal
                {
                    DatabasePath = "/Server 1/Fake Air Handler 1",
                    DeviceName = "DeviceTest1",
                    ItemId = "AV15",
                    SendTime = 600
                },
                new Signal
                {
                    DatabasePath = "/Server 1/Fake Air Handler 2",
                    DeviceName = "DeviceTest2",
                    ItemId = "AV1",
                    SendTime = 600
                },
                new Signal
                {
                    DatabasePath = "/Server 1/Fake Air Handler 2",
                    DeviceName = "DeviceTest2",
                    ItemId = "AV2",
                    SendTime = 600
                }
            };
            cache.AddOrUpdateItem(signals, "CurrentSignalValues", processor.CacheTenantId, 0);
            return processor;
        } 
        #endregion
        #region ValidateTest - IProcessorTestFixture Member
        [Test]
        public void ValidateTest()
        {
            throw new NotImplementedException();
        } 
        #endregion
        #region CancelTest - IProcessorTestFixture Member
        [Test]
        public void CancelTest()
        {
            this.RunCancelTest();
        } 
        #endregion
        #region ExecuteTest - IProcessorTestFixture Member
        [Test]
        public void ExecuteTest()
        {
            this.RunExecuteTest();
        } 
        #endregion
    }
}