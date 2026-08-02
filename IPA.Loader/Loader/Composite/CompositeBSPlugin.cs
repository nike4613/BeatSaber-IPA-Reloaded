using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader.Composite
{
    internal class CompositeBSPlugin
    {
        private readonly IEnumerable<PluginExecutor> plugins;

        private delegate Task CompositeCall(PluginExecutor plugin);

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
                    {
                        callback(plugin).ContinueWith(t =>
                        {
                            Logger.Default.Error($"{plugin.Metadata.Name} {method}: {t.Exception!.InnerException}");
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Default.Error($"{plugin.Metadata.Name} {method}: {ex}");
                }
            }
        }

        public void OnEnable()
            => Invoke(plugin => plugin.Enable());

        public void OnDisable() // do something useful with the Task that Disable gives us
             => Invoke(plugin => plugin.Disable());
    }
}