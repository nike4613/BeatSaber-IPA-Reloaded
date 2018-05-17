using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = IllusionPlugin.Logger;

namespace IllusionInjector {
    public class CompositePlugin : IPlugin {
        IEnumerable<IPlugin> plugins;

        private delegate void CompositeCall(IPlugin plugin);

        private Logger debugLogger => PluginManager.debugLogger;
        
        public CompositePlugin(IEnumerable<IPlugin> plugins) {
            this.plugins = plugins;
        }

        public void OnApplicationStart() {
            Invoke(plugin => plugin.OnApplicationStart());
        }

        public void OnApplicationQuit() {
            Invoke(plugin => plugin.OnApplicationQuit());
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) {
            foreach (var plugin1 in plugins.Where(o => o is IPluginNew)) {
                var plugin = (IPluginNew) plugin1;
                try {
                    plugin.OnSceneLoaded(scene, sceneMode);
                }
                catch (Exception ex) {
                    debugLogger.Exception($"{plugin.Name}: {ex}");
                }
            }
        }

        public void OnSceneUnloaded(Scene scene) {
            foreach (var plugin1 in plugins.Where(o => o is IPluginNew)) {
                var plugin = (IPluginNew) plugin1;
                try {
                    plugin.OnSceneUnloaded(scene);
                }
                catch (Exception ex) {
                    debugLogger.Exception($"{plugin.Name}: {ex}");
                }
            }
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
            foreach (var plugin1 in plugins.Where(o => o is IPluginNew)) {
                var plugin = (IPluginNew) plugin1;
                try {
                    plugin.OnActiveSceneChanged(prevScene, nextScene);
                }
                catch (Exception ex) {
                    debugLogger.Exception($"{plugin.Name}: {ex}");
                }
            }
        }


        private void Invoke(CompositeCall callback) {
            foreach (var plugin in plugins) {
                try {
                    callback(plugin);
                }
                catch (Exception ex) {
                    debugLogger.Exception($"{plugin.Name}: {ex}");
                }
            }
        }


        public void OnUpdate() {
            Invoke(plugin => plugin.OnUpdate());
        }

        public void OnFixedUpdate() {
            Invoke(plugin => plugin.OnFixedUpdate());
        }

        [Obsolete("Use OnSceneLoaded instead")]
        public void OnLevelWasLoaded(int level)
        {
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.OnLevelWasLoaded(level);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: {1}", plugin.Name, ex);
                }
            }
        }

        [Obsolete("Use OnSceneLoaded instead")]
        public void OnLevelWasInitialized(int level)
        {
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.OnLevelWasInitialized(level);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: {1}", plugin.Name, ex);
                }
            }
        }


        public string Name {
            get { throw new NotImplementedException(); }
        }

        public string Version {
            get { throw new NotImplementedException(); }
        }

        public void OnLateUpdate() {
            Invoke(plugin => {
                if (plugin is IEnhancedPlugin)
                    ((IEnhancedPlugin) plugin).OnLateUpdate();
            });
        }
    }
}