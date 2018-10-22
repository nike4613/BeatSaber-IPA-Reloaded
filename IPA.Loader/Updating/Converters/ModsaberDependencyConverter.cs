using System;
using Newtonsoft.Json;
using SemVer;
using static IPA.Updating.ModSaber.ApiEndpoint.Mod;

namespace IPA.Updating.Converters
{
    internal class ModSaberDependencyConverter : JsonConverter<Dependency>
    {
        public override Dependency ReadJson(JsonReader reader, Type objectType, Dependency existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var parts = (reader.Value as string)?.Split('@');
            return new Dependency
            {
                Name = parts?[0],
                VersionRange = new Range(parts?[1])
            };
        }

        public override void WriteJson(JsonWriter writer, Dependency value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.Name}@{value.VersionRange}");
        }
    }
}
