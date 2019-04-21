using IPA;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using BSIPA_ModList.UI;
using UnityEngine;

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

            IPA.Updating.BeatMods.Updater.ModListPresent = true;
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

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
            {
                if (ButtonUI.Instance == null)
                {
                    Logger.log.Debug("Creating Menu");
                    new GameObject("BSIPA Mod List Object").AddComponent<ButtonUI>().Init();
                }
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
