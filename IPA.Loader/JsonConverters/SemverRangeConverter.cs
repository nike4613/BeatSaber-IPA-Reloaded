using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using SemVer;

namespace IPA.JsonConverters
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class SemverRangeConverter : JsonConverter<Range>
    {
        public override Range ReadJson(JsonReader reader, Type objectType, Range existingValue, bool hasExistingValue, JsonSerializer serializer) => new Range(reader.Value as string);

        public override void WriteJson(JsonWriter writer, Range value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
