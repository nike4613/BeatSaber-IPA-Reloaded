
using IPA.Logging;

namespace IPA.Loader.Features
{
    internal class PrintFeature : Feature
    {
        public override bool Initialize(PluginLoader.PluginMetadata meta, string[] parameters)
        {
            Logger.features.Info($"{meta.Name}: {string.Join(" ", parameters)}");
            return true;
        }
    }
}
