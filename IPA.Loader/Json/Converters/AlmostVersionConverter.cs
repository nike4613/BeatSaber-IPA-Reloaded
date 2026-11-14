#nullable enable
using IPA.Utilities;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPA.Json.Converters
{
    internal sealed class AlmostVersionConverter : JsonConverter<AlmostVersion>
    {
        public override AlmostVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected a string, got {reader.TokenType}");
            }

            return new AlmostVersion(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, AlmostVersion value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
