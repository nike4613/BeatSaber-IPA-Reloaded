#nullable enable
using System;
using Newtonsoft.Json;
using Version = Hive.Versioning.Version;

namespace IPA.JsonConverters
{
    internal class SemverVersionConverter : JsonConverter<Version?>
    {
        public override Version? ReadJson(JsonReader reader, Type objectType, Version? existingValue, bool hasExistingValue, JsonSerializer serializer)
            => reader.Value is not string s ? existingValue : new Version(s);

        public override void WriteJson(JsonWriter writer, Version? value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else writer.WriteValue(value.ToString());
        }
    }
}
