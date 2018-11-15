using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using Mongoose.Common;
using SxL.Common;

namespace EboIotEdgeConnector.Extension
{
    public class SignalFileParser
    {
        #region Parse
        public static List<Signal> Parse(string signalFileLocation)
        {
            var signals = new List<Signal>();
            using (var parser = new TextFieldParser(signalFileLocation))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(";");
                parser.CommentTokens = new[] { ";;;;;;" };
                var isFirstLine = true;
                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();
                    // Let's skip the header line. We don't want this to be a signal..
                    if (isFirstLine)
                    {
                        isFirstLine = false;
                        continue;
                    }

                    if (fields == null || !fields.Any())
                    {
                        Logger.LogInfo(LogCategory.Processor, "SignalFileParser", "CSV File line was empty.. continuing...");
                        continue;
                    }

                    ConvertRowToSignal(fields, signals);
                }
            }

            return signals;
        }
        #endregion
        #region ConvertRowToSignal
        private static void ConvertRowToSignal(string[] fields, List<Signal> signals)
        {
            try
            {
                if (!bool.TryParse(fields[6], out var sendOnUpdate))
                {
                    Logger.LogInfo(LogCategory.Processor, "SignalFileParser",
                        "'SendOnUpdate' is not a valid boolean, default to false..");
                }

                if (!int.TryParse(fields[7], out int sendTime))
                {
                    Logger.LogInfo(LogCategory.Processor, "SignalFileParser",
                        "'SendTime' is not a valid integer, default to false..");
                    sendTime = 600;
                }

                signals.Add(new Signal
                {
                    PointName = fields[1],
                    DatabasePath = fields[2],
                    SendOnUpdate = sendOnUpdate,
                    SendTime = sendTime
                });
            }
            catch (Exception e)
            {
                Logger.LogInfo(LogCategory.Processor, "SignalFileParser",
                    $"Failed to parse line in CSV file: {string.Join(";", fields)}");
            }
        } 
        #endregion
    }
}