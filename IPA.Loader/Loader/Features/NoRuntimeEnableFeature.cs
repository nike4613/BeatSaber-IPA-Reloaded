using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Loader.Features
{
    internal class NoRuntimeEnableFeature : Feature
    {
        internal static bool HaveLoadedPlugins = false;

        public override bool Initialize(PluginLoader.PluginMetadata meta, string[] parameters)
        {
            return parameters.Length == 0;
        }

        public override bool BeforeLoad(PluginLoader.PluginMetadata plugin)
        {
            return !HaveLoadedPlugins;
        }

        public override string InvalidMessage 
        { 
            get => "Plugin requested to not be loaded after initial plugin load"; 
            protected set { }
        }
    }
}
