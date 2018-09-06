using Newtonsoft.Json;
using SemVer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Updating.Converters
{
    internal class SemverRangeConverter : JsonConverter<Range>
    {
        public override Range ReadJson(JsonReader reader, Type objectType, Range existingValue, bool hasExistingValue, JsonSerializer serializer) => new Range(reader.Value as string);

        public override void WriteJson(JsonWriter writer, Range value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
