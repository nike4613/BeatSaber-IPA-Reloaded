#nullable enable
using System;
using Hive.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPA.JsonConverters
{
    internal class SemverRangeConverter : JsonConverter<VersionRange?>
    {
        public override VersionRange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
           => reader.TokenType is not JsonTokenType.String ? null : new VersionRange(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, VersionRange? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }
}
