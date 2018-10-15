using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Ews.Client;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Common.Data;
using Mongoose.Process;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Setup Processor", "This processor parses the signal CSV file, and stores the result in the in-memory cache for use by the other processors.")]
    public class SetupProcessor : EboIotEdgeConnectorProcessorBase
    {
        #region SignalFileLocation
        [Required]
        public string SignalFileLocation { get; set; }
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            var signals = SignalFileParser.Parse(SignalFileLocation);

            GetAndUpdateUnitsForSignals(signals);

            Cache.AddOrUpdateItem(signals, "CurrentSignalValues", CacheTenantId, 0);

            //RegistryManager = RegistryManager.CreateFromConnectionString(IotHubSettings.AzureConnectionString);

            //CreateAndMapDevices(devices);

            //RegistryManager.Dispose();
            return Prompts;
        }
        #endregion

        #region GetAndUpdateUnitsForSignals
        private void GetAndUpdateUnitsForSignals(List<Signal> signals)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, "Getting units for all signals..");
            while (signals.Any())
            {
                try
                {
                    var response = ManagedEwsClient.GetItems(EboEwsSettings, signals.Take(500).Select(a => a.EwsId).ToArray());
                    var successfulValues = response.GetItemsItems.ValueItems.ToList();

                    foreach (var value in successfulValues)
                    {
                        var deviceSignal = signals.FirstOrDefault(a => a.EwsId == value.Id);
                        if (deviceSignal == null)
                        {
                            Logger.LogInfo(LogCategory.Processor, this.Name, $"Returned value does not exist in the list known signals..");
                        }
                        else
                        {
                            deviceSignal.Unit = value.Unit;
                        }
                    }

                    foreach (var value in response.GetItemsErrorResults.ToList())
                    {
                        Logger.LogInfo(LogCategory.Processor, this.Name, $"Error getting value: {value.Id} - {value.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                    Prompts.Add(ex.ToPrompt());
                    // We will let it continue and see if everything else fails... Maybe some will work..
                }

                signals = signals.Skip(500).ToList();
            }
        }
        #endregion

        //#region CreateAndMapDevices
        //private void CreateAndMapDevices(IEnumerable<IGrouping<string, Signal>> devices)
        //{
        //    Logger.LogTrace(LogCategory.Processor, this.Name, $"Creating devices in the Azure IoT Hub and mapping to the corresponding device in the CSV file");
        //    foreach (var device in devices)
        //    {
        //        var processorValue = this.FindOrCreateProcessorValue("EboIotEdgeConnectorDeviceMapping", CacheTenantId, device.Key);
        //        string deviceId;
        //        var isNewMapping = false;
        //        if (string.IsNullOrEmpty(processorValue.Value))
        //        {
        //            deviceId = Guid.NewGuid().ToString();
        //            deviceId = $"se-{deviceId}";
        //            processorValue.Value = deviceId;
        //            isNewMapping = true;
        //        }
        //        else
        //        {
        //            deviceId = processorValue.Value;
        //        }

        //        try
        //        {
        //            var iotDevice = AddDeviceAsync(deviceId).Result;
        //            if (isNewMapping) ProcessorValueSource.Save();
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
        //            Prompts.Add(ex.ToPrompt());
        //        }
        //    }
        //} 
        //#endregion
    }
}