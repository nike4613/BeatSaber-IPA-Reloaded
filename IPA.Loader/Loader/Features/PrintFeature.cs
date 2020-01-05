
using IPA.Logging;

namespace IPA.Loader.Features
{
    internal class PrintFeature : Feature
    {
        public override bool Initialize(PluginMetadata meta, string[] parameters)
        {
            Logger.features.Info($"{meta.Name}: {string.Join(" ", parameters)}");
            return true;
        }
    }

    internal class DebugFeature : Feature
    {
        public override bool Initialize(PluginMetadata meta, string[] parameters)
        {
            Logger.features.Debug($"{meta.Name}: {string.Join(" ", parameters)}");
            return true;
        }
    }

    internal class WarnFeature : Feature
    {
        public override bool Initialize(PluginMetadata meta, string[] parameters)
        {
            Logger.features.Warn($"{meta.Name}: {string.Join(" ", parameters)}");
            return true;
        }
    }
}
