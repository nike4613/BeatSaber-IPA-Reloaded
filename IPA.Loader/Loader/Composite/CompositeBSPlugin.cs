using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
    internal class CompositeBSPlugin
    {
        private readonly IEnumerable<PluginExecutor> plugins;

        private delegate void CompositeCall(PluginExecutor plugin);
        
        public CompositeBSPlugin(IEnumerable<PluginExecutor> plugins) 
        {
            this.plugins = plugins;
        }
        private void Invoke(CompositeCall callback, [CallerMemberName] string method = "")
        {
            foreach (var plugin in plugins)
            {
                try
                {
                    if (plugin != null)
                        callback(plugin);
                }
                catch (Exception ex)
                {
                    Logger.log.Error($"{plugin.Metadata.Name} {method}: {ex}");
                }
            }
        }

        public void OnEnable()
            => Invoke(plugin => plugin.Enable());

        public void OnApplicationQuit() // do something useful with the Task that Disable gives us
             => Invoke(plugin => plugin.Disable());

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        { }//=> Invoke(plugin => plugin.Plugin.OnSceneLoaded(scene, sceneMode));

        public void OnSceneUnloaded(Scene scene)
        { }//=> Invoke(plugin => plugin.Plugin.OnSceneUnloaded(scene));

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        { }//=> Invoke(plugin => plugin.Plugin.OnActiveSceneChanged(prevScene, nextScene));

        public void OnUpdate()
        { }/*=> Invoke(plugin =>
            {
                if (plugin.Plugin is IEnhancedPlugin saberPlugin)
                    saberPlugin.OnUpdate();
            });*/

        public void OnFixedUpdate()
        { }/*=> Invoke(plugin => {
                if (plugin.Plugin is IEnhancedPlugin saberPlugin)
                    saberPlugin.OnFixedUpdate();
            });*/

        public void OnLateUpdate()
        { }/*=> Invoke(plugin => {
                if (plugin.Plugin is IEnhancedPlugin saberPlugin)
                    saberPlugin.OnLateUpdate();
            });*/
    }
}