using IPA.JsonConverters;
using IPA.Utilities;
using Newtonsoft.Json;
using SemVer;
using System;
using System.Collections.Generic;
using AlmostVersionConverter = IPA.JsonConverters.AlmostVersionConverter;
using Version = SemVer.Version;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Loader
{
    internal class PluginManifest
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name;

        [JsonProperty("id", Required = Required.AllowNull)]
        public string Id;

        [JsonProperty("description", Required = Required.Always), JsonConverter(typeof(MultilineStringConverter))]
        public string Description;

        [JsonProperty("version", Required = Required.Always), JsonConverter(typeof(SemverVersionConverter))]
        public Version Version;

        [JsonProperty("gameVersion", Required = Required.Always), JsonConverter(typeof(AlmostVersionConverter))]
        public AlmostVersion GameVersion;

        [JsonProperty("author", Required = Required.Always)]
        public string Author;

        [JsonProperty("dependsOn", Required = Required.DisallowNull, ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, Range> Dependencies = new Dictionary<string, Range>();

        [JsonProperty("conflictsWith", Required = Required.DisallowNull, ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, Range> Conflicts = new Dictionary<string, Range>();

        [JsonProperty("features", Required = Required.DisallowNull)]
        public string[] Features = Array.Empty<string>();

        [JsonProperty("loadBefore", Required = Required.DisallowNull)]
        public string[] LoadBefore = Array.Empty<string>();

        [JsonProperty("loadAfter", Required = Required.DisallowNull)]
        public string[] LoadAfter = Array.Empty<string>();

        [JsonProperty("icon", Required = Required.DisallowNull)]
        public string IconPath = null;

        [Serializable]
        public class LinksObject
        {
            [JsonProperty("project-home", Required = Required.DisallowNull)]
            public Uri ProjectHome = null;

            [JsonProperty("project-source", Required = Required.DisallowNull)]
            public Uri ProjectSource = null;

            [JsonProperty("donate", Required = Required.DisallowNull)]
            public Uri Donate = null;
        }

        [JsonProperty("links", Required = Required.DisallowNull)]
        public LinksObject Links = null;

        [Serializable]
        public class MiscObject
        {
            [JsonProperty("plugin-hint", Required = Required.DisallowNull)]
            public string PluginMainHint = null;
        }

        [JsonProperty("misc", Required = Required.DisallowNull)]
        public MiscObject Misc = null;
    }
}