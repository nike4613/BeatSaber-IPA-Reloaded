#nullable enable
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPA.Json.Converters
{
    internal sealed class MultilineStringConverter : JsonConverter<string>
    {
        private static readonly char[] LineSeparator = ['\n'];

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString()!;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var stringBuilder = new StringBuilder();
                var isFirstLine = true;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException($"Expected a string, got {reader.TokenType}");
                    }

                    if (!isFirstLine)
                    {
                        stringBuilder.Append('\n');
                    }

                    isFirstLine = false;
                    stringBuilder.Append(reader.GetString()!);
                }

                return stringBuilder.ToString();
            }

            throw new JsonException($"Expected a string or an array, got {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            var lines = value.Split(LineSeparator);

            if (lines.Length == 1)
            {
                writer.WriteStringValue(value);
                return;
            }

            writer.WriteStartArray();

            foreach (var line in lines)
            {
                writer.WriteStringValue(line);
            }

            writer.WriteEndArray();
        }
    }
}
