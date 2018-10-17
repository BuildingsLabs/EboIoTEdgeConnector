using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Ews.Client;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public abstract class EboIotEdgeConnectorProcessorBase : Processor
    {
        internal List<Prompt> Prompts = new List<Prompt>();
        internal IManagedEwsClient ManagedEwsClient = MongooseObjectFactory.Current.GetInstance<IManagedEwsClient>();

        #region Signals
        private List<Signal> _signals;
        [ConfigurationIgnore]
        internal List<Signal> Signals
        {
            get
            {
                try
                {
                    if (_signals == null)
                    {
                        var signals = Cache.RetrieveItem("CurrentSignalValues", tenantId: CacheTenantId);
                        if (signals == null || !(signals is List<Signal>)) return null;
                        _signals = (List<Signal>)signals;
                    }
                    return _signals;
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                    return null;
                }

            }
            set
            {
                Cache.AddOrUpdateItem(value, "CurrentSignalValues", CacheTenantId, 0);
                _signals = value;
            }

        }

        #endregion

        #region IsLicensed
        public override bool IsLicensed
        {
            get
            {
            #if DEBUG
                return false;
            #else
                return true;
            #endif
            }
        }
        #endregion
        #region EboEwsSettings
        [Tooltip("The settings that specify the EWS endpoint and credentials required to communicate with EBO.")]
        public EboEwsSettings EboEwsSettings { get; set; }
        #endregion
        #region CacheTenantId
        [Required, DefaultValue("DefaultValueForEboIotEdgeConnectorExtensionCacheTenantId"), Tooltip("The cache tenant ID specifies the container that all the processors working together need in order to share data between each other. This needs to be the same for the SetupProcessor, ValuePushProcessor, and RequestReceiveProcessor")]
        public string CacheTenantId { get; set; } 
        #endregion

        #region Constructor
        protected EboIotEdgeConnectorProcessorBase()
        {
            EboEwsSettings = new EboEwsSettings();
        }
        #endregion
    }
}