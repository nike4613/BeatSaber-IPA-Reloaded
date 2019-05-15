using IPA.Logging;
using IPA.Utilities;

namespace IPA.Config
{
    internal class SelfConfig
    {
        private static IConfigProvider _loaderConfig;

        private static void ConfigFileChangeDelegate(IConfigProvider configProvider, Ref<SelfConfig> selfConfigRef)
        {
            if (selfConfigRef.Value.Regenerate)
                configProvider.Store(selfConfigRef.Value = new SelfConfig { Regenerate = false });

            StandardLogger.Configure(selfConfigRef.Value);
        }
        public static IConfigProvider LoaderConfig
        {
            get => _loaderConfig;
            set
            {
                _loaderConfig?.RemoveLinks();
                value.Load();

                // This will set the instance reference to update to a
                // new instance every time the config file changes
                SelfConfigRef = value.MakeLink<SelfConfig>(ConfigFileChangeDelegate);
                _loaderConfig = value;
            }
        }

        public static Ref<SelfConfig> SelfConfigRef;

        public static void Set()
        {
            LoaderConfig = Config.GetProviderFor(IPAName, "json");
        }

        internal const string IPAName = "Beat Saber IPA";
        internal const string IPAVersion = "3.12.16"; 
		
        public bool Regenerate = true;

        public bool ApplyAntiYeet = false;

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
    }
}