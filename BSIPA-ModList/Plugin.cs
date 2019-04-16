using IPA;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using CustomUI.BeatSaber;
using BSIPA_ModList.UI;
using CustomUI.MenuButton;
using UnityEngine.Events;
using UnityEngine;
using System.Linq;

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

        private MainFlowCoordinator mainFlow;
        private ModListFlowCoordinator menuFlow;
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
            if (scene.name == "MenuCore")
            {
                if (mainFlow == null)
                    mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                if (menuFlow == null)
                    menuFlow = new GameObject("BSIPA Mod List Flow Coordinator").AddComponent<ModListFlowCoordinator>();
                if (button == null)
                    button = MenuButtonUI.AddButton("Mod List", "Look at installed mods, and control updating", () =>
                    {
                        Logger.log.Debug("Presenting own flow controller");
                        menuFlow.PresentOn(mainFlow);
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
