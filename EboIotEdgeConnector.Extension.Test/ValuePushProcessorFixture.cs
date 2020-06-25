using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ews.Common;
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
                MongooseObjectFactory.ConfigureDataDirectory();
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

            //var cache = MongooseObjectFactory.Current.GetInstance<ICache>();
            //cache.AddOrUpdateItem(46, "SetupProcessorConfigurationId", tenantId: processor.CacheTenantId);
            //if (cache.RetrieveItem("CurrentSignalValues", processor.CacheTenantId) == null)
            //{
            //    var signals = new List<Signal>
            //    {
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/AV1",
            //            PointName = "AV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            SendOnUpdate = true,
            //            Writeable = EwsValueWriteableEnum.ReadOnly,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/AV2",
            //            PointName = "AV2",
            //            SendTime = 30,
            //            Type = EwsValueTypeEnum.Double,
            //            SendOnUpdate = false,
            //            Writeable = EwsValueWriteableEnum.ReadOnly,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/AV5",
            //            PointName = "AV5",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/AV15",
            //            PointName = "AV15",
            //            SendTime = 8,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/BV1",
            //            PointName = "BV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Boolean,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/IV1",
            //            PointName = "IV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Long,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/SV1",
            //            PointName = "SV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.String,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/DTV1",
            //            PointName = "DTV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.DateTime,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 2/AV1",
            //            PointName = "AV1",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 2/AV2",
            //            PointName = "AV2",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 2/AVss2",
            //            PointName = "AV2",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //        new Signal
            //        {
            //            DatabasePath = "/Server 1/Fake Air Handler 1/MANÖVER",
            //            PointName = "MANÖVER",
            //            SendTime = 600,
            //            Type = EwsValueTypeEnum.Double,
            //            Writeable = EwsValueWriteableEnum.Writeable,
            //            Forceable = EwsValueForceableEnum.Forceable
            //        },
            //    };
                //cache.AddOrUpdateItem(signals, "CurrentSignalValues", processor.CacheTenantId, 0);
            //}
            
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
            try
            {
                new SetupProcessorFixture().RunExecuteTest();
            }
            catch (Exception ex)
            {
                // This is gonna fail, just let it continue.
            }
            this.RunExecuteTest();
            //Task.Delay(5000).Wait();
            //this.RunExecuteTest();
            //Task.Delay(5000).Wait();
            //this.RunExecuteTest();
        } 
        #endregion
    }
}