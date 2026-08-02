using IPA.Config;
using IPA.Loader.Features;
using IPA.Utilities;
using IPA.Utilities.Async;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = IPA.Logging.Logger;

namespace IPA.Loader
{
    /// <summary>
    /// The manager class for all plugins.
    /// </summary>
    public static class PluginManager
    {
        private static List<PluginExecutor> _bsPlugins;
        internal static IEnumerable<PluginExecutor> BSMetas => _bsPlugins;

        /// <summary>
        /// Gets info about the enabled plugin with the specified name.
        /// </summary>
        /// <param name="name">the name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin metadata for the requested plugin or <see langword="null"/> if it doesn't exist or is disabled</returns>
        public static PluginMetadata GetPlugin(string name)
            => BSMetas.Select(p => p.Metadata).FirstOrDefault(p => p.Name == name);

        /// <summary>
        /// Gets info about the enabled plugin with the specified ID.
        /// </summary>
        /// <param name="id">the ID name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin metadata for the requested plugin or <see langword="null"/> if it doesn't exist or is disabled</returns>
        public static PluginMetadata GetPluginFromId(string id)
            => BSMetas.Select(p => p.Metadata).FirstOrDefault(p => p.Id == id);

        /// <summary>
        /// Gets a disabled plugin's metadata by its name.
        /// </summary>
        /// <param name="name">the name of the disabled plugin to get</param>
        /// <returns>the metadata for the corresponding plugin</returns>
        public static PluginMetadata GetDisabledPlugin(string name) =>
            DisabledPlugins.FirstOrDefault(p => p.Name == name);

        /// <summary>
        /// Gets a disabled plugin's metadata by its ID.
        /// </summary>
        /// <param name="id">the ID of the disabled plugin to get</param>
        /// <returns>the metadata for the corresponding plugin</returns>
        public static PluginMetadata GetDisabledPluginFromId(string id) =>
            DisabledPlugins.FirstOrDefault(p => p.Id == id);

        /// <summary>
        /// Creates a new transaction for mod enabling and disabling mods simultaneously.
        /// </summary>
        /// <returns>a new <see cref="StateTransitionTransaction"/> that captures the current state of loaded mods</returns>
        public static StateTransitionTransaction PluginStateTransaction()
            => new StateTransitionTransaction(EnabledPlugins, DisabledPlugins);

        private static readonly object commitTransactionLockObject = new object();

        internal static Task CommitTransaction(StateTransitionTransaction transaction)
        {
            if (!transaction.HasStateChanged) return Task.CompletedTask;

            if (!UnityGame.OnMainThread)
            {
                var transactionCopy = transaction.Clone();
                transaction.Dispose();
                return UnityMainThreadTaskScheduler.Factory.StartNew(() => CommitTransaction(transactionCopy)).Unwrap();
            }

            lock (commitTransactionLockObject)
            {
                if (transaction.CurrentlyEnabled.Except(EnabledPlugins)
                               .Concat(EnabledPlugins.Except(transaction.CurrentlyEnabled)).Any()
                 || transaction.CurrentlyDisabled.Except(DisabledPlugins)
                               .Concat(DisabledPlugins.Except(transaction.CurrentlyDisabled)).Any())
                { // ensure that the transaction's base state reflects the current state, otherwise throw
                    transaction.Dispose();
                    throw new InvalidOperationException("Transaction no longer resembles the current state of plugins");
                }

                var toEnable = transaction.ToEnable;
                var toDisable = transaction.ToDisable;
                transaction.Dispose();

                using var disabledChangeTransaction = DisabledConfig.Instance.ChangeTransaction();
                {
                    // first enable the mods that need to be
                    void DeTree(List<PluginMetadata> into, IEnumerable<PluginMetadata> tree)
                    {
                        foreach (var st in tree)
                            if (toEnable.Contains(st) && !into.Contains(st))
                            {
                                DeTree(into, st.Dependencies);
                                into.Add(st);
                            }
                    }

                    var enableOrder = new List<PluginMetadata>();
                    DeTree(enableOrder, toEnable);

                    foreach (var meta in enableOrder)
                    {
                        var executor = runtimeDisabledPlugins.FirstOrDefault(e => e.Metadata == meta);
                        if (meta.RuntimeOptions == RuntimeOptions.DynamicInit)
                        {
                            if (executor != null)
                                runtimeDisabledPlugins.Remove(executor);
                            else
                                executor = PluginLoader.InitPlugin(meta, EnabledPlugins);

                            if (executor == null) continue; // couldn't initialize, skip to next
                        }

                        DisabledConfig.Instance.DisabledModIds.Remove(meta.Id ?? meta.Name);

                        PluginEnabled?.Invoke(meta, meta.RuntimeOptions != RuntimeOptions.DynamicInit);

                        if (meta.RuntimeOptions == RuntimeOptions.DynamicInit)
                        {
                            // it should only be marked as not disabled if it actually was
                            PluginLoader.DisabledPlugins.Remove(meta);
                            _bsPlugins.Add(executor);

                            try
                            {
                                executor.Enable();
                            }
                            catch (Exception e)
                            {
                                Logger.Loader.Error($"Error while enabling {meta.Id}:");
                                Logger.Loader.Error(e);
                                // this should still be considered enabled, hence its position
                            }
                        }
                    }
                }

                var result = Task.CompletedTask;
                {
                    // then disable the mods that need to be
                    static DisableExecutor MakeDisableExec(PluginExecutor e)
                        => new DisableExecutor
                        {
                            Executor = e,
                            Dependents = BSMetas.Where(f => f.Metadata.Dependencies.Contains(e.Metadata)).Select(MakeDisableExec)
                        };

                    var disableExecs = toDisable.Select(m => BSMetas.FirstOrDefault(e => e.Metadata == m)).NonNull().ToArray(); // eagerly evaluate once

                    foreach (var exec in disableExecs)
                    {
                        DisabledConfig.Instance.DisabledModIds.Add(exec.Metadata.Id ?? exec.Metadata.Name);
                        if (exec.Metadata.RuntimeOptions == RuntimeOptions.DynamicInit)
                        {
                            // it should only be marked as disabled if it was actually fully disabled
                            PluginLoader.DisabledPlugins.Add(exec.Metadata);
                            runtimeDisabledPlugins.Add(exec);
                            _bsPlugins.Remove(exec);
                        }

                        PluginDisabled?.Invoke(exec.Metadata, exec.Metadata.RuntimeOptions != RuntimeOptions.DynamicInit);
                    }

                    var disableStructure = disableExecs.Select(MakeDisableExec);

                    static Task Disable(DisableExecutor exec, Dictionary<PluginExecutor, Task> alreadyDisabled)
                    {
                        if (alreadyDisabled.TryGetValue(exec.Executor, out var task))
                            return task;
                        else
                        {
                            if (exec.Executor.Metadata.RuntimeOptions != RuntimeOptions.DynamicInit)
                                return Task.FromException(new CannotRuntimeDisableException(exec.Executor.Metadata));

                            var res = Task.WhenAll(exec.Dependents.Select(d => Disable(d, alreadyDisabled)))
                                 .ContinueWith(t =>
                                 {
                                     if (t.IsFaulted)
                                     {
                                         return Task.WhenAll(t, Task.FromException(
                                             new CannotRuntimeDisableException(exec.Executor.Metadata, "Dependents cannot be disabled for plugin")));
                                     }
                                     return exec.Executor.Disable()
                                        .ContinueWith(t =>
                                        {
                                            foreach (var feature in exec.Executor.Metadata.Features)
                                            {
                                                try
                                                {
                                                    feature.AfterDisable(exec.Executor.Metadata);
                                                }
                                                catch (Exception e)
                                                {
                                                    Logger.Loader.Critical($"Feature errored in {nameof(Feature.AfterDisable)}: {e}");
                                                }
                                            }
                                        }, UnityMainThreadTaskScheduler.Default);
                                 }, UnityMainThreadTaskScheduler.Default).Unwrap();
                            // We do not want to call the disable method if a dependent couldn't be disabled
                            // By scheduling on a UnityMainThreadScheduler, we ensure that Disable() is always called on the Unity main thread
                            alreadyDisabled.Add(exec.Executor, res);
                            return res;
                        }
                    }

                    var disabled = new Dictionary<PluginExecutor, Task>();
                    result = Task.WhenAll(disableStructure.Select(d => Disable(d, disabled)));
                }

                OnAnyPluginsStateChanged?.Invoke(result, toEnable, toDisable);
                // if there are any that are capable of enabling/disabling at runtime, run event handler
                if (toEnable.Concat(toDisable).Any(m => m.RuntimeOptions == RuntimeOptions.DynamicInit))
                    OnPluginsStateChanged?.Invoke(result);

                //DisabledConfig.Instance.Changed();
                // changed is handled by transaction
                return result;
            }
        }

        private struct DisableExecutor
        {
            public PluginExecutor Executor;
            public IEnumerable<DisableExecutor> Dependents;
        }

        /// <summary>
        /// Checks if a given plugin is disabled.
        /// </summary>
        /// <param name="meta">the plugin to check</param>
        /// <returns><see langword="true"/> if the plugin is disabled, <see langword="false"/> otherwise.</returns>
        public static bool IsDisabled(PluginMetadata meta) => DisabledPlugins.Contains(meta);

        /// <summary>
        /// Checks if a given plugin is enabled.
        /// </summary>
        /// <param name="meta">the plugin to check</param>
        /// <returns><see langword="true"/> if the plugin is enabled, <see langword="false"/> otherwise.</returns>
        public static bool IsEnabled(PluginMetadata meta) => BSMetas.Any(p => p.Metadata == meta);


        /// <summary>
        /// An invoker for the <see cref="PluginEnabled"/> event.
        /// </summary>
        /// <param name="plugin">the plugin that was enabled</param>
        /// <param name="needsRestart">whether it needs a restart to take effect</param>
        public delegate void PluginEnableDelegate(PluginMetadata plugin, bool needsRestart);
        /// <summary>
        /// An invoker for the <see cref="PluginDisabled"/> event.
        /// </summary>
        /// <param name="plugin">the plugin that was disabled</param>
        /// <param name="needsRestart">whether it needs a restart to take effect</param>
        public delegate void PluginDisableDelegate(PluginMetadata plugin, bool needsRestart);
        /// <summary>
        /// A delegate representing a state change event for any plugin.
        /// </summary>
        /// <param name="changeTask">the <see cref="Task"/> representing the change</param>
        /// <param name="enabled">the plugins that were enabled in the change</param>
        /// <param name="disabled">the plugins that were disabled in the change</param>
        public delegate void OnAnyPluginsStateChangedDelegate(Task changeTask, IEnumerable<PluginMetadata> enabled, IEnumerable<PluginMetadata> disabled);

        /// <summary>
        /// Called whenever a plugin is enabled, before the plugin in question is enabled.
        /// </summary>
        public static event PluginEnableDelegate PluginEnabled;
        /// <summary>
        /// Called whenever a plugin is disabled, before the plugin in question is enabled.
        /// </summary>
        public static event PluginDisableDelegate PluginDisabled;
        /// <summary>
        /// Called whenever any plugins have their state changed at runtime with the <see cref="Task"/> representing that state change.
        /// </summary>
        /// <remarks>
        /// Note that this is called on the Unity main thread, and cannot therefore block, as the <see cref="Task"/>
        /// provided represents operations that also run on the Unity main thread.
        /// </remarks>
        public static event Action<Task> OnPluginsStateChanged;
        /// <summary>
        /// Called whenever any plugins, regardless of whether or not their change occurs during runtime, have their state changed.
        /// </summary>
        /// <remarks>
        /// Note that this is called on the Unity main thread, and cannot therefore block, as the <see cref="Task"/>
        /// provided represents operations that also run on the Unity main thread.
        /// </remarks>
        public static event OnAnyPluginsStateChangedDelegate OnAnyPluginsStateChanged;

        /// <summary>
        /// Gets a collection of all enabled plugins, as represented by <see cref="PluginMetadata"/>.
        /// </summary>
        /// <value>a collection of all enabled plugins</value>
        public static IEnumerable<PluginMetadata> EnabledPlugins => BSMetas.Select(p => p.Metadata);
        /// <summary>
        /// Gets a list of disabled BSIPA plugins.
        /// </summary>
        /// <value>a collection of all disabled plugins as <see cref="PluginMetadata"/></value>
        public static IEnumerable<PluginMetadata> DisabledPlugins => PluginLoader.DisabledPlugins;
        private static readonly HashSet<PluginExecutor> runtimeDisabledPlugins = new HashSet<PluginExecutor>();

        /// <summary>
        /// Gets a read-only dictionary of an ignored plugin to the reason it was ignored, as an <see cref="IgnoreReason"/>.
        /// </summary>
        /// <value>a dictionary of <see cref="PluginMetadata"/> to <see cref="IgnoreReason"/> of ignored plugins</value>
        public static IReadOnlyDictionary<PluginMetadata, IgnoreReason> IgnoredPlugins => PluginLoader.ignoredPlugins;

        internal static IConfigProvider SelfConfigProvider { get; set; }

        internal static void Load()
        {
            string pluginDirectory = UnityGame.PluginsPath;

            // Process.GetCurrentProcess().MainModule crashes the game and Assembly.GetEntryAssembly() is NULL,
            // so we need to resort to P/Invoke
            string exeName = Path.GetFileNameWithoutExtension(AppInfo.StartupPath);
            _bsPlugins = new List<PluginExecutor>();

            if (!Directory.Exists(pluginDirectory)) return;

            var sw = Stopwatch.StartNew();

            PluginLoader.LoadPlugins(_bsPlugins);

            sw.Stop();

            Logger.Default.Info(exeName);
            Logger.Default.Info($"Running on Unity {Application.unityVersion}");
            Logger.Default.Info($"Game version {UnityGame.GameVersion}");
            Logger.Default.Info("-----------------------------");
            Logger.Default.Info($"Loading plugins from {Utils.GetRelativePath(pluginDirectory, Environment.CurrentDirectory)} and found {_bsPlugins.Count}");
            Logger.Default.Info("-----------------------------");
            foreach (var plugin in _bsPlugins)
            {
                Logger.Default.Info($"{plugin.Metadata.Name} ({plugin.Metadata.Id}): {plugin.Metadata.Version}");
            }
            Logger.Default.Info("-----------------------------");
            Logger.Default.Info($"Initializing plugins took {sw.Elapsed}");
        }

        internal static class AppInfo
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = false)]
            private static extern int GetModuleFileName(HandleRef hModule, StringBuilder buffer, int length);
            private static HandleRef NullHandleRef = new HandleRef(null, IntPtr.Zero);
            public static string StartupPath
            {
                get
                {
                    StringBuilder stringBuilder = new StringBuilder(260);
                    GetModuleFileName(NullHandleRef, stringBuilder, stringBuilder.Capacity);
                    return stringBuilder.ToString();
                }
            }
        }
    }
}
