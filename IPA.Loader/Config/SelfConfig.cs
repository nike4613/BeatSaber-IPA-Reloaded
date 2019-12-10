// BEGIN: section ignore
using IPA.Logging;
using IPA.Utilities;
using IPA.Config.Stores;
// END: section ignore
using Newtonsoft.Json;

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
        internal const string IPAVersion = "3.99.99.0";

        // uses Updates.AutoUpdate, Updates.AutoCheckUpdates, YeetMods, Debug.ShowCallSource, Debug.ShowDebug, 
        //      Debug.CondenseModLogs
        internal static SelfConfig CommandLineValues = new SelfConfig();

        // END: section ignore

        public bool Regenerate = true;

        public class Updates_
        {
            public bool AutoUpdate = true;
            // LINE: ignore 2
            public static bool AutoUpdate_ => (Instance?.Updates?.AutoUpdate ?? true)
                                           &&   CommandLineValues.Updates.AutoUpdate;

            public bool AutoCheckUpdates = true;
            // LINE: ignore 2
            public static bool AutoCheckUpdates_ => (Instance?.Updates?.AutoCheckUpdates ?? true)
                                                 &&   CommandLineValues.Updates.AutoCheckUpdates;
        }

        public Updates_ Updates = new Updates_();

        public class Debug_
        {
            public bool ShowCallSource = false;
            // LINE: ignore 2
            public static bool ShowCallSource_ => (Instance?.Debug?.ShowCallSource ?? false)
                                               ||   CommandLineValues.Debug.ShowCallSource;

            public bool ShowDebug = false;
            // LINE: ignore 2
            public static bool ShowDebug_ => (Instance?.Debug?.ShowDebug ?? false)
                                          ||   CommandLineValues.Debug.ShowDebug;

            // This option only takes effect after a full game restart, unless new logs are created again
            public bool CondenseModLogs = false;
            // LINE: ignore 2
            public static bool CondenseModLogs_ => (Instance?.Debug?.CondenseModLogs ?? false)
                                                ||   CommandLineValues.Debug.CondenseModLogs;

            public bool ShowHandledErrorStackTraces = false;
            // LINE: ignore
            public static bool ShowHandledErrorStackTraces_ => Instance?.Debug?.ShowHandledErrorStackTraces ?? false;

            public bool HideMessagesForPerformance = true;
            // LINE: ignore
            public static bool HideMessagesForPerformance_ => Instance?.Debug?.HideMessagesForPerformance ?? true;

            public int HideLogThreshold = 512;
            // LINE: ignore
            public static int HideLogThreshold_ => Instance?.Debug?.HideLogThreshold ?? 512;

            public bool ShowTrace = false;
            // LINE: ignore 2
            public static bool ShowTrace_ => (Instance?.Debug?.ShowTrace ?? false)
                                          ||   CommandLineValues.Debug.ShowTrace;
        }

        public Debug_ Debug = new Debug_();

        public bool YeetMods = true;
        // LINE: ignore 2
        public static bool YeetMods_ => (Instance?.YeetMods ?? true)
                                     &&   CommandLineValues.YeetMods;

        [JsonProperty(Required = Required.Default)]
        public string LastGameVersion = null;
        // LINE: ignore
        public static string LastGameVersion_ => Instance?.LastGameVersion;
    }
}