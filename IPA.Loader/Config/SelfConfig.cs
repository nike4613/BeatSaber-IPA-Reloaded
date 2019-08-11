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
		
        // END: section ignore

        public bool Regenerate = true;

        public class UpdateObject
        {
            public bool AutoUpdate = true;
            public bool AutoCheckUpdates = true;
        }

        public UpdateObject Updates = new UpdateObject();

        public class DebugObject
        {
            public bool ShowCallSource = false;
            public bool ShowDebug = false;
            public bool ShowHandledErrorStackTraces = false;
            public bool HideMessagesForPerformance = true;
            public int HideLogThreshold = 512;
        }

        public DebugObject Debug = new DebugObject();

        [JsonProperty(Required = Required.Default)]
        public string LastGameVersion = null;
    }
}