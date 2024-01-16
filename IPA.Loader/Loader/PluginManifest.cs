#nullable enable
using Hive.Versioning;
using IPA.JsonConverters;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AlmostVersionConverter = IPA.JsonConverters.AlmostVersionConverter;
using Version = Hive.Versioning.Version;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Loader
{
    internal class PluginManifest
    {
        [JsonPropertyName("name")]
        [JsonRequired]
        public string Name { get; init; } = null!;

        [JsonPropertyName("id")]
        [JsonRequired] // TODO: Originally AllowNull
        public string? Id { get; set; }

        [JsonPropertyName("description")]
        [JsonRequired]
        [JsonConverter(typeof(MultilineStringConverter))]
        public string Description { get; set; } = null!;

        [JsonPropertyName("version")]
        [JsonRequired]
        [JsonConverter(typeof(SemverVersionConverter))]
        public Version Version { get; init; } = null!;

        [JsonPropertyName("gameVersion")]
        [JsonRequired] // TODO: Originally DisallowNull
        [JsonConverter(typeof(AlmostVersionConverter))]
        public AlmostVersion? GameVersion { get; init; }

        [JsonPropertyName("author")]
        [JsonRequired]
        public string Author { get; init; } = null!;

        [JsonPropertyName("dependsOn")]
        [JsonRequired] // TODO: Originally DisallowNull
        public Dictionary<string, VersionRange> Dependencies { get; init; } = new();

        [JsonPropertyName("conflictsWith")]
        // TODO: Originally DisallowNull
        public Dictionary<string, VersionRange> Conflicts { get; init; } = new();

        [JsonPropertyName("features")]
        // TODO: Originally DisallowNull
        public Dictionary<string, List<JsonObject>> Features { get; init; } = new();

        [JsonPropertyName("loadBefore")]
        // TODO: Originally DisallowNull
        public string[] LoadBefore { get; init; } = Array.Empty<string>();

        [JsonPropertyName("loadAfter")]
        // TODO: Originally DisallowNull
        public string[] LoadAfter { get; init; } = Array.Empty<string>();

        [JsonPropertyName("icon")]
        // TODO: Originally DisallowNull
        public string? IconPath { get; init; }

        [JsonPropertyName("files")]
        // TODO: Originally DisallowNull
        public string[] Files { get; init; } = Array.Empty<string>();

        [Serializable]
        public class LinksObject
        {
            [JsonPropertyName("project-home")]
            // TODO: Originally DisallowNull
            public Uri? ProjectHome { get; init; }

            [JsonPropertyName("project-source")]
            // TODO: Originally DisallowNull
            public Uri? ProjectSource { get; init; }

            [JsonPropertyName("donate")]
            // TODO: Originally DisallowNull
            public Uri? Donate { get; init; }
        }

        [JsonPropertyName("links")]
        // TODO: Originally DisallowNull
        public LinksObject? Links { get; init; }

        [Serializable]
        public class MiscObject
        {
            [JsonPropertyName("plugin-hint")]
            // TODO: Originally DisallowNull
            public string? PluginMainHint { get; init; }
        }

        [JsonPropertyName("misc")]
        // TODO: Originally DisallowNull
        public MiscObject? Misc { get; init; }
    }
}