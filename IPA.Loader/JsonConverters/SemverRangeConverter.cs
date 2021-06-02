#nullable enable
using System;
using Hive.Versioning;
using Newtonsoft.Json;

namespace IPA.JsonConverters
{
    internal class SemverRangeConverter : JsonConverter<VersionRange?>
    {
        public override VersionRange? ReadJson(JsonReader reader, Type objectType, VersionRange? existingValue, bool hasExistingValue, JsonSerializer serializer)
            => reader.Value is string s && VersionRange.TryParse(s, out var range) ? range : existingValue;

        public override void WriteJson(JsonWriter writer, VersionRange? value, JsonSerializer serializer)
            => writer.WriteValue(value?.ToString());
    }
}
