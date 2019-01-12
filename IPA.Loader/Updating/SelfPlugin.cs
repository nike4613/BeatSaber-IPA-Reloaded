using IPA.Config;
using System;
using UnityEngine.SceneManagement;

namespace IPA.Updating
{
    [Obsolete("Only used for old updating system, replaced with a PluginMeta for teh embedded manifest")]
    internal class SelfPlugin : IBeatSaberPlugin
    {
        public static SelfPlugin Instance { get; set; } = new SelfPlugin();

        public string Name => SelfConfig.IPA_Name;

        public string Version => SelfConfig.IPA_Version;

        public ModsaberModInfo ModInfo => new ModsaberModInfo
        {
            CurrentVersion = SelfConfig.IPA_Version,
            InternalName = "beatsaber-ipa-reloaded"
        };

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
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnUpdate()
        {
        }
    }
}