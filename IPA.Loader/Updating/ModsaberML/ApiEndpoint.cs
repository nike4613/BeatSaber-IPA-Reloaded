using IPA.Logging;
using IPA.Updating.Converters;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SemVer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Version = SemVer.Version;

namespace IPA.Updating.ModsaberML
{
    class ApiEndpoint
    {
        public const string ApiBase = "https://www.modsaber.org/";
        public const string GetModInfoEndpoint = "registry/{0}/{1}";
        public const string GetModsWithSemver = "api/v1.0/mods/semver/{0}/{1}";

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
                        return LoneFunctions.StringToByteArray((string)reader.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("Error parsing version string: {0}", reader.Value), ex);
                    }
                }
                throw new Exception(string.Format("Unexpected token or value when parsing hex string. Token: {0}, Value: {1}", reader.TokenType, reader.Value));
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
                    writer.WriteValue(LoneFunctions.ByteArrayToString(value as byte[]));
                }
            }
        }

        [Serializable]
        public class Mod
        {
#pragma warning disable CS0649
            [JsonProperty("name")]
            public string Name;

            [JsonProperty("version"),
             JsonConverter(typeof(SemverVersionConverter))]
            public Version Version;

            [JsonProperty("approved")]
            public bool Approved;

            [JsonProperty("title")]
            public string Title;

            [JsonProperty("gameVersion"), 
             JsonConverter(typeof(SemverVersionConverter))]
            public Version GameVersion;

            [JsonProperty("author")]
            public string Author;

#pragma warning restore CS0649
            [Serializable]
            public class PlatformFile
            {
                [JsonProperty("hash"),
                 JsonConverter(typeof(HexArrayConverter))]
                public byte[] Hash = new byte[20];

                [JsonProperty("files", ItemConverterType = typeof(HexArrayConverter))]
                public Dictionary<string, byte[]> FileHashes = new Dictionary<string, byte[]>();

                [JsonProperty("url")]
                public string DownloadPath = null;

                public override string ToString()
                {
                    return $"{LoneFunctions.ByteArrayToString(Hash)}@{DownloadPath}({string.Join(",",FileHashes.Select(o=>$"\"{o.Key}\":\"{LoneFunctions.ByteArrayToString(o.Value)}\""))})";
                }
            }

            [Serializable]
            public class FilesObject
            {
                [JsonProperty("steam")]
                public PlatformFile Steam = null;

                [JsonProperty("oculus")]
                public PlatformFile Oculus = null;
            }
            
            [JsonProperty("files")]
            public FilesObject Files = null;
            
            public class Dependency
            {
                public string Name = null;
                public Range VersionRange = null;
            }

            [JsonProperty("dependsOn", ItemConverterType = typeof(ModsaberDependencyConverter))]
            public Dependency[] Dependencies = new Dependency[0];

            [JsonProperty("conflictsWith", ItemConverterType = typeof(ModsaberDependencyConverter))]
            public Dependency[] Conflicts = new Dependency[0];

            [JsonProperty("oldVersions", ItemConverterType = typeof(SemverVersionConverter))]
            public Version[] OldVersions = new Version[0];

            public override string ToString()
            {
                return $"{{\"{Title} ({Name})\"v{Version} for {GameVersion} by {Author} with \"{Files.Steam}\" and \"{Files.Oculus}\"}}";
            }
        }

    }
}
