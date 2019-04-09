using System;
using System.Collections.Generic;
using System.Linq;
using IPA.JsonConverters;
using IPA.Utilities;
using Newtonsoft.Json;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Updating.BeatMods
{
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

        [Serializable]
        public class Mod
        {
#pragma warning disable CS0649
            /// <summary>
            /// Will be a useless string of characters. Do not use.
            /// </summary>
            [JsonProperty("_id")]
            public string Id;

            [JsonProperty("required")]
            public bool Required;

            [JsonProperty("name")]
            public string Name;

            [JsonProperty("version"),
             JsonConverter(typeof(SemverVersionConverter))]
            public Version Version;

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

            /*[Serializable]
            public class DetailsData
            {
                [JsonProperty("author")]
                public AuthorType Author;
                [JsonProperty("title")]
                public string Title;
                [JsonProperty("description")]
                public string Description;
                [JsonProperty("published")]
                public string Published;
            }

            [JsonProperty("details")]
            public DetailsData Details;*/

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
            /*[Serializable]
            public class PlatformFile
            {
                [JsonProperty("hash"),
                 JsonConverter(typeof(HexArrayConverter))]
                public byte[] Hash = new byte[20];

                [JsonProperty("files", ItemConverterType = typeof(HexArrayConverter))]
                public Dictionary<string, byte[]> FileHashes = new Dictionary<string, byte[]>();

                [JsonProperty("url")]
                public string DownloadPath;

                public override string ToString() =>
                    $"{Utils.ByteArrayToString(Hash)}@{DownloadPath}({string.Join(",", FileHashes.Select(o => $"\"{o.Key}\":\"{Utils.ByteArrayToString(o.Value)}\""))})";
            }

            [Serializable]
            public class FilesObject
            {
                [JsonProperty("steam")]
                public PlatformFile Steam;

                [JsonProperty("oculus")]
                public PlatformFile Oculus;
            }
            
            [JsonProperty("files")]
            public FilesObject Files;*/

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

            /*public class Dependency
            {
                public string Name = null;
                public Range VersionRange = null;
            }

            [Serializable]
            public class LinksType
            {
                [JsonProperty("dependencies", ItemConverterType = typeof(ModSaberDependencyConverter))]
                public Dependency[] Dependencies = new Dependency[0];

                [JsonProperty("conflicts", ItemConverterType = typeof(ModSaberDependencyConverter))]
                public Dependency[] Conflicts = new Dependency[0];
            }

            [JsonProperty("links")]
            public LinksType Links;

            [JsonProperty("oldVersions", ItemConverterType = typeof(SemverVersionConverter))]
            public Version[] OldVersions = new Version[0];*/

            [JsonProperty("dependencies")]
            public Mod[] Dependencies;

            public override string ToString()
            {
                return $"{{\"{Name}\"v{Version} by {Author} files for {string.Join(", ", Downloads.Select(d => d.Type))}}}";
            }
        }

    }
}
