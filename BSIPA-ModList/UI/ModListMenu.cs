using CustomUI.BeatSaber;
using IPA.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BSIPA_ModList.UI
{
    internal class ModListMenu : CustomMenu
    {
        private ModListController controller;

#pragma warning disable CS0618
        public ModListMenu()
        {
            Logger.log.Debug("Menu constructor");

            controller = BeatSaberUI.CreateViewController<ModListController>();
            controller.Init(PluginManager.AllPlugins, PluginLoader.ignoredPlugins, PluginManager.Plugins);
            SetMainViewController(controller, true);
        }
#pragma warning restore
    }
}
