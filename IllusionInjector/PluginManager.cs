using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace IllusionInjector
{
    public static class PluginManager
    {
#pragma warning disable CS0618 // Type or member is obsolete (IPlugin)

        internal static readonly Logger debugLogger = new Logger("IllusionInjector");
        
        /// <summary>
        /// Gets the list of loaded plugins and loads them if necessary.
        /// </summary>
        public static IEnumerable<IBeatSaberPlugin> BSPlugins
        {
            get
            {
                if(_bsPlugins == null)
                {
                    LoadPlugins();
                }
                return _bsPlugins;
            }
        }
        private static List<IBeatSaberPlugin> _bsPlugins = null;
        
        public static IEnumerable<IPlugin> IPAPlugins
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
            debugLogger.Log(exeName);
            _bsPlugins = new List<IBeatSaberPlugin>();
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

            //Load copied plugins
            string[] copiedPlugins = Directory.GetFiles(cacheDir, "*.dll");
            foreach (string s in copiedPlugins)
            {
                var result = LoadPluginsFromFile(s, exeName);
                _bsPlugins.AddRange(result.Item1);
                _ipaPlugins.AddRange(result.Item2);
            }


            // DEBUG
            debugLogger.Log($"Running on Unity {UnityEngine.Application.unityVersion}");
            debugLogger.Log("-----------------------------");
            debugLogger.Log($"Loading plugins from {pluginDirectory} and found {_bsPlugins.Count}");
            debugLogger.Log("-----------------------------");
            foreach (var plugin in _bsPlugins)
            {
                debugLogger.Log($"{plugin.Name}: {plugin.Version}");
            }
            debugLogger.Log("-----------------------------");
        }

        private static Tuple<IEnumerable<IBeatSaberPlugin>, IEnumerable<IPlugin>> LoadPluginsFromFile(string file, string exeName)
        {
            List<IBeatSaberPlugin> bsPlugins = new List<IBeatSaberPlugin>();
            List<IPlugin> ipaPlugins = new List<IPlugin>();

            if (!File.Exists(file) || !file.EndsWith(".dll", true, null))
                return new Tuple<IEnumerable<IBeatSaberPlugin>, IEnumerable<IPlugin>>(bsPlugins, ipaPlugins);

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
                        debugLogger.Exception($"Could not load plugin {t.FullName} in {Path.GetFileName(file)}! {e}");
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
                        bsPlugins.Add(bsPlugin);
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
                debugLogger.Error($"Could not load {Path.GetFileName(file)}! {e}");
            }

            return new Tuple<IEnumerable<IBeatSaberPlugin>, IEnumerable<IPlugin>>(bsPlugins, ipaPlugins);
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
