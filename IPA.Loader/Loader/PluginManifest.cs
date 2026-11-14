#nullable enable
using Hive.Versioning;
using IPA.Json.Converters;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Version = Hive.Versioning.Version;

namespace IPA.Loader
{
    internal class PluginManifest
    {
        [JsonRequired]
        public string Name { get; init; } = null!;

        [JsonRequired]
        public string Id { get; init; } = null!;

        [JsonRequired]
        [JsonConverter(typeof(MultilineStringConverter))]
        public string Description { get; set; } = null!;

        [JsonRequired]
        public Version Version { get; init; } = null!;

        [DisallowNull]
        public AlmostVersion? GameVersion { get; init; }

        [JsonRequired]
        public string Author { get; init; } = null!;

        [JsonPropertyName("dependsOn")]
        public Dictionary<string, VersionRange> Dependencies { get; init; } = new();

        [JsonPropertyName("conflictsWith")]
        public Dictionary<string, VersionRange> Conflicts { get; init; } = new();

        public Dictionary<string, List<JsonObject>> Features { get; init; } = new();

        public string[] LoadBefore { get; init; } = Array.Empty<string>();

        public string[] LoadAfter { get; init; } = Array.Empty<string>();

        [JsonPropertyName("icon")]
        [DisallowNull]
        public string? IconPath { get; init; }

        public string[] Files { get; init; } = Array.Empty<string>();

        [Serializable]
        public class LinksObject
        {
            [JsonPropertyName("project-home")]
            [DisallowNull]
            public Uri? ProjectHome { get; init; }

            [JsonPropertyName("project-source")]
            [DisallowNull]
            public Uri? ProjectSource { get; init; }

            [DisallowNull]
            public Uri? Donate { get; init; }
        }

        [DisallowNull]
        public LinksObject? Links { get; init; }

        [Serializable]
        public class MiscObject
        {
            [JsonPropertyName("plugin-hint")]
            [DisallowNull]
            public string? PluginMainHint { get; init; }
        }

        [DisallowNull]
        public MiscObject? Misc { get; init; }
    }
}