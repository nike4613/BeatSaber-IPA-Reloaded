using IPA.Logging;
using IPA.Utilities;

namespace IPA.Config
{
    internal class SelfConfig
    {
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

        public static void Set()
        {
            LoaderConfig = Config.GetProviderFor(IPA_Name, "json");
        }

        internal const string IPA_Name = "Beat Saber IPA";
        internal const string IPA_Version = "3.12.0";

        public bool Regenerate = true;

        public class DebugObject
        {
            public bool ShowCallSource = false;
            public bool ShowDebug = false;
        }

        public DebugObject Debug = new DebugObject();
    }
}