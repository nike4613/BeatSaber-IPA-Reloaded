using System;
using Newtonsoft.Json;
using Version = SemVer.Version;

namespace IPA.JsonConverters
{
    internal class SemverVersionConverter : JsonConverter<Version>
    {
        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer) => new Version(reader.Value as string, true);

        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
