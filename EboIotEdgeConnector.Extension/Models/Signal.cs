using System;

namespace EboIotEdgeConnector.Extension
{
    public class Signal
    {
        #region DeviceName
        public string DeviceName { get; set; }
        #endregion
        #region ItemId
        public string ItemId { get; set; }
        #endregion
        #region DatabasePath
        public string DatabasePath { get; set; }
        #endregion
        #region Description
        public string Description { get; set; }
        #endregion
        #region Type
        public string Type { get; set; }
        #endregion
        #region Value
        public string Value { get; set; }
        #endregion
        #region Unit
        public string Unit { get; set; }
        #endregion
        #region SendOnUpdate
        public bool SendOnUpdate { get; set; }
        #endregion
        #region SendTime
        public int SendTime { get; set; }
        #endregion
        #region LastSendTime
        public DateTimeOffset? LastSendTime { get; set; }
        #endregion
        #region EwsId
        public string EwsId => $"01{DatabasePath}/{ItemId}"; 
        #endregion
    }
}