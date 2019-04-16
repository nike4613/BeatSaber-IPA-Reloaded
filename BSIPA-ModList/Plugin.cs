using IPA;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using CustomUI.BeatSaber;
using BSIPA_ModList.UI;
using CustomUI.MenuButton;
using UnityEngine.Events;

namespace BSIPA_ModList
{
    internal static class Logger
    {
        internal static IPALogger log { get; set; }
    }

    public class Plugin : IBeatSaberPlugin
    {
        public void Init(IPALogger logger)
        {
            Logger.log = logger;
            Logger.log.Debug("Init");
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        public void OnApplicationQuit()
        {
        }

        private ModListMenu menu;
        private MenuButton button;

        public void OnApplicationStart()
        {
            Logger.log.Debug("Creating Menu");
        }

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore" && button == null)
            {
                menu = BeatSaberUI.CreateCustomMenu<ModListMenu>("Installed Mods");
                button = MenuButtonUI.AddButton("All Mods", "Shows all installed mods, along with controls for updating them.", () =>
                {
                    Logger.log.Debug("Presenting menu");
                    menu.Present();
                });
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
