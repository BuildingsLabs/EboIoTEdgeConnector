using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EboIotEdgeConnector.Extension
{
    internal static class JsonConverter
    {
        #region Settings
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal, DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'" }
            },
        };
        #endregion
    }
}