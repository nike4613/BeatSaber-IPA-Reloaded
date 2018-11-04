using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
    internal class CompositeBSPlugin : IBeatSaberPlugin
    {
        private readonly IEnumerable<IBeatSaberPlugin> plugins;

        private delegate void CompositeCall(IBeatSaberPlugin plugin);
        
        public CompositeBSPlugin(IEnumerable<IBeatSaberPlugin> plugins) {
            this.plugins = plugins;
        }

        public void OnApplicationStart() {
            Invoke(plugin => plugin.OnApplicationStart());
        }

        public void OnApplicationQuit() {
            Invoke(plugin => plugin.OnApplicationQuit());
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) {
            Invoke(plugin => plugin.OnSceneLoaded(scene, sceneMode));
        }

        public void OnSceneUnloaded(Scene scene) {
            Invoke(plugin => plugin.OnSceneUnloaded(scene));
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
            Invoke(plugin => plugin.OnActiveSceneChanged(prevScene, nextScene));
        }
        
        private void Invoke(CompositeCall callback) {
            foreach (var plugin in plugins) {
                try {
                    callback(plugin);
                }
                catch (Exception ex) {
                    Logger.log.Error($"{plugin.Name}: {ex}");
                }
            }
        }
        
        public void OnUpdate() {
            Invoke(plugin => plugin.OnUpdate());
        }

        public void OnFixedUpdate() {
            Invoke(plugin => plugin.OnFixedUpdate());
        }

        public string Name => throw new NotImplementedException();

        public string Version => throw new NotImplementedException();

        public ModsaberModInfo ModInfo => throw new NotImplementedException();

        public void OnLateUpdate() {
            Invoke(plugin => {
                if (plugin is IEnhancedBeatSaberPlugin saberPlugin)
                    saberPlugin.OnLateUpdate();
            });
        }
    }
}