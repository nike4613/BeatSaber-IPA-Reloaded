using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPA.JsonConverters
{
    internal class MultilineStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<string[]>(ref reader, options);
                return string.Join("\n", list);
            }

            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            var list = value.Split('\n');
            if (list.Length == 1)
                writer.WriteStringValue(value);
            else
                JsonSerializer.Serialize(writer, list, options);
        }
    }
}
