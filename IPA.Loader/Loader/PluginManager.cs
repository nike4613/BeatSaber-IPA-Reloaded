using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using IPA.Config;
using IPA.Old;
using IPA.Utilities;
using Mono.Cecil;
using UnityEngine;
using Logger = IPA.Logging.Logger;
using System.Threading.Tasks;
using IPA.Utilities.Async;
#if NET4
using TaskEx = System.Threading.Tasks.Task;
using TaskEx6 = System.Threading.Tasks.Task;
using Task = System.Threading.Tasks.Task;
#endif
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// The manager class for all plugins.
    /// </summary>
    public static class PluginManager
    {
#pragma warning disable CS0618 // Type or member is obsolete (IPlugin)
        
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
        /// <param name="name">the ID name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin metadata for the requested plugin or <see langword="null"/> if it doesn't exist or is disabled</returns>
        public static PluginMetadata GetPluginFromId(string name)
            => BSMetas.Select(p => p.Metadata).FirstOrDefault(p => p.Id == name);

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
        /// <param name="name">the ID of the disabled plugin to get</param>
        /// <returns>the metadata for the corresponding plugin</returns>
        public static PluginMetadata GetDisabledPluginFromId(string name) =>
            DisabledPlugins.FirstOrDefault(p => p.Id == name);

        /// <summary>
        /// Creates a new transaction for mod enabling and disabling mods simultaneously.
        /// </summary>
        /// <returns>a new <see cref="StateTransitionTransaction"/> that captures the current state of loaded mods</returns>
        public static StateTransitionTransaction PluginStateTransaction()
            => new StateTransitionTransaction(EnabledPlugins, DisabledPlugins);

        private static readonly object commitTransactionLockObject = new object();

        internal static Task CommitTransaction(StateTransitionTransaction transaction)
        {
            if (!transaction.HasStateChanged) return TaskEx.WhenAll();

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

                        PluginLoader.DisabledPlugins.Remove(meta);
                        DisabledConfig.Instance.DisabledModIds.Remove(meta.Id ?? meta.Name);

                        PluginEnabled?.Invoke(meta, meta.RuntimeOptions != RuntimeOptions.DynamicInit);

                        if (meta.RuntimeOptions == RuntimeOptions.DynamicInit)
                        {
                            _bsPlugins.Add(executor);

                            try
                            {
                                executor.Enable();
                            }
                            catch (Exception e)
                            {
                                Logger.loader.Error($"Error while enabling {meta.Id}:");
                                Logger.loader.Error(e);
                                // this should still be considered enabled, hence its position
                            }
                        }
                    }
                }

                var result = TaskEx.WhenAll();
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
                        PluginLoader.DisabledPlugins.Add(exec.Metadata);
                        DisabledConfig.Instance.DisabledModIds.Add(exec.Metadata.Id ?? exec.Metadata.Name);
                        if (exec.Metadata.RuntimeOptions == RuntimeOptions.DynamicInit)
                        {
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
                                return TaskEx6.FromException(new CannotRuntimeDisableException(exec.Executor.Metadata));

                            var res = TaskEx.WhenAll(exec.Dependents.Select(d => Disable(d, alreadyDisabled)))
                                 .ContinueWith(t => t.IsFaulted 
                                    ? TaskEx.WhenAll(t, TaskEx6.FromException(
                                        new CannotRuntimeDisableException(exec.Executor.Metadata, "Dependents cannot be disabled for plugin"))) 
                                    : exec.Executor.Disable(), UnityMainThreadTaskScheduler.Default).Unwrap();
                            // We do not want to call the disable method if a dependent couldn't be disabled
                            // By scheduling on a UnityMainThreadScheduler, we ensure that Disable() is always called on the Unity main thread
                            alreadyDisabled.Add(exec.Executor, res);
                            return res;
                        }
                    }

                    var disabled = new Dictionary<PluginExecutor, Task>();
                    result = TaskEx.WhenAll(disableStructure.Select(d => Disable(d, disabled)));
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
        /// Gets a list of all enabled BSIPA plugins. Use <see cref="EnabledPlugins"/> instead of this.
        /// </summary>
        /// <value>a collection of all enabled plugins as <see cref="PluginMetadata"/>s</value>
        [Obsolete("This is an old name that no longer accurately represents its value. Use EnabledPlugins instead.")]
        public static IEnumerable<PluginMetadata> AllPlugins => EnabledPlugins;
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

        /// <summary>
        /// An <see cref="IEnumerable{T}"/> of old IPA plugins.
        /// </summary>
        /// <value>all legacy plugin instances</value>
        [Obsolete("This exists only to provide support for legacy IPA plugins based on the IPlugin interface.")]
        public static IEnumerable<Old.IPlugin> Plugins => _ipaPlugins;
        private static List<Old.IPlugin> _ipaPlugins;

        internal static IConfigProvider SelfConfigProvider { get; set; }

        internal static void Load()
        {
            string pluginDirectory = UnityGame.PluginsPath;

            // Process.GetCurrentProcess().MainModule crashes the game and Assembly.GetEntryAssembly() is NULL,
            // so we need to resort to P/Invoke
            string exeName = Path.GetFileNameWithoutExtension(AppInfo.StartupPath);
            _bsPlugins = new List<PluginExecutor>();
            _ipaPlugins = new List<Old.IPlugin>();

            if (!Directory.Exists(pluginDirectory)) return;

            string cacheDir = Path.Combine(pluginDirectory, ".cache");

            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            else
            {
                foreach (string plugin in Directory.GetFiles(cacheDir, "*"))
                    File.Delete(plugin);
            }

            // initialize BSIPA plugins first
            _bsPlugins.AddRange(PluginLoader.LoadPlugins());

            var metadataPaths = PluginLoader.PluginsMetadata.Select(m => m.File.FullName).ToList();
            var ignoredPaths = PluginLoader.ignoredPlugins.Select(m => m.Key.File.FullName)
                .Concat(PluginLoader.ignoredPlugins.SelectMany(m => m.Key.AssociatedFiles.Select(f => f.FullName))).ToList();
            var disabledPaths = DisabledPlugins.Select(m => m.File.FullName).ToList();

            //Copy plugins to .cache
            string[] originalPlugins = Directory.GetFiles(pluginDirectory, "*.dll");
            foreach (string s in originalPlugins)
            {
                if (metadataPaths.Contains(s)) continue;
                if (ignoredPaths.Contains(s)) continue;
                if (disabledPaths.Contains(s)) continue;
                string pluginCopy = Path.Combine(cacheDir, Path.GetFileName(s));

                #region Fix assemblies for refactor

                var module = ModuleDefinition.ReadModule(Path.Combine(pluginDirectory, s));
                foreach (var @ref in module.AssemblyReferences)
                { // fix assembly references
                    if (@ref.Name == "IllusionPlugin" || @ref.Name == "IllusionInjector")
                    {
                        @ref.Name = "IPA.Loader";
                    }
                }

                foreach (var @ref in module.GetTypeReferences())
                { // fix type references
                    if (@ref.FullName == "IllusionPlugin.IPlugin") @ref.Namespace = "IPA.Old"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.IEnhancedPlugin") @ref.Namespace = "IPA.Old"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.IniFile") @ref.Namespace = "IPA.Config"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.IModPrefs") @ref.Namespace = "IPA.Config"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.ModPrefs") @ref.Namespace = "IPA.Config"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.Utils.ReflectionUtil") @ref.Namespace = "IPA.Utilities"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.Logging.Logger") @ref.Namespace = "IPA.Logging"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.Logging.LogPrinter") @ref.Namespace = "IPA.Logging"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.PluginManager") @ref.Namespace = "IPA.Loader"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.PluginComponent") @ref.Namespace = "IPA.Loader"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.CompositeBSPlugin") @ref.Namespace = "IPA.Loader.Composite"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.CompositeIPAPlugin") @ref.Namespace = "IPA.Loader.Composite"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.Logging.UnityLogInterceptor") @ref.Namespace = "IPA.Logging"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.Logging.StandardLogger") @ref.Namespace = "IPA.Logging"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.Updating.SelfPlugin") @ref.Namespace = "IPA.Updating"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionInjector.Updating.Backup.BackupUnit") @ref.Namespace = "IPA.Updating.Backup"; //@ref.Name = "";
                    if (@ref.Namespace == "IllusionInjector.Utilities") @ref.Namespace = "IPA.Utilities"; //@ref.Name = "";
                    if (@ref.Namespace == "IllusionInjector.Logging.Printers") @ref.Namespace = "IPA.Logging.Printers"; //@ref.Name = "";
                }
                module.Write(pluginCopy);

                #endregion
            }

            //Load copied plugins
            string[] copiedPlugins = Directory.GetFiles(cacheDir, "*.dll");
            foreach (string s in copiedPlugins)
            {
                var result = LoadPluginsFromFile(s);
                if (result == null) continue;
                _ipaPlugins.AddRange(result.NonNull());
            }

            Logger.log.Info(exeName);
            Logger.log.Info($"Running on Unity {Application.unityVersion}");
            Logger.log.Info($"Game version {UnityGame.GameVersion}");
            Logger.log.Info("-----------------------------");
            Logger.log.Info($"Loading plugins from {Utils.GetRelativePath(pluginDirectory, Environment.CurrentDirectory)} and found {_bsPlugins.Count + _ipaPlugins.Count}");
            Logger.log.Info("-----------------------------");
            foreach (var plugin in _bsPlugins)
            {
                Logger.log.Info($"{plugin.Metadata.Name} ({plugin.Metadata.Id}): {plugin.Metadata.Version}");
            }
            Logger.log.Info("-----------------------------");
            foreach (var plugin in _ipaPlugins)
            {
                Logger.log.Info($"{plugin.Name}: {plugin.Version}");
            }
            Logger.log.Info("-----------------------------");
        }

        private static IEnumerable<Old.IPlugin> LoadPluginsFromFile(string file)
        {
            var ipaPlugins = new List<Old.IPlugin>();

            if (!File.Exists(file) || !file.EndsWith(".dll", true, null))
                return ipaPlugins;

            T OptionalGetPlugin<T>(Type t) where T : class
            {
                if (t.FindInterfaces((t, o) => t == (o as Type), typeof(T)).Length > 0)
                {
                    try
                    {
                        T pluginInstance = Activator.CreateInstance(t) as T;
                        return pluginInstance;
                    }
                    catch (Exception e)
                    {
                        Logger.loader.Error($"Could not load plugin {t.FullName} in {Path.GetFileName(file)}! {e}");
                    }
                }

                return null;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(file);

                foreach (Type t in assembly.GetTypes())
                {
                       
                    var ipaPlugin = OptionalGetPlugin<Old.IPlugin>(t);
                    if (ipaPlugin != null)
                    {
                        ipaPlugins.Add(ipaPlugin);
                    }
                }

            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.loader.Error($"Could not load the following types from {Path.GetFileName(file)}:");
                Logger.loader.Error($"  {string.Join("\n  ", e.LoaderExceptions?.Select(e1 => e1?.Message).StrJP() ?? Array.Empty<string>())}");
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not load {Path.GetFileName(file)}!");
                Logger.loader.Error(e);
            }

            return ipaPlugins;
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
#pragma warning restore CS0618 // Type or member is obsolete (IPlugin)
    }
}
