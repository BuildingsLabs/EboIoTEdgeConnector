using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EboIotEdgeConnector.Common
{
    class MqttCredentials
    {
        #region ClientId
        public string ClientId { get; set; }
        #endregion
        #region UserName
        public string UserName { get; set; }
        #endregion
        #region Password
        public string Password { get; set; }
        #endregion
    }
}
