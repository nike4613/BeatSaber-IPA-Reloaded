using System;
using System.Collections.Generic;
using System.Linq;
using IPA.JsonConverters;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Updating.BeatMods
{
#if BeatSaber
    class ApiEndpoint
    {
        public const string BeatModBase = "https://beatmods.com";
        public const string ApiBase = BeatModBase + "/api/v1/mod";
        public const string GetModInfoEndpoint = "?name={0}&version={1}";
        public const string GetModsByName = "?name={0}";

        class HexArrayConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(byte[]);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                if (reader.TokenType == JsonToken.String)
                {
                    try
                    {
                        return Utils.StringToByteArray((string)reader.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error parsing version string: {reader.Value}", ex);
                    }
                }
                throw new Exception(
                    $"Unexpected token or value when parsing hex string. Token: {reader.TokenType}, Value: {reader.Value}");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    if (!(value is byte[]))
                    {
                        throw new JsonSerializationException("Expected byte[] object value");
                    }
                    writer.WriteValue(Utils.ByteArrayToString((byte[]) value));
                }
            }
        }
        class ModMultiformatJsonConverter : JsonConverter<Mod>
        {
            public override Mod ReadJson(JsonReader reader, Type objectType, Mod existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                    return new Mod { Id = reader.Value as string, IsIdReference = true };
                else
                {
                    if (reader.TokenType != JsonToken.StartObject)
                        return null;

                    return serializer.Deserialize<Mod>(reader);
                }
            }

            public override void WriteJson(JsonWriter writer, Mod value, JsonSerializer serializer) => serializer.Serialize(writer, value);
        }

        [Serializable]
        public class Mod
        {
#pragma warning disable CS0649
            /// <summary>
            /// Will be a useless string of characters. Do not use.
            /// </summary>
            [JsonProperty("_id")]
            public string Id;

            [JsonIgnore]
            public bool IsIdReference = false;

            [JsonProperty("required")]
            public bool Required;

            [JsonProperty("name")]
            public string Name;

            [JsonProperty("version"),
             JsonConverter(typeof(SemverVersionConverter))]
            public Version Version;

            [JsonProperty("gameVersion"),
             JsonConverter(typeof(JsonConverters.AlmostVersionConverter))]
            public AlmostVersion GameVersion;

            [Serializable]
            public class AuthorType
            {
                [JsonProperty("username")]
                public string Name;
                [JsonProperty("_id")]
                public string Id;

                public override string ToString() => Name;
            }

            [JsonProperty("author")]
            public AuthorType Author;

            [JsonProperty("status")]
            public string Status;
            public const string ApprovedStatus = "approved";

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("category")]
            public string Category;

            [JsonProperty("link")]
            public Uri Link;
            
#pragma warning restore CS0649

            [Serializable]
            public class DownloadsObject
            {
                public const string TypeUniversal = "universal";
                public const string TypeSteam = "steam";
                public const string TypeOculus = "oculus";

                [JsonProperty("type")]
                public string Type;

                [JsonProperty("url")]
                public string Path;

                [Serializable]
                public class HashObject
                {
                    [JsonProperty("hash"), JsonConverter(typeof(HexArrayConverter))]
                    public byte[] Hash;

                    [JsonProperty("file")]
                    public string File;
                }

                /// <summary>
                /// Hashes stored are MD5
                /// </summary>
                [JsonProperty("hashMd5")]
                public HashObject[] Hashes;
            }

            [JsonProperty("downloads")]
            public DownloadsObject[] Downloads;

            [JsonProperty("dependencies", ItemConverterType = typeof(ModMultiformatJsonConverter))]
            public Mod[] Dependencies;

            public override string ToString()
            {
                return $"{{\"{Name}\"v{Version} by {Author} files for {string.Join(", ", Downloads.Select(d => d.Type))}}}";
            }
        }

    }
#endif
}
