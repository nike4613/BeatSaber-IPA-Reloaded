#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace IPA.Json.Converters
{
    internal sealed class FeaturesFieldConverter : JsonConverter<Dictionary<string, List<JsonObject>>>
    {
        public override Dictionary<string, List<JsonObject>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected an object, got {reader.TokenType}");
            }

            var dict = new Dictionary<string, List<JsonObject>>();

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString()!;
                reader.Read();

                var list = new List<JsonObject>();

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    list.Add(JsonNode.Parse(ref reader)!.AsObject());
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.StartObject)
                        {
                            throw new JsonException($"Expected an object, got {reader.TokenType}");
                        }

                        list.Add(JsonNode.Parse(ref reader)!.AsObject());
                    }
                }
                else
                {
                    throw new JsonException($"Expected an object or an array, got {reader.TokenType}");
                }

                dict.Add(name, list);
            }

            return dict;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, List<JsonObject>> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteStartArray();

                foreach (var obj in kvp.Value)
                {
                    obj.WriteTo(writer, options);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}
