using CustomUI.BeatSaber;
using CustomUI.Utilities;
using IPA.Loader;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRUI;

namespace BSIPA_ModList.UI
{
    internal class ModListFlowCoordinator : FlowCoordinator
    {
        private BackButtonNavigationController navigationController;
        private ModListController modList;

#pragma warning disable CS0618
        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        { // thx Caeden
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                title = "Installed Mods";

                navigationController = BeatSaberUI.CreateViewController<BackButtonNavigationController>();
                navigationController.didFinishEvent += backButton_DidFinish;

                modList = BeatSaberUI.CreateViewController<ModListController>();
                modList.Init(navigationController, PluginManager.AllPlugins, PluginLoader.ignoredPlugins, PluginManager.Plugins);

                PushViewControllerToNavigationController(navigationController, modList);
            }

            ProvideInitialViewControllers(navigationController);
        }
#pragma warning restore

        private delegate void PresentFlowCoordDel(FlowCoordinator self, FlowCoordinator newF, Action finished, bool immediate, bool replaceTop);
        private static PresentFlowCoordDel presentFlow;

        public void PresentOn(FlowCoordinator main, Action finished = null, bool immediate = false, bool replaceTop = false)
        {
            if (presentFlow == null)
            {
                var ty = typeof(FlowCoordinator);
                var m = ty.GetMethod("PresentFlowCoordinator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                presentFlow = (PresentFlowCoordDel)Delegate.CreateDelegate(typeof(PresentFlowCoordDel), m);
            }

            presentFlow(main, this, finished, immediate, replaceTop);
        }

        private void backButton_DidFinish()
        {
            MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            mainFlow.InvokeMethod("DismissFlowCoordinator", this, null, false);
        }
    }
}
