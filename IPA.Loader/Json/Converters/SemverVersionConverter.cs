#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Version = Hive.Versioning.Version;

namespace IPA.Json.Converters
{
    internal sealed class SemverVersionConverter : JsonConverter<Version>
    {
        public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected a string, got {reader.TokenType}");
            }

            return new Version(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
