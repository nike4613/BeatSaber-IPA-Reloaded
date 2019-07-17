using IPA.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.JsonConverters
{
    internal class AlmostVersionConverter : JsonConverter<AlmostVersion>
    {
        public override AlmostVersion ReadJson(JsonReader reader, Type objectType, AlmostVersion existingValue, bool hasExistingValue, JsonSerializer serializer) =>
            reader.Value == null ? null : new AlmostVersion(reader.Value as string);

        public override void WriteJson(JsonWriter writer, AlmostVersion value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else writer.WriteValue(value.ToString());
        }
    }
}
