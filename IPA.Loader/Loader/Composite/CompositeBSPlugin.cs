using IPA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
    public class CompositeBSPlugin : IBeatSaberPlugin
    {
        IEnumerable<IBeatSaberPlugin> plugins;

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
            foreach (var plugin in plugins) {
                try {
                    plugin.OnSceneLoaded(scene, sceneMode);
                }
                catch (Exception ex) {
                    Logger.log.Error($"{plugin.Name}: {ex}");
                }
            }
        }

        public void OnSceneUnloaded(Scene scene) {
            foreach (var plugin in plugins) {
                try {
                    plugin.OnSceneUnloaded(scene);
                }
                catch (Exception ex) {
                    Logger.log.Error($"{plugin.Name}: {ex}");
                }
            }
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
            foreach (var plugin in plugins) {
                try {
                    plugin.OnActiveSceneChanged(prevScene, nextScene);
                }
                catch (Exception ex) {
                    Logger.log.Error($"{plugin.Name}: {ex}");
                }
            }
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

        public Uri UpdateUri => throw new NotImplementedException();

        public ModsaberModInfo ModInfo => throw new NotImplementedException();

        public void OnLateUpdate() {
            Invoke(plugin => {
                if (plugin is IEnhancedBeatSaberPlugin)
                    ((IEnhancedBeatSaberPlugin) plugin).OnLateUpdate();
            });
        }
    }
}