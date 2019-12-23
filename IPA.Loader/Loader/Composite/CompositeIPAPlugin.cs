using IPA.Old;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class CompositeIPAPlugin : Old.IPlugin
    {
        private readonly IEnumerable<Old.IPlugin> plugins;

        private delegate void CompositeCall(Old.IPlugin plugin);
        
        public CompositeIPAPlugin(IEnumerable<Old.IPlugin> plugins) 
        {
            this.plugins = plugins;
        }

        public void OnApplicationStart() 
        {
            Invoke(plugin => plugin.OnApplicationStart());
        }

        public void OnApplicationQuit() 
        {
            Invoke(plugin => plugin.OnApplicationQuit());
        }
        
        private void Invoke(CompositeCall callback, [CallerMemberName] string member = "") 
        {
            foreach (var plugin in plugins) 
            {
                try 
                {
                    callback(plugin);
                }
                catch (Exception ex) 
                {
                    Logger.log.Error($"{plugin.Name} {member}: {ex}");
                }
            }
        }

        public void OnUpdate() 
        {
            Invoke(plugin => plugin.OnUpdate());
        }

        public void OnFixedUpdate() 
        {
            Invoke(plugin => plugin.OnFixedUpdate());
        }
        
        public string Name => throw new InvalidOperationException();

        public string Version => throw new InvalidOperationException();

        public void OnLateUpdate() 
        {
            Invoke(plugin => {
                if (plugin is Old.IEnhancedPlugin saberPlugin)
                    saberPlugin.OnLateUpdate();
            });
        }

        public void OnLevelWasLoaded(int level)
        {
            Invoke(plugin => plugin.OnLevelWasLoaded(level));
        }

        public void OnLevelWasInitialized(int level)
        {
            Invoke(plugin => plugin.OnLevelWasInitialized(level));
        }
    }
#pragma warning restore CS0618 // Type or member is obsolete
}