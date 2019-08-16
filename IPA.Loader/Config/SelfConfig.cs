// BEGIN: section ignore
using IPA.Logging;
using IPA.Utilities;
// END: section ignore
using Newtonsoft.Json;

namespace IPA.Config
{
    internal class SelfConfig
    {
        // This is to allow the doc generation to parse this file and use Newtonsoft to generate a JSON schema
        // BEGIN: section ignore

        private static IConfigProvider _loaderConfig;

        public static IConfigProvider LoaderConfig
        {
            get => _loaderConfig;
            set
            {
                _loaderConfig?.RemoveLinks();
                value.Load();
                SelfConfigRef = value.MakeLink<SelfConfig>((c, v) =>
                {
                    if (v.Value.Regenerate)
                        c.Store(v.Value = new SelfConfig { Regenerate = false });

                    StandardLogger.Configure(v.Value);
                });
                _loaderConfig = value;
            }
        }

        public static Ref<SelfConfig> SelfConfigRef;

        public static void Load()
        {
            LoaderConfig = Config.GetProviderFor(IPAName, "json");
        }

        public static void ReadCommandLine(string[] args)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "--no-yeet":
                        CommandLineValues.YeetMods = false;
                        break;
                    case "--condense-logs":
                        CommandLineValues.Debug.CondenseModLogs = true;
                        break;
                    case "--debug":
                        CommandLineValues.Debug.ShowDebug = true;
                        CommandLineValues.Debug.ShowCallSource = true;
                        break;
                    case "--no-updates":
                        CommandLineValues.Updates.AutoCheckUpdates = false;
                        CommandLineValues.Updates.AutoUpdate = false;
                        break;
                }
            }
        }

        internal const string IPAName = "Beat Saber IPA";
        internal const string IPAVersion = "3.12.25";

        // uses Updates.AutoUpdate, Updates.AutoCheckUpdates, YeetMods, Debug.ShowCallSource, Debug.ShowDebug, 
        //      Debug.CondenseModLogs
        internal static SelfConfig CommandLineValues = new SelfConfig();

        // END: section ignore

        public bool Regenerate = true;

        public class Updates_
        {
            public bool AutoUpdate = true;
            // LINE: ignore 2
            public static bool AutoUpdate_ => SelfConfigRef.Value.Updates.AutoUpdate
                                           &&   CommandLineValues.Updates.AutoUpdate;

            public bool AutoCheckUpdates = true;
            // LINE: ignore 2
            public static bool AutoCheckUpdates_ => SelfConfigRef.Value.Updates.AutoCheckUpdates
                                                 &&   CommandLineValues.Updates.AutoCheckUpdates;
        }

        public Updates_ Updates = new Updates_();

        public class Debug_
        {
            public bool ShowCallSource = false;
            // LINE: ignore 2
            public static bool ShowCallSource_ => SelfConfigRef.Value.Debug.ShowCallSource
                                               ||   CommandLineValues.Debug.ShowCallSource;

            public bool ShowDebug = false;
            // LINE: ignore 2
            public static bool ShowDebug_ => SelfConfigRef.Value.Debug.ShowDebug
                                          ||   CommandLineValues.Debug.ShowDebug;

            // This option only takes effect after a full game restart, unless new logs are created again
            public bool CondenseModLogs = false;
            // LINE: ignore 2
            public static bool CondenseModLogs_ => SelfConfigRef.Value.Debug.CondenseModLogs
                                                ||   CommandLineValues.Debug.CondenseModLogs;

            public bool ShowHandledErrorStackTraces = false;
            // LINE: ignore
            public static bool ShowHandledErrorStackTraces_ => SelfConfigRef.Value.Debug.ShowHandledErrorStackTraces;

            public bool HideMessagesForPerformance = true;
            // LINE: ignore
            public static bool HideMessagesForPerformance_ => SelfConfigRef.Value.Debug.HideMessagesForPerformance;

            public int HideLogThreshold = 512;
            // LINE: ignore
            public static int HideLogThreshold_ => SelfConfigRef.Value.Debug.HideLogThreshold;
        }

        public Debug_ Debug = new Debug_();

        public bool YeetMods = true;
        // LINE: ignore 2
        public static bool YeetMods_ => SelfConfigRef.Value.YeetMods 
                                     &&   CommandLineValues.YeetMods;

        [JsonProperty(Required = Required.Default)]
        public string LastGameVersion = null;
        // LINE: ignore
        public static string LastGameVersion_ => SelfConfigRef.Value.LastGameVersion;
    }
}