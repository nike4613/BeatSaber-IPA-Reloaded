using IPA.JsonConverters;
using Newtonsoft.Json;
using SemVer;
using System.Collections.Generic;

namespace IPA.Loader
{
    internal class PluginManifest
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name;

        [JsonProperty("id", Required = Required.AllowNull)]
        public string Id;

        [JsonProperty("description", Required = Required.Always)]
        public string Description;

        [JsonProperty("version", Required = Required.Always), JsonConverter(typeof(SemverVersionConverter))]
        public Version Version;

        [JsonProperty("gameVersion", Required = Required.Always), JsonConverter(typeof(SemverVersionConverter))]
        public Version GameVersion;

        [JsonProperty("author", Required = Required.Always)]
        public string Author;

        [JsonProperty("dependsOn", Required = Required.DisallowNull, ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, Range> Dependencies = new Dictionary<string, Range>();

        [JsonProperty("conflictsWith", Required = Required.DisallowNull, ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, Range> Conflicts = new Dictionary<string, Range>();

        [JsonProperty("features", Required = Required.DisallowNull)]
        public string[] Features = new string[0];

        [JsonProperty("loadBefore", Required = Required.DisallowNull)]
        public string[] LoadBefore = new string[0];

        [JsonProperty("loadAfter", Required = Required.DisallowNull)]
        public string[] LoadAfter = new string[0];
    }
}