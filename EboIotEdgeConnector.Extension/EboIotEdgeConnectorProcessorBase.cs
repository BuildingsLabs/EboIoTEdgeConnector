using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
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
        //internal RegistryManager RegistryManager;

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

        //#region AddDeviceAsync
        ///// <summary>
        ///// Adds a device to an Azure IoT Hub, and if it already exists, returns the existing.
        ///// </summary>
        ///// <param name="deviceId">The device ID of the device to be created in the Azure IoT Hub</param>
        //internal async Task<Device> AddDeviceAsync(string deviceId)
        //{
        //    Logger.LogTrace(LogCategory.Processor, this.Name, "Getting Azure IOT Device");
        //    Device device;
        //    try
        //    {
        //        device = await RegistryManager.AddDeviceAsync(new Device(deviceId));
        //    }
        //    catch (DeviceAlreadyExistsException)
        //    {
        //        device = await RegistryManager.GetDeviceAsync(deviceId);
        //    }
        //    Logger.LogTrace(LogCategory.Processor, this.Name, "Generated device key: {0}", device.Authentication.SymmetricKey.PrimaryKey);
        //    return device;
        //}
        //#endregion
    }
}
