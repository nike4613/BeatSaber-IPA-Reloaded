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
            InitFeatures();
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

            internal readonly List<Feature> InternalFeatures = new List<Feature>();

            internal bool IsSelf;

            internal bool IsBare;

            private PluginManifest manifest;

            internal HashSet<PluginMetadata> Dependencies { get; } = new HashSet<PluginMetadata>();

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
            public override string ToString() => $"{Name}({Id}@{Version})({PluginType?.FullName}) from '{Utils.GetRelativePath(File?.FullName, BeatSaber.InstallPath)}'";
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
                    PluginType = null,
                    IsSelf = true
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
                        File = new FileInfo(Path.Combine(BeatSaber.PluginsPath, plugin)),
                        IsSelf = false
                    };

                    var pluginModule = AssemblyDefinition.ReadAssembly(plugin, new ReaderParameters
                    {
                       ReadingMode = ReadingMode.Immediate,
                       ReadWrite = false,
                       AssemblyResolver = new CecilLibLoader() 
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
                        Logger.loader.Notice(
                        #if DIRE_LOADER_WARNINGS
                            $"Could not find plugin type for {Path.GetFileName(plugin)}"
                        #else
                            $"New plugin type not present in {Path.GetFileName(plugin)}; maybe an old plugin?"
                        #endif
                            );
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

                    if (metadata.Manifest == null)
                    {
                        Logger.loader.Error("Could not find manifest.json in namespace " +
                            $"{metadata.PluginType.Namespace} for {Path.GetFileName(plugin)}");
                        continue;
                    }

                    Logger.loader.Debug($"Adding info for {Path.GetFileName(plugin)}");
                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Could not load data for plugin {Path.GetFileName(plugin)}");
                    Logger.loader.Error(e);
                }
            }

            IEnumerable<string> bareManifests = Directory.GetFiles(BeatSaber.PluginsPath, "*.json");
            bareManifests = bareManifests.Concat(Directory.GetFiles(BeatSaber.PluginsPath, "*.manifest"));
            foreach (var manifest in bareManifests)
            {
                try
                {
                    var metadata = new PluginMetadata
                    {
                        File = new FileInfo(Path.Combine(BeatSaber.PluginsPath, manifest)),
                        IsSelf = false,
                        IsBare = true,
                    };

                    metadata.Manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifest));

                    Logger.loader.Debug($"Adding info for bare manifest {Path.GetFileName(manifest)}");
                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Could not load data for bare manifest {Path.GetFileName(manifest)}");
                    Logger.loader.Error(e);
                }
            }
        }

        // keep track of these for the updater; it should still be able to update mods not loaded
        internal static HashSet<PluginMetadata> ignoredPlugins = new HashSet<PluginMetadata>();

        internal static void Resolve()
        { // resolves duplicates and conflicts, etc
            PluginsMetadata.Sort((a, b) => b.Version.CompareTo(a.Version));
            
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
                        ignoredPlugins.Add(meta);
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

                if (ignore.Contains(meta))
                {
                    ignoredPlugins.Add(meta);
                    continue;
                }
                if (meta.Id != null)
                    ids.Add(meta.Id);

                resolved.Add(meta);
            }

            PluginsMetadata = resolved;
        }

        internal static void ComputeLoadOrder()
        {
#if DEBUG
            Logger.loader.Debug(string.Join(", ", PluginsMetadata.Select(p => p.ToString())));
#endif

            bool InsertInto(HashSet<PluginMetadata> root, PluginMetadata meta, bool isRoot = false)
            { // this is slow, and hella recursive
                bool inserted = false;
                foreach (var sr in root)
                {
                    inserted = inserted || InsertInto(sr.Dependencies, meta);

                    if (meta.Id != null)
                        if (sr.Manifest.Dependencies.ContainsKey(meta.Id) || sr.Manifest.LoadAfter.Contains(meta.Id))
                            inserted = inserted || sr.Dependencies.Add(meta);
                    if (sr.Id != null)
                        if (meta.Manifest.LoadBefore.Contains(sr.Id))
                            inserted = inserted || sr.Dependencies.Add(meta);
                }

                if (isRoot)
                {
                    foreach (var sr in root)
                    {
                        InsertInto(meta.Dependencies, sr);

                        if (sr.Id != null)
                            if (meta.Manifest.Dependencies.ContainsKey(sr.Id) || meta.Manifest.LoadAfter.Contains(sr.Id))
                                meta.Dependencies.Add(sr);
                        if (meta.Id != null)
                            if (sr.Manifest.LoadBefore.Contains(meta.Id))
                                meta.Dependencies.Add(sr);
                    }

                    root.Add(meta);
                }

                return inserted;
            }

            var pluginTree = new HashSet<PluginMetadata>();
            foreach (var meta in PluginsMetadata)
                InsertInto(pluginTree, meta, true);

            void DeTree(List<PluginMetadata> into, HashSet<PluginMetadata> tree)
            {
                foreach (var st in tree)
                    if (!into.Contains(st))
                    {
                        DeTree(into, st.Dependencies);
                        into.Add(st);
                    }
            }

            var deTreed = new List<PluginMetadata>();
            DeTree(deTreed, pluginTree);

#if DEBUG
            Logger.loader.Debug(string.Join(", ", deTreed.Select(p => p.ToString())));
#endif

            var metadata = new List<PluginMetadata>();
            var pluginsToLoad = new Dictionary<string, Version>();
            foreach (var meta in deTreed)
            {
                bool load = true;
                foreach (var dep in meta.Manifest.Dependencies)
                {
#if DEBUG
                    Logger.loader.Debug($"Looking for dependency {dep.Key} with version range {dep.Value.Intersect(new SemVer.Range("*.*.*"))}");
#endif
                    if (pluginsToLoad.ContainsKey(dep.Key) && dep.Value.IsSatisfied(pluginsToLoad[dep.Key]))
                        continue;

                    load = false;
                    Logger.loader.Warn($"{meta.Name} is missing dependency {dep.Key}@{dep.Value}");
                }

                if (load)
                {
                    metadata.Add(meta);
                    if (meta.Id != null)
                        pluginsToLoad.Add(meta.Id, meta.Version);
                }
                else
                    ignoredPlugins.Add(meta);
            }

            PluginsMetadata = metadata;
        }

        internal static void InitFeatures()
        {
            var parsedFeatures = PluginsMetadata.Select(m =>
                    Tuple.Create(m,
                        m.Manifest.Features.Select(f => 
                            Tuple.Create(f, Ref.Create<Feature.FeatureParse?>(null))
                        ).ToList()
                    )
                ).ToList();

            while (DefineFeature.NewFeature)
            {
                DefineFeature.NewFeature = false;

                foreach (var plugin in parsedFeatures)
                    for (var i = 0; i < plugin.Item2.Count; i++)
                    {
                        var feature = plugin.Item2[i];

                        var success = Feature.TryParseFeature(feature.Item1, plugin.Item1, out var featureObj,
                            out var exception, out var valid, out var parsed, feature.Item2.Value);

                        if (!success && !valid && featureObj == null && exception == null) // no feature of type found
                            feature.Item2.Value = parsed;
                        else if (success)
                        {
                            if (valid && featureObj.StoreOnPlugin)
                                plugin.Item1.InternalFeatures.Add(featureObj);
                            else if (!valid)
                                Logger.features.Warn(
                                    $"Feature not valid on {plugin.Item1.Name}: {featureObj.InvalidMessage}");
                            plugin.Item2.RemoveAt(i--);
                        }
                        else
                        {
                            Logger.features.Error($"Error parsing feature definition on {plugin.Item1.Name}");
                            Logger.features.Error(exception);
                            plugin.Item2.RemoveAt(i--);
                        }
                    }

                foreach (var plugin in PluginsMetadata)
                    foreach (var feature in plugin.Features)
                        feature.Evaluate();
            }

            foreach (var plugin in parsedFeatures)
            {
                if (plugin.Item2.Count <= 0) continue;

                Logger.features.Warn($"On plugin {plugin.Item1.Name}:");
                foreach (var feature in plugin.Item2)
                    Logger.features.Warn($"    Feature not found with name {feature.Item1}");
            }
        }

        internal static void Load(PluginMetadata meta)
        {
            if (meta.Assembly == null && meta.PluginType != null)
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

                Feature denyingFeature = null;
                if (!meta.Features.All(f => (denyingFeature = f).BeforeLoad(meta)))
                {
                    Logger.loader.Warn(
                        $"Feature {denyingFeature?.GetType()} denied plugin {meta.Name} from loading! {denyingFeature?.InvalidMessage}");
                    ignoredPlugins.Add(meta);
                    return null;
                }

                var type = meta.Assembly.GetType(meta.PluginType.FullName);
                var instance = (IBeatSaberPlugin)Activator.CreateInstance(type);

                info.Metadata = meta;
                info.Plugin = instance;

                var init = type.GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
                if (init != null)
                {
                    denyingFeature = null;
                    if (!meta.Features.All(f => (denyingFeature = f).BeforeInit(info)))
                    {
                        Logger.loader.Warn(
                            $"Feature {denyingFeature?.GetType()} denied plugin {meta.Name} from initializing! {denyingFeature?.InvalidMessage}");
                        ignoredPlugins.Add(meta);
                        return null;
                    }

                    PluginInitInjector.Inject(init, info);
                }

                foreach (var feature in meta.Features)
                    try
                    {
                        feature.AfterInit(info);
                    }
                    catch (Exception e)
                    {
                        Logger.loader.Critical($"Feature errored in {nameof(Feature.AfterInit)}: {e}");
                    }
            }
            catch (AmbiguousMatchException)
            {
                Logger.loader.Error($"Only one Init allowed per plugin (ambiguous match in {meta.Name})");
                // not adding to ignoredPlugins here because this should only happen in a development context
                // if someone fucks this up on release thats on them
                return null;
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not init plugin {meta.Name}: {e}");
                ignoredPlugins.Add(meta);
                return null;
            }

            return info;
        }

        internal static List<PluginInfo> LoadPlugins() => PluginsMetadata.Select(InitPlugin).Where(p => p != null).ToList();
    }
}