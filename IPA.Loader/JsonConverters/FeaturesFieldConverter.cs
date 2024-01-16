using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace IPA.JsonConverters
{
    internal class FeaturesFieldConverter : JsonConverter<Dictionary<string, List<JsonObject>>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Assert([DoesNotReturnIf(false)] bool condition)
        {
            if (!condition)
                throw new InvalidOperationException();
        }

        public override Dictionary<string, List<JsonObject>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // TODO: Why?
                _ = JsonSerializer.Deserialize<string[]>(ref reader, options);
                Logger.Features.Warn("Encountered old features used. They no longer do anything, please move to the new format.");
                // TODO: Is there an alternative to existingValue?
                return null;
            }

            var dict = new Dictionary<string, List<JsonObject>>();
            Assert(reader.TokenType == JsonTokenType.StartObject && reader.Read());

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                Assert(reader.Read());

                var list = reader.TokenType == JsonTokenType.StartObject
                    ? (new() { JsonSerializer.Deserialize<JsonObject>(ref reader, options) })
                    : JsonSerializer.Deserialize<List<JsonObject>>(ref reader, options);

                dict.Add(name, list);
                Assert(reader.Read());
            }

            return dict;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, List<JsonObject>> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
