using System;
using IPA.Updating.ModSaber;
using Newtonsoft.Json;
using SemVer;

namespace IPA.JsonConverters
{
    internal class ModSaberDependencyConverter : JsonConverter<ApiEndpoint.Mod.Dependency>
    {
        public override ApiEndpoint.Mod.Dependency ReadJson(JsonReader reader, Type objectType, ApiEndpoint.Mod.Dependency existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var parts = (reader.Value as string)?.Split('@');
            return new ApiEndpoint.Mod.Dependency
            {
                Name = parts?[0],
                VersionRange = new Range(parts?[1])
            };
        }

        public override void WriteJson(JsonWriter writer, ApiEndpoint.Mod.Dependency value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.Name}@{value.VersionRange}");
        }
    }
}
