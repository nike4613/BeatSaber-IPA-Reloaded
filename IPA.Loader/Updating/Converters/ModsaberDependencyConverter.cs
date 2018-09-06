using IPA.Updating.ModsaberML;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IPA.Updating.ModsaberML.ApiEndpoint.Mod;

namespace IPA.Updating.Converters
{
    internal class ModsaberDependencyConverter : JsonConverter<Dependency>
    {
        public override Dependency ReadJson(JsonReader reader, Type objectType, Dependency existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var parts = (reader.Value as string).Split('@');
            return new Dependency()
            {
                Name = parts[0],
                VersionRange = new SemVer.Range(parts[1])
            };
        }

        public override void WriteJson(JsonWriter writer, Dependency value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.Name}@{value.VersionRange.ToString()}");
        }
    }
}
