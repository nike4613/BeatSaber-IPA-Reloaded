using IPA.Config;
using IPA.Loader.Features;
using IPA.Logging;
using IPA.Utilities;
using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Version = SemVer.Version;

namespace IPA.Loader
{
    /// <summary>
    /// A type to manage the loading of plugins.
    /// </summary>
    public class PluginLoader
    {
        internal static Task LoadTask() => Task.Run(() =>
        {
            LoadMetadata();
            Resolve();
            ComputeLoadOrder();
        });

        /// <summary>
        /// A class which describes
        /// </summary>
        public class PluginMetadata
        {
            /// <summary>
            /// The assembly the plugin was loaded from.
            /// </summary>
            public Assembly Assembly { get; internal set; }

            /// <summary>
            /// The TypeDefinition for the main type of the plugin.
            /// </summary>
            public TypeDefinition PluginType { get; internal set; }

            /// <summary>
            /// The human readable name of the plugin.
            /// </summary>
            public string Name { get; internal set; }

            /// <summary>
            /// The ModSaber ID of the plugin, or null if it doesn't have one.
            /// </summary>
            public string Id { get; internal set; }

            /// <summary>
            /// The version of the plugin.
            /// </summary>
            public Version Version { get; internal set; }

            /// <summary>
            /// The file the plugin was loaded from.
            /// </summary>
            public FileInfo File { get; internal set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            /// The features this plugin requests.
            /// </summary>
            public IReadOnlyList<Feature> Features => InternalFeatures;

            internal List<Feature> InternalFeatures = new List<Feature>();

            private PluginManifest manifest;

            internal PluginManifest Manifest
            {
                get => manifest;
                set
                {
                    manifest = value;
                    Name = value.Name;
                    Version = value.Version;
                    Id = value.Id;
                }
            }

            /// <inheritdoc />
            public override string ToString() => $"{Name}({Id}@{Version})({PluginType?.FullName}) from '{LoneFunctions.GetRelativePath(File?.FullName, BeatSaber.InstallPath)}'";
        }

        /// <summary>
        /// A container object for all the data relating to a plugin.
        /// </summary>
        public class PluginInfo
        {
            internal IBeatSaberPlugin Plugin { get; set; }

            /// <summary>
            /// Metadata for the plugin.
            /// </summary>
            public PluginMetadata Metadata { get; internal set; } = new PluginMetadata();
        }

        internal static List<PluginMetadata> PluginsMetadata = new List<PluginMetadata>();

        internal static void LoadMetadata()
        {
            string[] plugins = Directory.GetFiles(BeatSaber.PluginsPath, "*.dll");

            try
            {
                var selfMeta = new PluginMetadata
                {
                    Assembly = Assembly.GetExecutingAssembly(),
                    File = new FileInfo(Path.Combine(BeatSaber.InstallPath, "IPA.exe")),
                    PluginType = null
                };

                string manifest;
                using (var manifestReader =
                    new StreamReader(
                        selfMeta.Assembly.GetManifestResourceStream(typeof(PluginLoader), "manifest.json") ??
                        throw new InvalidOperationException()))
                    manifest = manifestReader.ReadToEnd();

                selfMeta.Manifest = JsonConvert.DeserializeObject<PluginManifest>(manifest);

                PluginsMetadata.Add(selfMeta);
            }
            catch (Exception e)
            {
                Logger.loader.Critical("Error loading own manifest");
                Logger.loader.Critical(e);
            }

            foreach (var plugin in plugins)
            {
                try
                {
                    var metadata = new PluginMetadata
                    {
                        File = new FileInfo(Path.Combine(BeatSaber.PluginsPath, plugin))
                    };

                    var pluginModule = AssemblyDefinition.ReadAssembly(plugin, new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Immediate,
                        ReadWrite = false
                    }).MainModule;

                    var iBeatSaberPlugin = pluginModule.ImportReference(typeof(IBeatSaberPlugin));
                    foreach (var type in pluginModule.Types)
                    {
                        foreach (var inter in type.Interfaces)
                        {
                            var ifType = inter.InterfaceType;

                            if (iBeatSaberPlugin.FullName == ifType.FullName)
                            {
                                metadata.PluginType = type;
                                break;
                            }
                        }

                        if (metadata.PluginType != null) break;
                    }

                    if (metadata.PluginType == null)
                    {
                        Logger.loader.Warn($"Could not find plugin type for {Path.GetFileName(plugin)}");
                        continue;
                    }

                    foreach (var resource in pluginModule.Resources)
                    {
                        if (!(resource is EmbeddedResource embedded) ||
                            embedded.Name != $"{metadata.PluginType.Namespace}.manifest.json") continue;

                        string manifest;
                        using (var manifestReader = new StreamReader(embedded.GetResourceStream()))
                            manifest = manifestReader.ReadToEnd();

                        metadata.Manifest = JsonConvert.DeserializeObject<PluginManifest>(manifest);
                        break;
                    }

                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Could not load data for plugin {Path.GetFileName(plugin)}");
                    Logger.loader.Error(e);
                }
            }
        }

        internal static void Resolve()
        { // resolves duplicates and conflicts, etc
            PluginsMetadata.Sort((a, b) => a.Version.CompareTo(b.Version));

            var ids = new HashSet<string>();
            var ignore = new HashSet<PluginMetadata>();
            var resolved = new List<PluginMetadata>(PluginsMetadata.Count);
            foreach (var meta in PluginsMetadata)
            {
                if (meta.Id != null)
                {
                    if (ids.Contains(meta.Id))
                    {
                        Logger.loader.Warn($"Found duplicates of {meta.Id}, using newest");
                        ignore.Add(meta);
                        continue; // because of sorted order, hightest order will always be the first one
                    }

                    bool processedLater = false;
                    foreach (var meta2 in PluginsMetadata)
                    {
                        if (ignore.Contains(meta2)) continue;
                        if (meta == meta2)
                        {
                            processedLater = true;
                            continue;
                        }

                        if (!meta2.Manifest.Conflicts.ContainsKey(meta.Id)) continue;

                        var range = meta2.Manifest.Conflicts[meta.Id];
                        if (!range.IsSatisfied(meta.Version)) continue;

                        Logger.loader.Warn($"{meta.Id}@{meta.Version} conflicts with {meta2.Name}");

                        if (processedLater)
                        {
                            Logger.loader.Warn($"Ignoring {meta2.Name}");
                            ignore.Add(meta2);
                        }
                        else
                        {
                            Logger.loader.Warn($"Ignoring {meta.Name}");
                            ignore.Add(meta);
                            break;
                        }
                    }
                }

                if (ignore.Contains(meta)) continue;
                if (meta.Id != null) ids.Add(meta.Id);

                resolved.Add(meta);
            }

            PluginsMetadata = resolved;
        }

        internal static void ComputeLoadOrder()
        {
            PluginsMetadata.Sort((a, b) =>
            {
                if (a.Id == b.Id) return 0;
                if (a.Id != null)
                {
                    if (b.Manifest.Dependencies.ContainsKey(a.Id) || b.Manifest.LoadAfter.Contains(a.Id)) return -1;
                    if (b.Manifest.LoadBefore.Contains(a.Id)) return 1;
                }
                if (b.Id != null)
                {
                    if (a.Manifest.Dependencies.ContainsKey(b.Id) || a.Manifest.LoadAfter.Contains(b.Id)) return 1;
                    if (a.Manifest.LoadBefore.Contains(b.Id)) return -1;
                }

                return 0;
            });

            var metadata = new List<PluginMetadata>();
            var pluginsToLoad = new Dictionary<string, Version>();
            foreach (var meta in PluginsMetadata)
            {
                bool load = true;
                foreach (var dep in meta.Manifest.Dependencies)
                {
                    if (pluginsToLoad.ContainsKey(dep.Key) && dep.Value.IsSatisfied(pluginsToLoad[dep.Key])) continue;

                    load = false;
                    Logger.loader.Warn($"{meta.Name} is missing dependency {dep.Key}@{dep.Value}");
                }

                if (load)
                {
                    metadata.Add(meta);
                    if (meta.Id != null)
                        pluginsToLoad.Add(meta.Id, meta.Version);
                }
            }

            PluginsMetadata = metadata;
        }

        internal static void Load(PluginMetadata meta)
        {
            if (meta.Assembly == null)
                meta.Assembly = Assembly.LoadFrom(meta.File.FullName);
        }

        internal static PluginInfo InitPlugin(PluginMetadata meta)
        {
            if (meta.PluginType == null)
                return new PluginInfo()
                {
                    Metadata = meta,
                    Plugin = null
                };

            var info = new PluginInfo();

            try
            {
                Load(meta);

                var type = meta.Assembly.GetType(meta.PluginType.FullName);
                var instance = (IBeatSaberPlugin)Activator.CreateInstance(type);

                info.Metadata = meta;
                info.Plugin = instance;

                {
                    var init = type.GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
                    if (init != null)
                    {
                        var initArgs = new List<object>();
                        var initParams = init.GetParameters();

                        Logger modLogger = null;
                        IModPrefs modPrefs = null;
                        IConfigProvider cfgProvider = null;

                        foreach (var param in initParams)
                        {
                            var paramType = param.ParameterType;
                            if (paramType.IsAssignableFrom(typeof(Logger)))
                            {
                                if (modLogger == null) modLogger = new StandardLogger(meta.Name);
                                initArgs.Add(modLogger);
                            }
                            else if (paramType.IsAssignableFrom(typeof(IModPrefs)))
                            {
                                if (modPrefs == null) modPrefs = new ModPrefs(instance);
                                initArgs.Add(modPrefs);
                            }
                            else if (paramType.IsAssignableFrom(typeof(IConfigProvider)))
                            {
                                if (cfgProvider == null)
                                {
                                    cfgProvider = Config.Config.GetProviderFor(Path.Combine("UserData", $"{meta.Name}"), param);
                                    cfgProvider.Load();
                                }
                                initArgs.Add(cfgProvider);
                            }
                            else
                                initArgs.Add(paramType.GetDefault());
                        }

                        init.Invoke(instance, initArgs.ToArray());
                    }
                }
            }
            catch (AmbiguousMatchException)
            {
                Logger.loader.Error($"Only one Init allowed per plugin (ambiguous match in {meta.Name})");
                return null;
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not init plugin {meta.Name}: {e}");
                return null;
            }

            return info;
        }

        internal static List<PluginInfo> LoadPlugins() => PluginsMetadata.Select(InitPlugin).Where(p => p != null).ToList();
    }
}