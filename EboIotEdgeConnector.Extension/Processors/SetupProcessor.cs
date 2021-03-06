﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Ews.Client;
using Ews.Common;
using Mongoose.Common;
using Mongoose.Common.Attributes;
using Mongoose.Common.Data;
using Mongoose.Process;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    [ConfigurationDefaults("Setup Processor", "This processor parses the signal CSV file, and stores the result in the in-memory cache for use by the other processors.")]
    public class SetupProcessor : EboIotEdgeConnectorProcessorBase, ILongRunningProcess
    {
        private List<Signal> _signalsToUse;

        #region SignalFileLocation
        [Required]
        public string SignalFileLocation { get; set; }
        #endregion

        #region Execute_Subclass - Override
        protected override IEnumerable<Prompt> Execute_Subclass()
        {
            ResetSubscriptionCache();
            var signals = SignalFileParser.Parse(SignalFileLocation);

            try
            {
                if (!EvaluatePerformanceImpact(signals))
                {
                    Prompts.Add(new Prompt
                    {
                        Message = "Could not successfully evaluate performance, cannot continue.",
                        Severity = PromptSeverity.MayNotContinue
                    });
                    return Prompts;
                }

                GetAndUpdateInitialPropertiesForSignals(_signalsToUse);
            }
            catch (Exception ex)
            {
                Prompts.Add(ex.ToPrompt());
                return Prompts;
            }

            return Prompts;
        }
        #endregion

        #region ResetSubscriptionCache
        /// <summary>
        /// Resets the in-memory cache, so that old subscriptions don't continue to be read
        /// </summary>
        private void ResetSubscriptionCache()
        {
            var cacheKeys = Cache.Keys(CacheTenantId);
            foreach (var key in cacheKeys.Where(a => a.StartsWith("ActiveSubscriptions")))
            {
                Cache.DeleteItem(key, CacheTenantId);
            }
        } 
        #endregion
        #region GetAndUpdateUnitsForSignals
        private void GetAndUpdateInitialPropertiesForSignals(List<Signal> signals)
        {
            var newSignals = new List<Signal>();
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Getting units for all signals.. {signals.Count} total.");
            // Get list of signals that have not been previously discovered
            var newSignalsLeftToAdd = signals.Where(a => !Signals.Select(b => b.EwsId).Contains(a.EwsId)).ToList();
            Logger.LogTrace(LogCategory.Processor, this.Name, $"Found, {newSignalsLeftToAdd.Count} total to add.");
            while (newSignalsLeftToAdd.Any())
            {
                Logger.LogTrace(LogCategory.Processor, this.Name, $"Found, {newSignalsLeftToAdd.Count} left to add.");
                if (IsCancellationRequested) return;
                try
                {
                    var response = ManagedEwsClient.GetItems(EboEwsSettings, newSignalsLeftToAdd.Take(100).Select(a => a.EwsId).ToArray());
                    var successfulValues = response.GetItemsItems.ValueItems?.ToList();
                    AddSuccessfulSignalsToCache(signals, successfulValues, newSignals);

                    // The below code is a workaround for EBO timing out on GetItems for many values, and returning either an INVALID_ID or TIMEOUT error when the values are in fact valid. We need to make sure we discover as many as possible, so let's loop until we do.
                    // We will do this as long as at least 1 value from the request was successful.
                    while (successfulValues != null && successfulValues.Any() && response.GetItemsErrorResults != null && response.GetItemsErrorResults.Any(a => a.Message == "INVALID_ID" || a.Message == "TIMEOUT"))
                    {
                        response = ManagedEwsClient.GetItems(EboEwsSettings, response.GetItemsErrorResults.Where(a => a.Message == "INVALID_ID" || a.Message == "TIMEOUT").Select(a => a.Id).ToArray());
                        successfulValues = response.GetItemsItems.ValueItems?.ToList();
                        AddSuccessfulSignalsToCache(signals, successfulValues, newSignals);
                    }

                    var valuesToRetry = UpdateInvalidEwsIdsForRetry(signals, response);

                    response = ManagedEwsClient.GetItems(EboEwsSettings, valuesToRetry.Select(a => a.EwsId).ToArray());
                    successfulValues = response.GetItemsItems.ValueItems?.ToList();
                    AddSuccessfulSignalsToCache(signals, successfulValues, newSignals);

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

                newSignalsLeftToAdd = newSignalsLeftToAdd.Skip(100).ToList();
            }

            signals.AddRange(newSignals);
            Signals = signals;

            SeedProcessorValues(signals);
        }
        #endregion
        #region EvaluatePerformanceImpact
        /// <summary>
        /// Due to some performance issues with version of EBO prior to 2.0, we will limit the amount of points that can be consumed out of EBO versions
        /// prior to 2.0 to 100. No limit for point count in version 2.0 and above.
        /// </summary>
        private bool EvaluatePerformanceImpact(List<Signal> signals)
        {
            try
            {
                // We save this right away so we can execute this setup processor from the value push processor if it is not running
                Cache.AddOrUpdateItem(this.ConfigurationId, "SetupProcessorConfigurationId", this.CacheTenantId, 0);
                var response = ManagedEwsClient.GetWebServiceInformation(EboEwsSettings);
                var eboVersion = new Version(response.GetWebServiceInformationSystem.Version);
                if (eboVersion.Major > 1)
                {
                    _signalsToUse = signals;
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

                    _signalsToUse = signals.Take(100).ToList();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(LogCategory.Processor, this.Name, ex.ToString());
                return false;
            }     
        }
        #endregion
        #region UpdateInvalidEwsIdsForRetry
        private List<Signal> UpdateInvalidEwsIdsForRetry(List<Signal> signals, GetItemsResponse response)
        {
            var valuesToRetry = new List<Signal>();
            // We don't know if these are 01 EWS IDs or 11.. So for all invalid IDs, we must now try to 11 to see what happens.
            if (response.GetItemsErrorResults != null && response.GetItemsErrorResults.ToList().Any())
            {
                foreach (var value in response.GetItemsErrorResults.ToList().Where(a => a.Message == "INVALID_ID"))
                {
                    var deviceSignal = signals.FirstOrDefault(a => a.EwsId == value.Id);
                    if (deviceSignal == null)
                    {
                        Logger.LogInfo(LogCategory.Processor, this.Name, $"Returned value does not exist in the list known signals..");
                    }
                    else
                    {
                        deviceSignal.EwsId = $"11{deviceSignal.DatabasePath}";
                    }
                    valuesToRetry.Add(deviceSignal);
                }
            }
            return valuesToRetry;
        }
        #endregion
        #region AddSuccessfulSignalsToCache
        private void AddSuccessfulSignalsToCache(List<Signal> signals, List<ValueItemType> successfulValues, List<Signal> newSignals)
        {
            foreach (var value in successfulValues)
            {
                var deviceSignal = signals.FirstOrDefault(a => a.EwsId == value.Id);
                if (deviceSignal == null)
                {
                    Logger.LogInfo(LogCategory.Processor, this.Name, $"Returned value does not exist in the list known signals..");
                }
                else
                {
                    var tryParse = Enum.TryParse(value.Type, true, out EwsValueTypeEnum type);
                    if (!tryParse) Logger.LogInfo(LogCategory.Processor, this.Name, $"{value.Type} could not be parsed into a valid EwsValueTypeEnum, default of 'DateTime' will be used for ID {value.Id}.");
                    tryParse = Enum.TryParse(value.Writeable, true, out EwsValueWriteableEnum writeable);
                    if (!tryParse) Logger.LogInfo(LogCategory.Processor, this.Name, $"{value.Writeable} could not be parsed into a valid EwsValueWriteableEnum, default of 'ReadOnly' will be used for ID {value.Id}.");
                    tryParse = Enum.TryParse(value.Forceable, true, out EwsValueForceableEnum forceable);
                    if (!tryParse) Logger.LogInfo(LogCategory.Processor, this.Name, $"{value.Forceable} could not be parsed into a valid EwsValueForceableEnum, default of 'NotForceable' will be used for ID {value.Id}.");
                    deviceSignal.Type = type;
                    deviceSignal.Unit = value.Unit;
                    deviceSignal.Writeable = writeable;
                    deviceSignal.Forceable = forceable;
                    newSignals.Add(deviceSignal);
                }
            }
        }
        #endregion

        #region SeedProcessorValues
        /// <summary>
        /// Seeds processor values with all the discovered Signals, this is to improve performance on startup if these have already been discovered
        /// </summary>
        private void SeedProcessorValues(List<Signal> signals)
        {
            ProcessorValueSource.Delete(ProcessorValueSource.Items.Where(a => a.Key == "Signals" && a.Group == CacheTenantId).ToList());
            int i = 1;
            while (signals.Any())
            {
                var pv = this.FindOrCreateProcessorValue("Signals", CacheTenantId, i.ToString());
                pv.Value = signals.Take(500).ToJSON();
                signals = signals.Skip(500).ToList();
                i++;
            }

            ProcessorValueSource.Save();
        }
        #endregion
    }
}