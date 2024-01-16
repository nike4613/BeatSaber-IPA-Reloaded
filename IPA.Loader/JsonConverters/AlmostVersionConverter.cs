using IPA.Utilities;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPA.JsonConverters
{
    internal class AlmostVersionConverter : JsonConverter<AlmostVersion>
    {
        public override AlmostVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : new AlmostVersion(reader.GetString());

        public override void Write(Utf8JsonWriter writer, AlmostVersion value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }
}
