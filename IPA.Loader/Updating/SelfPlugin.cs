using IPA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace IPA.Updating
{
    internal class SelfPlugin : IBeatSaberPlugin
    {
        internal const string IPA_Name = "Beat Saber IPA";
        internal const string IPA_Version = "3.9.0";

        public string Name => IPA_Name;

        public string Version => IPA_Version;

        public ModsaberModInfo ModInfo => new ModsaberModInfo
        {
            CurrentVersion = new Version(IPA_Version),
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
