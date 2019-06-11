using IPA;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using BSIPA_ModList.UI;
using UnityEngine;
using IPA.Logging;
using BSIPA_ModList.UI.ViewControllers;
using System.Collections;
using IPA.Loader;

namespace BSIPA_ModList
{
    internal static class Logger
    {
        internal static IPALogger log { get; set; }

        internal static IPALogger md => log.GetChildLogger("MarkDown");
    }

    /// <summary>
    /// The main plugin type for the in-game mod list mod.
    /// </summary>
    internal class Plugin : IBeatSaberPlugin
    {
        /// <summary>
        /// Initializes the plugin with certain parameters. Is only called once.
        /// 
        /// This is called by the plugin loader in BSIPA, and thus must be <see langword="public"/>.
        /// </summary>
        /// <param name="logger">a logger to initialize the plugin with</param>
        public void Init(IPALogger logger)
        {
            Logger.log = logger;

            IPA.Updating.BeatMods.Updater.ModListPresent = true;

            // Load resources ahead of time
            MarkdownView.StartLoadResourcesAsync();

            SharedCoroutineStarter.instance.StartCoroutine(LoadPluginIcons());
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
        }

        private static IEnumerator LoadPluginIcons()
        {
            foreach (var p in PluginManager.AllPlugins)
            {
                yield return null;
                Logger.log.Debug($"Loading icon for {p.Metadata.Name}");
                var _ = p.Metadata.GetIcon();
            }
        }

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
            {
                FloatingNotification.Create();

                if (ButtonUI.Instance == null)
                    new GameObject("BSIPA Mod List Object").AddComponent<ButtonUI>().Init();
            }
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnUpdate()
        {
        }
    }
}
