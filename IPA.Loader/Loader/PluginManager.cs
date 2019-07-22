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
using static IPA.Loader.PluginLoader;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// The manager class for all plugins.
    /// </summary>
    public static class PluginManager
    {
#pragma warning disable CS0618 // Type or member is obsolete (IPlugin)
        
        /// <summary>
        /// An <see cref="IEnumerable"/> of new Beat Saber plugins
        /// </summary>
        internal static IEnumerable<IBeatSaberPlugin> BSPlugins => (_bsPlugins ?? throw new InvalidOperationException()).Select(p => p.Plugin);
        private static List<PluginInfo> _bsPlugins;
        internal static IEnumerable<PluginInfo> BSMetas => _bsPlugins;

        /// <summary>
        /// Gets info about the plugin with the specified name.
        /// </summary>
        /// <param name="name">the name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin info for the requested plugin or null</returns>
        public static PluginInfo GetPlugin(string name)
        {
            return BSMetas.FirstOrDefault(p => p.Metadata.Name == name);
        }

        /// <summary>
        /// Gets info about the plugin with the specified ModSaber name.
        /// </summary>
        /// <param name="name">the ModSaber name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin info for the requested plugin or null</returns>
        [Obsolete("Old name. Use GetPluginFromId instead.")]
        public static PluginInfo GetPluginFromModSaberName(string name) => GetPluginFromId(name);

        /// <summary>
        /// Gets info about the plugin with the specified ID.
        /// </summary>
        /// <param name="name">the ID name of the plugin to get (must be an exact match)</param>
        /// <returns>the plugin info for the requested plugin or null</returns>
        public static PluginInfo GetPluginFromId(string name)
        {
            return BSMetas.FirstOrDefault(p => p.Metadata.Id == name);
        }

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
        /// Disables a plugin, and all dependents.
        /// </summary>
        /// <param name="plugin">the plugin to disable</param>
        /// <returns>whether or not it needs a restart to enable</returns>
        public static bool DisablePlugin(PluginInfo plugin)
        {
            if (plugin == null) return false;

            if (plugin.Metadata.IsBare)
            {
                Logger.loader.Warn($"Trying to disable bare manifest");
                return false;
            }

            if (IsDisabled(plugin.Metadata)) return false;

            var needsRestart = false;

            Logger.loader.Info($"Disabling {plugin.Metadata.Name}");

            var dependents = BSMetas.Where(m => m.Metadata.Dependencies.Contains(plugin.Metadata)).ToList();
            needsRestart = dependents.Aggregate(needsRestart, (b, p) => DisablePlugin(p) || b);

            DisabledConfig.Ref.Value.DisabledModIds.Add(plugin.Metadata.Id ?? plugin.Metadata.Name);
            DisabledConfig.Provider.Store(DisabledConfig.Ref.Value);

            if (!needsRestart && plugin.Plugin is IDisablablePlugin disable)
            {
                try
                {
                    disable.OnDisable();
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Error occurred trying to disable {plugin.Metadata.Name}");
                    Logger.loader.Error(e);
                }

                if (needsRestart)
                    Logger.loader.Warn($"Disablable plugin has non-disablable dependents; some things may not work properly");
            }
            else needsRestart = true;

            runtimeDisabled.Add(plugin);
            _bsPlugins.Remove(plugin);

            try
            {
                PluginDisabled?.Invoke(plugin.Metadata, needsRestart);
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Error occurred invoking disable event for {plugin.Metadata.Name}");
                Logger.loader.Error(e);
            }

            return needsRestart;
        }

        /// <summary>
        /// Disables a plugin, and all dependents.
        /// </summary>
        /// <param name="pluginId">the ID, or name if the ID is null, of the plugin to disable</param>
        /// <returns>whether a restart is needed to activate</returns>
        public static bool DisablePlugin(string pluginId) => DisablePlugin(GetPluginFromId(pluginId) ?? GetPlugin(pluginId));

        /// <summary>
        /// Enables a plugin that had been previously disabled.
        /// </summary>
        /// <param name="plugin">the plugin to enable</param>
        /// <returns>whether a restart is needed to activate</returns>
        public static bool EnablePlugin(PluginMetadata plugin)
        {
            if (plugin == null) return false;

            if (plugin.IsBare)
            {
                Logger.loader.Warn($"Trying to enable bare manifest");
                return false;
            }

            if (!IsDisabled(plugin)) return false;

            Logger.loader.Info($"Enabling {plugin.Name}");

            DisabledConfig.Ref.Value.DisabledModIds.Remove(plugin.Id ?? plugin.Name);
            DisabledConfig.Provider.Store(DisabledConfig.Ref.Value);

            var needsRestart = true;

            var depsNeedRestart = plugin.Dependencies.Aggregate(false, (b, p) => EnablePlugin(p) || b);

            var runtimeInfo = runtimeDisabled.FirstOrDefault(p => p.Metadata == plugin);
            if (runtimeInfo != null && runtimeInfo.Plugin is IDisablablePlugin disable)
            {
                try
                {
                    disable.OnEnable();
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Error occurred trying to enable {plugin.Name}");
                    Logger.loader.Error(e);
                }
                needsRestart = false;
            }
            else
            {
                PluginLoader.DisabledPlugins.Remove(plugin);
                if (runtimeInfo == null)
                {
                    runtimeInfo = InitPlugin(plugin);
                    needsRestart = false;
                }
            }

            if (runtimeInfo != null)
                runtimeDisabled.Remove(runtimeInfo);

            _bsPlugins.Add(runtimeInfo);

            try
            {
                PluginEnabled?.Invoke(runtimeInfo, needsRestart || depsNeedRestart);
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Error occurred invoking enable event for {plugin.Name}");
                Logger.loader.Error(e);
            }

            return needsRestart || depsNeedRestart;
        }

        /// <summary>
        /// Enables a plugin that had been previously disabled.
        /// </summary>
        /// <param name="pluginId">the ID, or name if the ID is null, of the plugin to enable</param>
        /// <returns>whether a restart is needed to activate</returns>
        public static bool EnablePlugin(string pluginId) => 
            EnablePlugin(GetDisabledPluginFromId(pluginId) ?? GetDisabledPlugin(pluginId));

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

        private static readonly List<PluginInfo> runtimeDisabled = new List<PluginInfo>();
        /// <summary>
        /// Gets a list of disabled BSIPA plugins.
        /// </summary>
        /// <value>a collection of all disabled plugins as <see cref="PluginMetadata"/></value>
        public static IEnumerable<PluginMetadata> DisabledPlugins => PluginLoader.DisabledPlugins.Concat(runtimeDisabled.Select(p => p.Metadata));

        /// <summary>
        /// An invoker for the <see cref="PluginEnabled"/> event.
        /// </summary>
        /// <param name="plugin">the plugin that was enabled</param>
        /// <param name="needsRestart">whether it needs a restart to take effect</param>
        public delegate void PluginEnableDelegate(PluginInfo plugin, bool needsRestart);
        /// <summary>
        /// An invoker for the <see cref="PluginDisabled"/> event.
        /// </summary>
        /// <param name="plugin">the plugin that was disabled</param>
        /// <param name="needsRestart">whether it needs a restart to take effect</param>
        public delegate void PluginDisableDelegate(PluginMetadata plugin, bool needsRestart);

        /// <summary>
        /// Called whenever a plugin is enabled.
        /// </summary>
        public static event PluginEnableDelegate PluginEnabled;
        /// <summary>
        /// Called whenever a plugin is disabled.
        /// </summary>
        public static event PluginDisableDelegate PluginDisabled;

        /// <summary>
        /// Gets a list of all BSIPA plugins.
        /// </summary>
        /// <value>a collection of all enabled plugins as <see cref="PluginInfo"/>s</value>
        public static IEnumerable<PluginInfo> AllPlugins => BSMetas;

        /// <summary>
        /// Converts a plugin's metadata to a <see cref="PluginInfo"/>.
        /// </summary>
        /// <param name="meta">the metadata</param>
        /// <returns>the plugin info</returns>
        public static PluginInfo InfoFromMetadata(PluginMetadata meta)
        {
            if (IsDisabled(meta))
                return runtimeDisabled.FirstOrDefault(p => p.Metadata == meta);
            else
                return AllPlugins.FirstOrDefault(p => p.Metadata == meta);
        }

        /// <summary>
        /// An <see cref="IEnumerable"/> of old IPA plugins.
        /// </summary>
        /// <value>all legacy plugin instances</value>
        [Obsolete("I mean, IPlugin shouldn't be used, so why should this? Not renaming to extend support for old plugins.")]
        public static IEnumerable<IPlugin> Plugins => _ipaPlugins;
        private static List<IPlugin> _ipaPlugins;

        internal static IConfigProvider SelfConfigProvider { get; set; }

        internal static void Load()
        {
            string pluginDir = BeatSaber.PluginsPath;
            var gameVer = BeatSaber.GameVersion;
            var lastVerS = SelfConfig.SelfConfigRef.Value.LastGameVersion;
            var lastVer = lastVerS != null ? new AlmostVersion(lastVerS, gameVer) : null;

            if (lastVer != null && gameVer != lastVer)
            {
                var oldPluginsName = Path.Combine(BeatSaber.InstallPath, $"Old {lastVer} Plugins");
                var newPluginsName = Path.Combine(BeatSaber.InstallPath, $"Old {gameVer} Plugins");

                ReleaseAll();

                if (Directory.Exists(oldPluginsName))
                    Directory.Delete(oldPluginsName, true);
                Directory.Move(pluginDir, oldPluginsName);
                if (Directory.Exists(newPluginsName))
                    Directory.Move(newPluginsName, pluginDir);
                else
                    Directory.CreateDirectory(pluginDir);

                LoadTask().Wait();
            }

            SelfConfig.SelfConfigRef.Value.LastGameVersion = gameVer.ToString();
            SelfConfig.LoaderConfig.Store(SelfConfig.SelfConfigRef.Value);

            LoadPlugins();
        }

        private static void LoadPlugins()
        {
            string pluginDirectory = BeatSaber.PluginsPath;

            // Process.GetCurrentProcess().MainModule crashes the game and Assembly.GetEntryAssembly() is NULL,
            // so we need to resort to P/Invoke
            string exeName = Path.GetFileNameWithoutExtension(AppInfo.StartupPath);
            _bsPlugins = new List<PluginInfo>();
            _ipaPlugins = new List<IPlugin>();

            if (!Directory.Exists(pluginDirectory)) return;

            string cacheDir = Path.Combine(pluginDirectory, ".cache");

            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            else
            {
                foreach (string plugin in Directory.GetFiles(cacheDir, "*"))
                {
                    File.Delete(plugin);
                }
            }

            // initialize BSIPA plugins first
            _bsPlugins.AddRange(PluginLoader.LoadPlugins());

            //Copy plugins to .cache
            string[] originalPlugins = Directory.GetFiles(pluginDirectory, "*.dll");
            foreach (string s in originalPlugins)
            {
                if (PluginsMetadata.Select(m => m.File.FullName).Contains(s)) continue;
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
                    if (@ref.FullName == "IllusionPlugin.IBeatSaberPlugin") @ref.Namespace = "IPA"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.IEnhancedBeatSaberPlugin") @ref.Namespace = "IPA"; //@ref.Name = "";
                    if (@ref.FullName == "IllusionPlugin.BeatSaber.ModsaberModInfo") @ref.Namespace = "IPA"; //@ref.Name = "";
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
                    if (@ref.Namespace == "IllusionInjector.Updating.ModsaberML") @ref.Namespace = "IPA.Updating.ModSaber"; //@ref.Name = "";
                }
                module.Write(pluginCopy);

                #endregion
            }

            //Load copied plugins
            string[] copiedPlugins = Directory.GetFiles(cacheDir, "*.dll");
            foreach (string s in copiedPlugins)
            {
                var result = LoadPluginsFromFile(s);
                _ipaPlugins.AddRange(result.Item2);
            }
            
            Logger.log.Info(exeName);
            Logger.log.Info($"Running on Unity {Application.unityVersion}");
            Logger.log.Info($"Game version {BeatSaber.GameVersion}");
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

        private static Tuple<IEnumerable<PluginInfo>, IEnumerable<IPlugin>> LoadPluginsFromFile(string file)
        {
            List<IPlugin> ipaPlugins = new List<IPlugin>();

            if (!File.Exists(file) || !file.EndsWith(".dll", true, null))
                return new Tuple<IEnumerable<PluginInfo>, IEnumerable<IPlugin>>(null, ipaPlugins);

            T OptionalGetPlugin<T>(Type t) where T : class
            {
                // use typeof() to allow for easier renaming (in an ideal world this compiles to a string, but ¯\_(ツ)_/¯)
                if (t.GetInterface(typeof(T).Name) != null)
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
                       
                    IPlugin ipaPlugin = OptionalGetPlugin<IPlugin>(t);
                    if (ipaPlugin != null)
                    {
                        ipaPlugins.Add(ipaPlugin);
                    }
                }

            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.loader.Error($"Could not load the following types from {Path.GetFileName(file)}:");
                Logger.loader.Error($"  {string.Join("\n  ", e.LoaderExceptions?.Select(e1 => e1?.Message) ?? new string[0])}");
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not load {Path.GetFileName(file)}!");
                Logger.loader.Error(e);
            }

            return new Tuple<IEnumerable<PluginInfo>, IEnumerable<IPlugin>>(null, ipaPlugins);
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
