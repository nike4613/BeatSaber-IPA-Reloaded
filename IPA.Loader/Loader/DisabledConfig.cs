using IPA.Config;
using IPA.Config.Stores;
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
        public static Config.Config Disabled { get; set; }

        public static DisabledConfig Instance;

        public static void Load()
        {
            Disabled = Config.Config.GetConfigFor("Disabled Mods", "json");
            Instance = Disabled.Generated<DisabledConfig>();
        }

        public bool Reset = true;

        public HashSet<string> DisabledModIds = new HashSet<string>();
    }
}
