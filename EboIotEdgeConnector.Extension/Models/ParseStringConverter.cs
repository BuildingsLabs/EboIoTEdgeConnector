using System;
using Newtonsoft.Json;

namespace EboIotEdgeConnector.Extension
{
    internal class ParseStringConverter : Newtonsoft.Json.JsonConverter
    {
        #region CanConvert - Override
        public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?); 
        #endregion
        #region ReadJson - Override
        public override object ReadJson(JsonReader reader, Type t, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            long l;
            if (Int64.TryParse(value, out l))
            {
                return l;
            }
            throw new Exception("Cannot unmarshal type long");
        } 
        #endregion
        #region WriteJson - Override
        public override void WriteJson(JsonWriter writer, object untypedValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (long)untypedValue;
            serializer.Serialize(writer, value.ToString());
            return;
        }
        #endregion
        #region Singleton
        public static readonly ParseStringConverter Singleton = new ParseStringConverter(); 
        #endregion
    }
}