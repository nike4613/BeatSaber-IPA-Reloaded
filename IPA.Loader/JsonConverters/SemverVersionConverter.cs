#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Version = Hive.Versioning.Version;

namespace IPA.JsonConverters
{
    internal class SemverVersionConverter : JsonConverter<Version?>
    {
        public override Version? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
           => reader.TokenType is not JsonTokenType.String ? null : new Version(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, Version? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }
}
