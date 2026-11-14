#nullable enable
using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Boolean = IPA.Config.Data.Boolean;

namespace IPA.Config.Providers
{
    internal class JsonConfigProvider : IConfigProvider
    {
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void RegisterConfig()
        {
            Config.Register<JsonConfigProvider>();
        }

        public string Extension => "json";

        public Value? Load(FileInfo file)
        {
            if (!file.Exists)
            {
                return Value.Null();
            }

            byte[]? buffer = null;

            try
            {
                using var fileStream = file.OpenRead();
                var length = (int)fileStream.Length;
                buffer = ArrayPool<byte>.Shared.Rent(length);

                var read = 0;
                int count;
                while (read < length && (count = fileStream.Read(buffer, read, length - read)) > 0)
                {
                    read += count;
                }

                var reader = new Utf8JsonReader(buffer.AsSpan(0, read));
                reader.Read();

                return VisitToValue(ref reader, string.Empty);
            }
            catch (Exception e)
            {
                Logger.Config.Error($"Error reading JSON file {file.FullName}; ignoring");
                Logger.Config.Error(e);
                return Value.Null();
            }
            finally
            {
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private Value? VisitToValue(ref Utf8JsonReader reader, string member)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return Value.Null();
                case JsonTokenType.True:
                    return Value.Bool(true);
                case JsonTokenType.False:
                    return Value.Bool(false);
                case JsonTokenType.String:
                    return Value.Text(reader.GetString()!);
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var l))
                    {
                        return Value.Integer(l);
                    }
                    if (reader.TryGetDecimal(out var dec))
                    {
                        return Value.Float(dec);
                    }
                    Logger.Config.Warn($"'{member}' is '{Encoding.UTF8.GetString([.. reader.ValueSpan])}', representable as neither Integer nor FloatingPoint; treating as null");
                    return Value.Null();
                case JsonTokenType.StartArray:
                    var list = Value.List();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(VisitToValue(ref reader, member));
                    }
                    return list;
                case JsonTokenType.StartObject:
                    var map = Value.Map();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        var name = reader.GetString()!;
                        reader.Read();
                        map.Add(name, VisitToValue(ref reader, name));
                    }
                    return map;
                default:
                    throw new JsonException($"Unexpected {nameof(JsonTokenType)}.{reader.TokenType}");
            }
        }

        public void Store(Value value, FileInfo file)
        {
            if (!file.Directory!.Exists)
            {
                file.Directory.Create();
            }

            try
            {
                using var fileStream = file.Open(FileMode.Create, FileAccess.Write);
                using var writer = new Utf8JsonWriter(fileStream, WriterOptions);
                VisitToWriter(writer, value);
            }
            catch (Exception e)
            {
                Logger.Config.Error($"Error serializing value for {file.FullName}");
                Logger.Config.Error(e);
            }
        }

        private void VisitToWriter(Utf8JsonWriter writer, Value? val)
        {
            switch (val)
            {
                case Text t:
                    writer.WriteStringValue(t.Value);
                    break;
                case Boolean b:
                    writer.WriteBooleanValue(b.Value);
                    break;
                case Integer i:
                    writer.WriteNumberValue(i.Value);
                    break;
                case FloatingPoint f:
                    writer.WriteNumberValue(f.Value);
                    break;
                case List l:
                    writer.WriteStartArray();
                    foreach (var item in l)
                    {
                        VisitToWriter(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
                case Map m:
                    writer.WriteStartObject();
                    foreach (var kvp in m)
                    {
                        writer.WritePropertyName(kvp.Key);
                        VisitToWriter(writer, kvp.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case null:
                    writer.WriteNullValue();
                    break;
                default:
                    throw new ArgumentException($"Unsupported subtype of {nameof(Value)}");
            }
        }
    }
}