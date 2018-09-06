using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Updating.Converters
{
    internal class SemverVersionConverter : JsonConverter<Version>
    {
        public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer) => new Version(reader.Value as string);

        public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
