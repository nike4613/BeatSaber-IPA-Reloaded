using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.JsonConverters
{
    internal class MultilineStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var list = serializer.Deserialize<string[]>(reader);
                return string.Join("\n", list);
            }
            else
                return reader.Value as string;
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            var list = value.Split('\n');
            if (list.Length == 1)
                serializer.Serialize(writer, value);
            else
                serializer.Serialize(writer, list);
        }
    }
}
