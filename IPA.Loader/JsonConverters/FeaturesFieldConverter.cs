using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.JsonConverters
{
    internal class FeaturesFieldConverter : JsonConverter<Dictionary<string, JObject>>
    {
        public override Dictionary<string, JObject> ReadJson(JsonReader reader, Type objectType, Dictionary<string, JObject> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                _ = serializer.Deserialize<string[]>(reader);
                Logger.features.Warn("Encountered old features used. They no longer do anything, please move to the new format.");
                return existingValue;
            }

            return serializer.Deserialize<Dictionary<string, JObject>>(reader);
        }

        public override void WriteJson(JsonWriter writer, Dictionary<string, JObject> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
