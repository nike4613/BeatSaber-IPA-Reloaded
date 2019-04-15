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
        internal static IEnumerable<IBeatSaberPlugin> BSPlugins
        {
            get
            {
                if(_bsPlugins == null)
                {
                    LoadPlugins();
                }
                return (_bsPlugins ?? throw new InvalidOperationException()).Select(p => p.Plugin);
            }
        }
        private static List<PluginInfo> _bsPlugins;
        internal static IEnumerable<PluginInfo> BSMetas
        {
            get
            {
                if (_bsPlugins == null)
                {
                    LoadPlugins();
                }
                return _bsPlugins;
            }
        }

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
        public static PluginInfo GetPluginFromModSaberName(string name)
        {
            return BSMetas.FirstOrDefault(p => p.Metadata.Id == name);
        }

        /// <summary>
        /// Gets a list of all BSIPA plugins.
        /// </summary>
        public static IEnumerable<PluginInfo> AllPlugins => BSMetas;

        /// <summary>
        /// An <see cref="IEnumerable"/> of old IPA plugins
        /// </summary>
        [Obsolete("I mean, IPlugin shouldn't be used, so why should this? Not renaming to extend support for old plugins.")]
        public static IEnumerable<IPlugin> Plugins
        {
            get
            {
                if (_ipaPlugins == null)
                {
                    LoadPlugins();
                }
                return _ipaPlugins;
            }
        }
        private static List<IPlugin> _ipaPlugins;

        internal static IConfigProvider SelfConfigProvider { get; set; }

        private static void LoadPlugins()
        {
            string pluginDirectory = Path.Combine(Environment.CurrentDirectory, "Plugins");

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

            /*var selfPlugin = new PluginInfo
            {
                Filename = Path.Combine(BeatSaber.InstallPath, "IPA.exe"),
                Plugin = SelfPlugin.Instance
            };
            selfPlugin.Metadata.Manifest = new PluginManifest
            {
                Author = "DaNike",
                Features = new string[0],
                Description = "",
                Version = new SemVer.Version(SelfConfig.IPA_Version),
                GameVersion = BeatSaber.GameVersion,
                Id = "beatsaber-ipa-reloaded"
            };
            selfPlugin.Metadata.File = new FileInfo(Path.Combine(BeatSaber.InstallPath, "IPA.exe"));

            _bsPlugins.Add(selfPlugin);*/

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
                Logger.log.Info($"{plugin.Metadata.Name}: {plugin.Metadata.Version}");
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
                        /*string[] filter = null;

                        if (typeof(T) == typeof(IPlugin) && pluginInstance is IEnhancedPlugin enhancedPlugin)
                            filter = enhancedPlugin.Filter;
                        else if (pluginInstance is IGenericEnhancedPlugin plugin)
                            filter = plugin.Filter;*/

                        //if (filter == null || filter.Contains(exeName, StringComparer.OrdinalIgnoreCase))
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
                Logger.loader.Error($"  {string.Join(", ", e.Types.Select(t => t.FullName))}");
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not load {Path.GetFileName(file)}!");
                Logger.loader.Error(e);
            }

            return new Tuple<IEnumerable<PluginInfo>, IEnumerable<IPlugin>>(null, ipaPlugins);
        }

        internal class AppInfo
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
