using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace IPA.Config
{
    internal static class ConfigRuntime
    {
        private static readonly ConcurrentBag<Config> configs = new ConcurrentBag<Config>();
        private static readonly AutoResetEvent memoryChangedWatcher = new AutoResetEvent(false);

        public static void RegisterConfig(Config cfg)
        {
            configs.Add(cfg);
            
            // TODO: register file watcher, reset changed watcher
        }

    }
}
