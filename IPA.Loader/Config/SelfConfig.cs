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

        internal const string IPAName = "Beat Saber IPA";
        internal const string IPAVersion = "3.12.25";

        // uses Updates.AutoCheckUpdates, YeetMods
        internal static SelfConfig CommandLineValues = new SelfConfig();

        // END: section ignore

        public bool Regenerate = true;

        public class Updates_
        {
            public bool AutoUpdate = true;
            // LINE: ignore
            public static bool AutoUpdate_ => SelfConfigRef.Value.Updates.AutoUpdate;

            public bool AutoCheckUpdates = true;
            // LINE: ignore 2
            public static bool AutoCheckUpdates_ => SelfConfigRef.Value.Updates.AutoCheckUpdates 
                                                 &&   CommandLineValues.Updates.AutoCheckUpdates;
        }

        public Updates_ Updates = new Updates_();

        public class Debug_
        {
            public bool ShowCallSource = false;
            // LINE: ignore
            public static bool ShowCallSource_ => SelfConfigRef.Value.Debug.ShowCallSource;

            public bool ShowDebug = false;
            // LINE: ignore
            public static bool ShowDebug_ => SelfConfigRef.Value.Debug.ShowDebug;

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