using IPA.Config.Data;
using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using Boolean = IPA.Config.Data.Boolean;
using System.Threading.Tasks;

namespace IPA.Config.Providers
{
    internal class JsonConfigProvider : IConfigProvider
    {
        public static void RegisterConfig()
        {
            Config.Register<JsonConfigProvider>();
        }

        public string Extension => "json";

        public Value Load(FileInfo file)
        {
            if (!file.Exists) return Value.Null();

            JToken jtok;
            using (var sreader = new StreamReader(file.OpenRead()))
            {
                using var jreader = new JsonTextReader(sreader);
                jtok = JToken.ReadFrom(jreader);
            }

            try
            {
                return VisitToValue(jtok);
            }
            catch (Exception e)
            {
                Logger.config.Error($"Error reading JSON file {file.FullName}; ignoring");
                Logger.config.Error(e);
                return Value.Null();
            }
        }

        private Value VisitToValue(JToken tok)
        {
            if (tok == null) return Value.Null();

            switch (tok.Type)
            {
                case JTokenType.Raw: // idk if the parser will normally emit a Raw type, but just to be safe
                    return VisitToValue(JToken.Parse((tok as JRaw).Value as string));
                case JTokenType.Undefined:
                    Logger.config.Warn("Found JTokenType.Undefined");
                    goto case JTokenType.Null;
                case JTokenType.Bytes: // never used by Newtonsoft
                    Logger.config.Warn("Found JTokenType.Bytes");
                    goto case JTokenType.Null;
                case JTokenType.Comment: // never used by Newtonsoft
                    Logger.config.Warn("Found JTokenType.Comment");
                    goto case JTokenType.Null;
                case JTokenType.Constructor: // never used by Newtonsoft
                    Logger.config.Warn("Found JTokenType.Constructor");
                    goto case JTokenType.Null;
                case JTokenType.Property: // never used by Newtonsoft
                    Logger.config.Warn("Found JTokenType.Property");
                    goto case JTokenType.Null;
                case JTokenType.Null: 
                    return Value.Null();
                case JTokenType.Boolean:
                    return Value.Bool(((tok as JValue).Value as bool?) ?? false);
                case JTokenType.String:
                    var val = (tok as JValue).Value;
                    if (val is string s) return Value.Text(s);
                    else if (val is char c) return Value.Text("" + c);
                    else return Value.Text(string.Empty);
                case JTokenType.Integer:
                    val = (tok as JValue).Value;
                    if (val is long l) return Value.Integer(l);
                    else if (val is ulong u) return Value.Integer((long)u);
                    else return Value.Integer(0);
                case JTokenType.Float:
                    val = (tok as JValue).Value;
                    if (val is decimal dec) return Value.Float(dec);
                    else if (val is double dou) return Value.Float((decimal)dou);
                    else if (val is float flo) return Value.Float((decimal)flo);
                    else return Value.Float(0); // default to 0 if something breaks
                case JTokenType.Date:
                    val = (tok as JValue).Value;
                    if (val is DateTime dt) return Value.Text(dt.ToString());
                    else if (val is DateTimeOffset dto) return Value.Text(dto.ToString());
                    else return Value.Text("Unknown Date-type token");
                case JTokenType.TimeSpan:
                    val = (tok as JValue).Value;
                    if (val is TimeSpan ts) return Value.Text(ts.ToString());
                    else return Value.Text("Unknown TimeSpan-type token");
                case JTokenType.Guid:
                    val = (tok as JValue).Value;
                    if (val is Guid g) return Value.Text(g.ToString());
                    else return Value.Text("Unknown Guid-type token");
                case JTokenType.Uri:
                    val = (tok as JValue).Value;
                    if (val is Uri ur) return Value.Text(ur.ToString());
                    else return Value.Text("Unknown Uri-type token");
                case JTokenType.Array:
                    return Value.From((tok as JArray).Select(VisitToValue));
                case JTokenType.Object:
                    return Value.From((tok as IEnumerable<KeyValuePair<string, JToken>>)
                        .Select(kvp => new KeyValuePair<string, Value>(kvp.Key, VisitToValue(kvp.Value))));
                default:
                    throw new ArgumentException($"Unknown {nameof(JTokenType)} in parameter");
            }
        }

        public void Store(Value value, FileInfo file)
        {
            if (file.Directory.Exists)
                file.Directory.Create();

            try
            {
                var tok = VisitToToken(value);

                using var swriter = new StreamWriter(file.Open(FileMode.Create, FileAccess.Write));
                using var jwriter = new JsonTextWriter(swriter)
                {
                    Formatting = Formatting.Indented
                };
                tok.WriteTo(jwriter);
            }
            catch (Exception e)
            {
                Logger.config.Error($"Error serializing value for {file.FullName}");
                Logger.config.Error(e);
            }
        }

        private JToken VisitToToken(Value val) 
        { 
            switch (val)
            {
                case Text t:
                    return new JValue(t.Value);
                case Boolean b:
                    return new JValue(b.Value);
                case Integer i:
                    return new JValue(i.Value);
                case FloatingPoint f:
                    return new JValue(f.Value);
                case List l:
                    var jarr = new JArray();
                    foreach (var tok in l.Select(VisitToToken)) jarr.Add(tok);
                    return jarr;
                case Map m:
                    var jobj = new JObject();
                    foreach (var kvp in m) jobj.Add(kvp.Key, VisitToToken(kvp.Value));
                    return jobj;
                case null:
                    return JValue.CreateNull();
                default:
                    throw new ArgumentException($"Unsupported subtype of {nameof(Value)}");
            }
        }
    }
}