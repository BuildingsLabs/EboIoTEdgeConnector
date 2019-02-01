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

            try
            {
                EvaluatePerformanceImpact(signals);
            }
            catch (Exception ex)
            {
                Prompts.Add(ex.ToPrompt());
                return Prompts;
            }

            GetAndUpdateInitialPropertiesForSignals(Signals);

            return Prompts;
        }
        #endregion

        #region GetAndUpdateUnitsForSignals
        private void GetAndUpdateInitialPropertiesForSignals(List<Signal> signals)
        {
            var newSignals = new List<Signal>();
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
                            newSignals.Add(deviceSignal);
                        }
                    }

                    foreach (var value in response.GetItemsErrorResults.ToList())
                    {
                        Prompts.Add(new Prompt {Message = $"Error getting value, this value will not be pushed: {value.Id} - {value.Message}", Severity = PromptSeverity.MayNotContinue});
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

            Signals = newSignals;
        }
        #endregion
        #region EvaluatePerformanceImpact
        /// <summary>
        /// Due to some performance issues with version of EBO prior to 2.0, we will limit the amount of points that can be consumed out of EBO versions
        /// prior to 2.0 to 100. No limit for point count in version 2.0 and above.
        /// </summary>
        private void EvaluatePerformanceImpact(List<Signal> signals)
        {
            var eboVersion = new Version(ManagedEwsClient.GetWebServiceInformation(EboEwsSettings).GetWebServiceInformationSystem.Version);
            if (eboVersion.Major > 1)
            {
                Signals = signals;
            }
            else
            {
                if (signals.Count > 100)
                {
                    Prompts.Add(new Prompt
                    {
                        Severity = PromptSeverity.MayContinue,
                        Message = $"Due to performance concerns, only 100 points out of {signals.Count} can be consumed when using EBO versions prior to 2.0. Please update your EBO to version 2.0 or greater to get the full functionality of the EBO IoT Edge Smart Connector Extension."
                    });
                }

                Signals = signals.Take(100).ToList();
            }
        }
        #endregion
    }
}