using IPA.Config;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Loader
{
    internal class DisabledConfig
    {
        private static IConfigProvider _provider;

        public static IConfigProvider Provider
        {
            get => _provider;
            set
            {
                _provider?.RemoveLinks();
                value.Load();
                Ref = value.MakeLink<DisabledConfig>((c, v) =>
                {
                    if (v.Value.Reset)
                        c.Store(v.Value = new DisabledConfig { Reset = false });
                });
                _provider = value;
            }
        }

        public static Ref<DisabledConfig> Ref;

        public static void Load()
        {
            Provider = Config.Config.GetProviderFor("Disabled Mods", "json");
        }

        public bool Reset = true;

        public HashSet<string> DisabledModIds = new HashSet<string>();
    }
}
