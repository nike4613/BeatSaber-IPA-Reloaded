using IPA.Config;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using IPA.Logging;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NET4
using Task = System.Threading.Tasks.Task;
using TaskEx = System.Threading.Tasks.Task;
#endif
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Loader
{
    internal class DisabledConfig
    {
        public static Config.Config Disabled { get; set; }

        public static DisabledConfig Instance;

        public static void Load()
        {
            Disabled = Config.Config.GetConfigFor("Disabled Mods", "json");
            Instance = Disabled.Generated<DisabledConfig>();
        }

        public virtual bool Reset { get; set; } = true;

        [NonNullable]
        [UseConverter(typeof(CollectionConverter<string, HashSet<string>>))]
        public virtual HashSet<string> DisabledModIds { get; set; } = new HashSet<string>();

        protected internal virtual void Changed() { }
        protected internal virtual IDisposable ChangeTransaction() => null;

        private Task disableUpdateTask = null;
        private int updateState = 0;

        protected virtual void OnReload()
        {
            if (DisabledModIds == null || Reset)
            {
                DisabledModIds = new HashSet<string>();
                Reset = false;
            }

            if (!PluginLoader.IsFirstLoadComplete) return; // if the first load isn't complete, skip all of this

            var referToState = unchecked(++updateState);
            var copy = DisabledModIds.ToArray();
            if (disableUpdateTask == null || disableUpdateTask.IsCompleted)
            {
                disableUpdateTask = UpdateDisabledMods(copy);
            }
            else
            {
                disableUpdateTask = disableUpdateTask.ContinueWith(t =>
                {
                    // skip if another got here before the last finished
                    if (referToState != updateState) return TaskEx.WhenAll();
                    else return UpdateDisabledMods(copy);
                });
            }
        }

        private Task UpdateDisabledMods(string[] updateWithDisabled)
        {
            do
            {
                using var transaction = PluginManager.PluginStateTransaction();
                var disabled = transaction.DisabledPlugins.ToArray();
                foreach (var plugin in disabled)
                    transaction.Enable(plugin, autoDeps: true);

                var all = transaction.EnabledPlugins.ToArray();
                foreach (var plugin in all.Where(m => updateWithDisabled.Contains(m.Id)))
                    transaction.Disable(plugin, autoDependents: true);

                try
                {
                    if (transaction.WillNeedRestart)
                        Logger.loader.Warn("Runtime disabled config reload will need game restart to apply");
                    return transaction.Commit().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Logger.loader.Error("Error changing disabled plugins");
                            Logger.loader.Error(t.Exception);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
            }
            while (true);
        }
    }
}
