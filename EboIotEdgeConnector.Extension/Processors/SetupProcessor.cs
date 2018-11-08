using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Process;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Setup Processor", "This processor parses the signal CSV file, and stores the result in the in-memory cache for use by the other processors.")]
    public class SetupProcessor : EboIotEdgeConnectorProcessorBase, ILongRunningProcess
    {
        #region SignalFileLocation
        [Required]
        public string SignalFileLocation { get; set; }
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            var signals = SignalFileParser.Parse(SignalFileLocation);
            GetAndUpdateInitialPropertiesForSignals(signals);
            Signals = signals;
            return Prompts;
        }
        #endregion

        #region GetAndUpdateUnitsForSignals
        private void GetAndUpdateInitialPropertiesForSignals(List<Signal> signals)
        {
            Logger.LogTrace(LogCategory.Processor, this.Name, "Getting units for all signals..");
            while (signals.Any())
            {
                if (IsCancellationRequested) return;
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
                            Enum.TryParse(value.Type, true, out EwsValueTypeEnum type);
                            Enum.TryParse(value.Writeable, true, out EwsValueWriteableEnum writeable);
                            Enum.TryParse(value.Forceable, true, out EwsValueForceableEnum forceable);
                            deviceSignal.Type = type;
                            deviceSignal.Unit = value.Unit;
                            deviceSignal.Writeable = writeable;
                            deviceSignal.Forceable = forceable;
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
    }
}