using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
    internal class CompositeBSPlugin : IBeatSaberPlugin
    {
        private readonly IEnumerable<PluginLoader.PluginInfo> plugins;

        private delegate void CompositeCall(PluginLoader.PluginInfo plugin);
        
        public CompositeBSPlugin(IEnumerable<PluginLoader.PluginInfo> plugins) {
            this.plugins = plugins;
        }

        public void OnApplicationStart() {
            Invoke(plugin => plugin.Plugin.OnApplicationStart());
        }

        public void OnApplicationQuit() {
            Invoke(plugin => plugin.Plugin.OnApplicationQuit());
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) {
            Invoke(plugin => plugin.Plugin.OnSceneLoaded(scene, sceneMode));
        }

        public void OnSceneUnloaded(Scene scene) {
            Invoke(plugin => plugin.Plugin.OnSceneUnloaded(scene));
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
            Invoke(plugin => plugin.Plugin.OnActiveSceneChanged(prevScene, nextScene));
        }
        
        private void Invoke(CompositeCall callback) {
            foreach (var plugin in plugins) {
                try {
                    if (plugin.Plugin != null)
                        callback(plugin);
                }
                catch (Exception ex) {
                    Logger.log.Error($"{plugin.Metadata.Name}: {ex}");
                }
            }
        }
        
        public void OnUpdate() {
            Invoke(plugin => plugin.Plugin.OnUpdate());
        }

        public void OnFixedUpdate() {
            Invoke(plugin => plugin.Plugin.OnFixedUpdate());
        }

        public string Name => throw new InvalidOperationException();

        public string Version => throw new InvalidOperationException();

        public void OnLateUpdate() {
            Invoke(plugin => {
                if (plugin.Plugin is IEnhancedBeatSaberPlugin saberPlugin)
                    saberPlugin.OnLateUpdate();
            });
        }
    }
}