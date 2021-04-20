using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Ews.Client;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using Newtonsoft.Json;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public abstract class EboIotEdgeConnectorProcessorBase : Processor
    {
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
                        var signals = Cache.RetrieveItem<List<Signal>>("CurrentSignalValues", tenantId: CacheTenantId);
                        if (signals == null)
                        {
                            signals = new List<Signal>();
                            // Let's see if we saved any items in the ProcessorValues
                            var pvs = ProcessorValueSource.Items.Where(a => a.Key == "Signals" && a.Group == CacheTenantId);
                            var pvSignals = new List<Signal>();
                            foreach (var pv in pvs)
                            {
                                Logger.LogTrace(LogCategory.Processor, this.Name, "Seeding Signal Cache from Processor Values");
                                try
                                {
                                    pvSignals.AddRange(JsonConvert.DeserializeObject<List<Signal>>(pv.Value));
                                }
                                catch (Exception ex)
                                {
                                    ProcessorValueSource.Delete(pv);
                                    ProcessorValueSource.Save();
                                    // Not a big deal if these don't exist, as they will just be discovered again with the SetupProcessor
                                    Logger.LogInfo(LogCategory.Processor, this.Name, "Error when getting saved list of signals from processor value source. Objects will be rediscovered.", ex);
                                }
                                if (pvSignals.Any()) Cache.AddOrUpdateItem(pvSignals, "CurrentSignalValues", CacheTenantId, 0);
                                _signals = pvSignals;
                            }
                        }

                        _signals = signals;
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
        #region Prompts
        [ConfigurationIgnore]
        protected List<Prompt> Prompts { get; set; }
        #endregion
        #region IsLicensed
        #if DEBUG
        public override bool IsLicensed => false;
        #endif
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
            Prompts = new List<Prompt>();
        } 
        #endregion
    }
}