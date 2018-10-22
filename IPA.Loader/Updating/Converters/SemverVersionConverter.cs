using Newtonsoft.Json;
using System;
using Version = SemVer.Version;

namespace IPA.Updating.Converters
{
    internal class SemverVersionConverter : JsonConverter<Version>
    {
        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer) => new Version(reader.Value as string);

        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
