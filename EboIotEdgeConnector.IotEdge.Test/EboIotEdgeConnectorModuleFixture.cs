using NUnit.Framework;

namespace EboIotEdgeConnector.IotEdge.Test
{
    [TestFixture]
    public class EboIotEdgeConnectorModuleFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            
        }

        [Test]
        public void ConnectorModuleTest()
        {
            var module = new EboIotEdgeConnectorModule();
            Assert.AreEqual(1, 1);
        }    
    }
}