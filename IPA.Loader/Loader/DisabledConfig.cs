using IPA.Config;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
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

        public virtual bool Reset { get; set; } = true;

        [NonNullable]
        [UseConverter(typeof(CollectionConverter<string, HashSet<string>>))]
        public virtual HashSet<string> DisabledModIds { get; set; } = new HashSet<string>();

        protected internal virtual void Changed() { }
        protected internal virtual IDisposable ChangeTransaction() => null;

        protected virtual void OnReload()
        {
            if (DisabledModIds == null || Reset)
            {
                DisabledModIds = new HashSet<string>();
                Reset = false;
            }
        }
    }
}
