using System;
using System.Reflection;
using Mongoose.Common;
using Mongoose.Test;
using Mongoose.Test.Processors;
using NUnit.Framework;
using SxL.Common;
using SmartConnector = Mongoose.Service.Mongoose;

namespace EboIotEdgeConnector.Extension.Test
{
    public class SetupProcessorFixture : SmartConnectorTestFixtureBase, IProcessorTestFixture<SetupProcessor>
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
        public SetupProcessor CreateTestableProcessor()
        {
            var processor = this.CreateProccessorInstanceWithDefaultValues();
            processor.SignalFileLocation = $"{Assembly.GetExecutingAssembly().Location.Replace("\\bin\\Debug\\EboIotEdgeConnector.Extension.Test.dll", string.Empty)}\\Files\\test_object_config.csv";
            processor.EboEwsSettings = new EboEwsSettings
            {
                Address = "http://localhost:8020/EcoStruxure/DataExchange",
                UserName = "admin",
                Password = "Admin!23"
            };
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