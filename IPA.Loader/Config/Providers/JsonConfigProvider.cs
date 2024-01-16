#nullable enable
using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Boolean = IPA.Config.Data.Boolean;

namespace IPA.Config.Providers
{
    internal class JsonConfigProvider : IConfigProvider
    {
        public static void RegisterConfig()
        {
            Config.Register<JsonConfigProvider>();
        }

        public string Extension => "json";

        public Value? Load(FileInfo file)
        {
            if (!file.Exists) return Value.Null();

            try
            {
                using var fileStream = file.OpenRead();
                return VisitToValue(JsonNode.Parse(fileStream));
            }
            catch (Exception e)
            {
                Logger.Config.Error($"Error reading JSON file {file.FullName}; ignoring");
                Logger.Config.Error(e);
                return Value.Null();
            }
        }

        private Value? VisitToValue(JsonNode? node)
        {
            if (node == null) return Value.Null();

            switch (node.GetValueKind())
            {
                case JsonValueKind.Undefined:
                    Logger.Config.Warn($"Found {nameof(JsonValueKind)}.{nameof(JsonValueKind.Undefined)}");
                    goto case JsonValueKind.Null;
                case JsonValueKind.Null:
                    return Value.Null();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (node.AsValue().TryGetValue<bool>(out var b))
                        return Value.Bool(b);
                    return Value.Bool(false);
                case JsonValueKind.String:
                    if (node.AsValue().TryGetValue<string>(out var s))
                        return Value.Text(s);
                    return Value.Text(string.Empty);
                case JsonValueKind.Number:
                    var value = node.AsValue();
                    if (value.TryGetValue<long>(out var l))
                        return Value.Integer(l);
                    if (value.TryGetValue<ulong>(out var u))
                        return Value.Integer((long)u);
                    if (value.TryGetValue<decimal>(out var dec))
                        return Value.Float(dec);
                    if (value.TryGetValue<double>(out var dou))
                        return Value.Float((decimal)dou);
                    if (value.TryGetValue<float>(out var flo))
                        return Value.Float((decimal)flo);
                    return Value.Float(0); // default to 0 if something breaks
                case JsonValueKind.Array:
                    return Value.From(node.AsArray().Select(VisitToValue));
                case JsonValueKind.Object:
                    return Value.From(node.AsObject()
                        .Select(kvp => new KeyValuePair<string, Value?>(kvp.Key, VisitToValue(kvp.Value))));
                default:
                    throw new ArgumentException($"Unknown {nameof(JsonValueKind)} in parameter");
            }
        }

        public void Store(Value value, FileInfo file)
        {
            if (!file.Directory.Exists)
                file.Directory.Create();

            try
            {
                var jsonNode = VisitToNode(value);
                using var fileStream = file.Open(FileMode.Create, FileAccess.Write);
                using var jsonWriter = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });

                if (jsonNode == null)
                {
                    jsonWriter.WriteNullValue();
                }
                else
                {
                    jsonNode.WriteTo(jsonWriter);
                }
            }
            catch (Exception e)
            {
                Logger.Config.Error($"Error serializing value for {file.FullName}");
                Logger.Config.Error(e);
            }
        }

        private JsonNode? VisitToNode(Value? val)
        {
            switch (val)
            {
                case Text t:
                    return JsonValue.Create(t.Value);
                case Boolean b:
                    return JsonValue.Create(b.Value);
                case Integer i:
                    return JsonValue.Create(i.Value);
                case FloatingPoint f:
                    return JsonValue.Create(f.Value);
                case List l:
                    var jarr = new JsonArray();
                    foreach (var tok in l.Select(VisitToNode)) jarr.Add(tok);
                    return jarr;
                case Map m:
                    var jobj = new JsonObject();
                    foreach (var kvp in m) jobj.Add(kvp.Key, VisitToNode(kvp.Value));
                    return jobj;
                case null:
                    return null;
                default:
                    throw new ArgumentException($"Unsupported subtype of {nameof(Value)}");
            }
        }
    }
}