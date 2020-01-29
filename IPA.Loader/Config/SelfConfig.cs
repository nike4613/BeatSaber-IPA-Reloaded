// BEGIN: section ignore
using IPA.Logging;
using IPA.Utilities;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
// END: section ignore
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IPA.Config
{
    internal class SelfConfig
    {
        // This is to allow the doc generation to parse this file and use Newtonsoft to generate a JSON schema
        // BEGIN: section ignore

        public static Config LoaderConfig { get; set; }

        public static SelfConfig Instance = new SelfConfig();

        public static void Load()
        {
            LoaderConfig = Config.GetConfigFor(IPAName, "json");
            Instance = LoaderConfig.Generated<SelfConfig>();
        }

        protected virtual void CopyFrom(SelfConfig cfg) { }
        protected internal virtual void OnReload()
        {
            if (Regenerate)
                CopyFrom(new SelfConfig { Regenerate = false });
            StandardLogger.Configure();
        }

        protected internal virtual void Changed()
        {
            Logger.log.Debug("SelfConfig Changed called");
        }

        public static void ReadCommandLine(string[] args)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "--debug":
                    case "--mono-debug":
                        CommandLineValues.Debug.ShowDebug = true;
                        CommandLineValues.Debug.ShowCallSource = true;
                        break;
                    case "--no-yeet":
                        CommandLineValues.YeetMods = false;
                        break;
                    case "--condense-logs":
                        CommandLineValues.Debug.CondenseModLogs = true;
                        break;
                    case "--no-updates":
                        CommandLineValues.Updates.AutoCheckUpdates = false;
                        CommandLineValues.Updates.AutoUpdate = false;
                        break;
                    case "--trace":
                        CommandLineValues.Debug.ShowTrace = true;
                        break;
                }
            }
        }

        internal const string IPAName = "Beat Saber IPA";
        internal const string IPAVersion = "3.99.99.5";

        // uses Updates.AutoUpdate, Updates.AutoCheckUpdates, YeetMods, Debug.ShowCallSource, Debug.ShowDebug, 
        //      Debug.CondenseModLogs
        internal static SelfConfig CommandLineValues = new SelfConfig();

        // END: section ignore

        public virtual bool Regenerate { get; set; } = true;

        public class Updates_
        {
            public virtual bool AutoUpdate { get; set; } = true;
            // LINE: ignore 2
            public static bool AutoUpdate_ => (Instance?.Updates?.AutoUpdate ?? true)
                                           &&   CommandLineValues.Updates.AutoUpdate;

            public virtual bool AutoCheckUpdates { get; set; } = true;
            // LINE: ignore 2
            public static bool AutoCheckUpdates_ => (Instance?.Updates?.AutoCheckUpdates ?? true)
                                                 &&   CommandLineValues.Updates.AutoCheckUpdates;
        }

        // LINE: ignore
        [NonNullable]
        public virtual Updates_ Updates { get; set; } = new Updates_();

        public class Debug_
        {
            public virtual bool ShowCallSource { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowCallSource_ => (Instance?.Debug?.ShowCallSource ?? false)
                                               ||   CommandLineValues.Debug.ShowCallSource;

            public virtual bool ShowDebug { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowDebug_ => (Instance?.Debug?.ShowDebug ?? false)
                                          ||   CommandLineValues.Debug.ShowDebug;

            // This option only takes effect after a full game restart, unless new logs are created again
            public virtual bool CondenseModLogs { get; set; } = false;
            // LINE: ignore 2
            public static bool CondenseModLogs_ => (Instance?.Debug?.CondenseModLogs ?? false)
                                                ||   CommandLineValues.Debug.CondenseModLogs;

            public virtual bool ShowHandledErrorStackTraces { get; set; } = false;
            // LINE: ignore
            public static bool ShowHandledErrorStackTraces_ => Instance?.Debug?.ShowHandledErrorStackTraces ?? false;

            public virtual bool HideMessagesForPerformance { get; set; } = true;
            // LINE: ignore
            public static bool HideMessagesForPerformance_ => Instance?.Debug?.HideMessagesForPerformance ?? true;

            public virtual int HideLogThreshold { get; set; } = 512;
            // LINE: ignore
            public static int HideLogThreshold_ => Instance?.Debug?.HideLogThreshold ?? 512;

            public virtual bool ShowTrace { get; set; } = false;
            // LINE: ignore 2
            public static bool ShowTrace_ => (Instance?.Debug?.ShowTrace ?? false)
                                          ||   CommandLineValues.Debug.ShowTrace;
        }

        // LINE: ignore
        [NonNullable]
        public virtual Debug_ Debug { get; set; } = new Debug_();

        public virtual bool YeetMods { get; set; } = true;
        // LINE: ignore 2
        public static bool YeetMods_ => (Instance?.YeetMods ?? true)
                                     &&   CommandLineValues.YeetMods;

        // LINE: ignore
        [NonNullable, UseConverter(typeof(CollectionConverter<string, HashSet<string>>))]
        public virtual HashSet<string> GameAssemblies { get; set; } = new HashSet<string>
            {
            // LINE: ignore 5
#if BeatSaber // provide these defaults only for Beat Saber builds
                "MainAssembly.dll", "HMLib.dll", "HMUI.dll", "VRUI.dll"
#else // otherwise specify Assembly-CSharp.dll
                "Assembly-CSharp.dll"
#endif
            };
        // LINE: ignore
        public static HashSet<string> GameAssemblies_ => Instance?.GameAssemblies ?? new HashSet<string> { "Assembly-CSharp.dll" };

        [JsonProperty(Required = Required.DisallowNull)] // Used for documentation schema generation
        public virtual string LastGameVersion { get; set; } = null;
        // LINE: ignore
        public static string LastGameVersion_ => Instance?.LastGameVersion;
    }
}