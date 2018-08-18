using IllusionInjector.Logging;
using IllusionInjector.Updating;
using IllusionInjector.Utilities;
using IllusionPlugin;
using IllusionPlugin.BeatSaber;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LoggerBase = IllusionPlugin.Logging.Logger;

namespace IllusionInjector
{
    public static class PluginManager
    {
#pragma warning disable CS0618 // Type or member is obsolete (IPlugin)

        public class BSPluginMeta
        {
            public IBeatSaberPlugin Plugin { get; internal set; }
            public string Filename { get; internal set; }
            public ModsaberModInfo ModsaberInfo { get; internal set; }
        }

        public static IEnumerable<IBeatSaberPlugin> BSPlugins
        {
            get
            {
                if(_bsPlugins == null)
                {
                    LoadPlugins();
                }
                return _bsPlugins.Select(p => p.Plugin);
            }
        }
        private static List<BSPluginMeta> _bsPlugins = null;
        internal static IEnumerable<BSPluginMeta> BSMetas
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
        private static List<IPlugin> _ipaPlugins = null;

        

        private static void LoadPlugins()
        {
            string pluginDirectory = Path.Combine(Environment.CurrentDirectory, "Plugins");

            // Process.GetCurrentProcess().MainModule crashes the game and Assembly.GetEntryAssembly() is NULL,
            // so we need to resort to P/Invoke
            string exeName = Path.GetFileNameWithoutExtension(AppInfo.StartupPath);
            Logger.log.Info(exeName);
            _bsPlugins = new List<BSPluginMeta>();
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

            //Copy plugins to .cache
            string[] originalPlugins = Directory.GetFiles(pluginDirectory, "*.dll");
            foreach (string s in originalPlugins)
            {
                string pluginCopy = Path.Combine(cacheDir, Path.GetFileName(s));
                File.Copy(Path.Combine(pluginDirectory, s), pluginCopy);
            }

            var selfPlugin = new BSPluginMeta
            {
                Filename = Path.Combine(Environment.CurrentDirectory, "IPA.exe"),
                Plugin = new SelfPlugin()
            };
            selfPlugin.ModsaberInfo = selfPlugin.Plugin.ModInfo;

            _bsPlugins.Add(selfPlugin);

            //Load copied plugins
            string[] copiedPlugins = Directory.GetFiles(cacheDir, "*.dll");
            foreach (string s in copiedPlugins)
            {
                var result = LoadPluginsFromFile(s, exeName);
                _bsPlugins.AddRange(result.Item1);
                _ipaPlugins.AddRange(result.Item2);
            }


            // DEBUG
            Logger.log.Info($"Running on Unity {UnityEngine.Application.unityVersion}");
            Logger.log.Info($"Game version {UnityEngine.Application.version}");
            Logger.log.Info("-----------------------------");
            Logger.log.Info($"Loading plugins from {LoneFunctions.GetRelativePath(pluginDirectory, Environment.CurrentDirectory)} and found {_bsPlugins.Count + _ipaPlugins.Count}");
            Logger.log.Info("-----------------------------");
            foreach (var plugin in _bsPlugins)
            {
                Logger.log.Info($"{plugin.Plugin.Name}: {plugin.Plugin.Version}");
            }
            Logger.log.Info("-----------------------------");
            foreach (var plugin in _ipaPlugins)
            {
                Logger.log.Info($"{plugin.Name}: {plugin.Version}");
            }
            Logger.log.Info("-----------------------------");
        }

        private static Tuple<IEnumerable<BSPluginMeta>, IEnumerable<IPlugin>> LoadPluginsFromFile(string file, string exeName)
        {
            List<BSPluginMeta> bsPlugins = new List<BSPluginMeta>();
            List<IPlugin> ipaPlugins = new List<IPlugin>();

            if (!File.Exists(file) || !file.EndsWith(".dll", true, null))
                return new Tuple<IEnumerable<BSPluginMeta>, IEnumerable<IPlugin>>(bsPlugins, ipaPlugins);

            T OptionalGetPlugin<T>(Type t) where T : class
            {
                // use typeof() to allow for easier renaming (in an ideal world this compiles to a string, but ¯\_(ツ)_/¯)
                if (t.GetInterface(typeof(T).Name) != null)
                {
                    try
                    {
                        T pluginInstance = Activator.CreateInstance(t) as T;
                        string[] filter = null;

                        if (pluginInstance is IGenericEnhancedPlugin)
                        {
                            filter = ((IGenericEnhancedPlugin)pluginInstance).Filter;
                        }

                        if (filter == null || filter.Contains(exeName, StringComparer.OrdinalIgnoreCase))
                            return pluginInstance;
                    }
                    catch (Exception e)
                    {
                        Logger.log.Error($"Could not load plugin {t.FullName} in {Path.GetFileName(file)}! {e}");
                    }
                }

                return null;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(file);

                foreach (Type t in assembly.GetTypes())
                {
                    IBeatSaberPlugin bsPlugin = OptionalGetPlugin<IBeatSaberPlugin>(t);
                    if (bsPlugin != null)
                    {
                        try
                        {
                            var init = t.GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
                            if (init != null)
                            {
                                var initArgs = new List<object>();
                                var initParams = init.GetParameters();

                                LoggerBase modLogger = null;
                                IModPrefs modPrefs = null;

                                foreach (var param in initParams)
                                {
                                    var ptype = param.ParameterType;
                                    if (ptype.IsAssignableFrom(typeof(LoggerBase))) {
                                        if (modLogger == null) modLogger = new StandardLogger(bsPlugin.Name);
                                        initArgs.Add(modLogger);
                                    }
                                    else if (ptype.IsAssignableFrom(typeof(IModPrefs)))
                                    {
                                        if (modPrefs == null) modPrefs = new ModPrefs(bsPlugin);
                                        initArgs.Add(modPrefs);
                                    }
                                    else
                                        initArgs.Add(ptype.GetDefault());
                                }

                                init.Invoke(bsPlugin, initArgs.ToArray());
                            }

                            bsPlugins.Add(new BSPluginMeta
                            {
                                Plugin = bsPlugin,
                                Filename = file.Replace("\\.cache", ""), // quick and dirty fix
                                ModsaberInfo = bsPlugin.ModInfo
                            });
                        }
                        catch (AmbiguousMatchException)
                        {
                            Logger.log.Error($"Only one Init allowed per plugin");
                        }
                    }
                    else
                    {
                        IPlugin ipaPlugin = OptionalGetPlugin<IPlugin>(t);
                        if (ipaPlugin != null)
                        {
                            ipaPlugins.Add(ipaPlugin);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Logger.log.Error($"Could not load {Path.GetFileName(file)}! {e}");
            }

            return new Tuple<IEnumerable<BSPluginMeta>, IEnumerable<IPlugin>>(bsPlugins, ipaPlugins);
        }

        public class AppInfo
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = false)]
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
