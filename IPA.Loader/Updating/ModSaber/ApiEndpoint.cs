using System;
using System.Collections.Generic;
using System.Linq;
using IPA.Updating.Converters;
using IPA.Utilities;
using Newtonsoft.Json;
using SemVer;
using Version = SemVer.Version;

namespace IPA.Updating.ModSaber
{
    class ApiEndpoint
    {
        public const string ApiBase = "https://www.modsaber.org/";
        public const string GetModInfoEndpoint = "registry/{0}/{1}";
        public const string GetModsWithSemver = "api/v1.1/mods/semver/{0}/{1}";

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
                    writer.WriteValue(LoneFunctions.ByteArrayToString((byte[]) value));
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

            [Serializable]
            public class AuthorType
            {
                [JsonProperty("name")]
                public string Name;
                [JsonProperty("id")]
                public string Id;

                public override string ToString() => Name;
            }

            [Serializable]
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
            public DetailsData Details;

            [Serializable]
            public class ApprovalStatus
            {
                [JsonProperty("status")]
                public bool Status;
                [JsonProperty("modified")]
                public string LastModified;
            }

            [JsonProperty("approval")]
            public ApprovalStatus Approval;
            
            [Serializable]
            public class GameVersionType
            {
                [JsonProperty("value"),
                 JsonConverter(typeof(SemverVersionConverter))]
                public Version Version;
                [JsonProperty("manifest")]
                public string Manifest;
            }

            [JsonProperty("gameVersion"), 
             JsonConverter(typeof(SemverVersionConverter))]
            public GameVersionType GameVersion;

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
                public string DownloadPath;

                public override string ToString() =>
                    $"{LoneFunctions.ByteArrayToString(Hash)}@{DownloadPath}({string.Join(",", FileHashes.Select(o => $"\"{o.Key}\":\"{LoneFunctions.ByteArrayToString(o.Value)}\""))})";
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
            public FilesObject Files;
            
            public class Dependency
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
            public Version[] OldVersions = new Version[0];

            public override string ToString()
            {
                return $"{{\"{Details.Title} ({Name})\"v{Version} for {GameVersion.Version} by {Details.Author} with \"{Files.Steam}\" and \"{Files.Oculus}\"}}";
            }
        }

    }
}
