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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Version = SemVer.Version;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// A type to manage the loading of plugins.
    /// </summary>
    public class PluginLoader
    {
        internal static Task LoadTask() => Task.Factory.StartNew(() =>
        {
            YeetIfNeeded();

            LoadMetadata();
            Resolve();
            ComputeLoadOrder();
            FilterDisabled();

            ResolveDependencies();
        });

        /// <summary>
        /// A class which describes a loaded plugin.
        /// </summary>
        public class PluginMetadata
        {
            /// <summary>
            /// The assembly the plugin was loaded from.
            /// </summary>
            /// <value>the loaded Assembly that contains the plugin main type</value>
            public Assembly Assembly { get; internal set; }

            /// <summary>
            /// The TypeDefinition for the main type of the plugin.
            /// </summary>
            /// <value>the Cecil definition for the plugin main type</value>
            public TypeDefinition PluginType { get; internal set; }

            /// <summary>
            /// The human readable name of the plugin.
            /// </summary>
            /// <value>the name of the plugin</value>
            public string Name { get; internal set; }

            /// <summary>
            /// The BeatMods ID of the plugin, or null if it doesn't have one.
            /// </summary>
            /// <value>the updater ID of the plugin</value>
            public string Id { get; internal set; }

            /// <summary>
            /// The version of the plugin.
            /// </summary>
            /// <value>the version of the plugin</value>
            public Version Version { get; internal set; }

            /// <summary>
            /// The file the plugin was loaded from.
            /// </summary>
            /// <value>the file the plugin was loaded from</value>
            public FileInfo File { get; internal set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            /// The features this plugin requests.
            /// </summary>
            /// <value>the list of features requested by the plugin</value>
            public IReadOnlyList<Feature> Features => InternalFeatures;

            internal readonly List<Feature> InternalFeatures = new List<Feature>();

            internal bool IsSelf;

            /// <summary>
            /// Whether or not this metadata object represents a bare manifest.
            /// </summary>
            /// <value><see langword="true"/> if it is bare, <see langword="false"/> otherwise</value>
            public bool IsBare { get; internal set; }

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

            /// <summary>
            /// Gets all of the metadata as a readable string.
            /// </summary>
            /// <returns>the readable printable metadata string</returns>
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
            /// <value>the metadata for this plugin</value>
            public PluginMetadata Metadata { get; internal set; } = new PluginMetadata();
        }

        internal static void YeetIfNeeded()
        {
            string pluginDir = BeatSaber.PluginsPath;
            var gameVer = BeatSaber.GameVersion;
            var lastVerS = SelfConfig.LastGameVersion_;
            var lastVer = lastVerS != null ? new AlmostVersion(lastVerS, gameVer) : null;

            if (SelfConfig.YeetMods_ && lastVer != null && gameVer != lastVer)
            {
                var oldPluginsName = Path.Combine(BeatSaber.InstallPath, $"Old {lastVer} Plugins");
                var newPluginsName = Path.Combine(BeatSaber.InstallPath, $"Old {gameVer} Plugins");

                if (Directory.Exists(oldPluginsName))
                    Directory.Delete(oldPluginsName, true);
                Directory.Move(pluginDir, oldPluginsName);
                if (Directory.Exists(newPluginsName))
                    Directory.Move(newPluginsName, pluginDir);
                else
                    Directory.CreateDirectory(pluginDir);
            }

            SelfConfig.SelfConfigRef.Value.LastGameVersion = gameVer.ToString();
            SelfConfig.LoaderConfig.Store(SelfConfig.SelfConfigRef.Value);
        }

        internal static List<PluginMetadata> PluginsMetadata = new List<PluginMetadata>();
        internal static List<PluginMetadata> DisabledPlugins = new List<PluginMetadata>();

        private static readonly Regex embeddedTextDescriptionPattern = new Regex(@"#!\[(.+)\]", RegexOptions.Compiled | RegexOptions.Singleline);

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

                    string pluginNs = "";

                    foreach (var resource in pluginModule.Resources)
                    {
                        const string manifestSuffix = ".manifest.json";
                        if (!(resource is EmbeddedResource embedded) ||
                            !embedded.Name.EndsWith(manifestSuffix)) continue;

                        pluginNs = embedded.Name.Substring(0, embedded.Name.Length - manifestSuffix.Length);

                        string manifest;
                        using (var manifestReader = new StreamReader(embedded.GetResourceStream()))
                            manifest = manifestReader.ReadToEnd();

                        metadata.Manifest = JsonConvert.DeserializeObject<PluginManifest>(manifest);
                        break;
                    }

                    if (metadata.Manifest == null)
                    {
#if DIRE_LOADER_WARNINGS
                        Logger.loader.Error($"Could not find manifest.json for {Path.GetFileName(plugin)}");
#else
                        Logger.loader.Notice($"No manifest.json in {Path.GetFileName(plugin)}");
#endif
                        continue;
                    }

                    foreach (var type in pluginModule.Types)
                    {
                        if (type.Namespace != pluginNs) continue;

                        foreach (var inter in type.Interfaces)
                        {
                            if (typeof(IBeatSaberPlugin).FullName == inter.InterfaceType.FullName)
                            {
                                metadata.PluginType = type;
                                goto type_loop_done; // break out of both loops
                            }
                        }
                    }

                    type_loop_done:
                    if (metadata.PluginType == null)
                    {
                        Logger.loader.Error($"No plugin found in the manifest namespace ({pluginNs}) in {Path.GetFileName(plugin)}");
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

            foreach (var meta in PluginsMetadata)
            { // process description include
                var lines = meta.Manifest.Description.Split('\n');
                var m = embeddedTextDescriptionPattern.Match(lines[0]);
                if (m.Success)
                {
                    if (meta.IsBare)
                    {
                        Logger.loader.Warn($"Bare manifest cannot specify description file");
                        meta.Manifest.Description = string.Join("\n", lines.Skip(1).StrJP()); // ignore first line
                        continue;
                    }

                    var name = m.Groups[1].Value;
                    string description;
                    if (!meta.IsSelf)
                    {
                        var resc = meta.PluginType.Module.Resources.Select(r => r as EmbeddedResource)
                                                                   .Where(r => r != null)
                                                                   .FirstOrDefault(r => r.Name == name);
                        if (resc == null)
                        {
                            Logger.loader.Warn($"Could not find description file for plugin {meta.Name} ({name}); ignoring include");
                            meta.Manifest.Description = string.Join("\n", lines.Skip(1).StrJP()); // ignore first line
                            continue;
                        }

                        using (var reader = new StreamReader(resc.GetResourceStream()))
                            description = reader.ReadToEnd();
                    }
                    else
                    {
                        using (var descriptionReader =
                            new StreamReader(
                                meta.Assembly.GetManifestResourceStream(name) ??
                                throw new InvalidOperationException()))
                            description = descriptionReader.ReadToEnd();
                    }

                    meta.Manifest.Description = description;
                }
            }
        }

        // keep track of these for the updater; it should still be able to update mods not loaded
        // TODO: add ignore reason
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

        private static void FilterDisabled()
        {
            var enabled = new List<PluginMetadata>(PluginsMetadata.Count);

            var disabled = DisabledConfig.Ref.Value.DisabledModIds;
            foreach (var meta in PluginsMetadata)
            {
                if (disabled.Contains(meta.Id ?? meta.Name))
                    DisabledPlugins.Add(meta);
                else
                    enabled.Add(meta);
            }

            PluginsMetadata = enabled;
        }

        internal static void ComputeLoadOrder()
        {
#if DEBUG
            Logger.loader.Debug(string.Join(", ", PluginsMetadata.Select(p => p.ToString()).StrJP()));
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

            PluginsMetadata = new List<PluginMetadata>();
            DeTree(PluginsMetadata, pluginTree);

#if DEBUG
            Logger.loader.Debug(string.Join(", ", PluginsMetadata.Select(p => p.ToString()).StrJP()));
#endif
        }

        internal static void ResolveDependencies()
        {
            var metadata = new List<PluginMetadata>();
            var pluginsToLoad = new Dictionary<string, Version>();
            var disabledLookup = DisabledPlugins.Where(m => m.Id != null).ToDictionary(m => m.Id, m => m.Version);
            foreach (var meta in PluginsMetadata)
            {
                bool load = true;
                bool disable = false;
                foreach (var dep in meta.Manifest.Dependencies)
                {
#if DEBUG
                    Logger.loader.Debug($"Looking for dependency {dep.Key} with version range {dep.Value.Intersect(new SemVer.Range("*.*.*"))}");
#endif
                    if (pluginsToLoad.ContainsKey(dep.Key) && dep.Value.IsSatisfied(pluginsToLoad[dep.Key]))
                        continue;

                    load = false;

                    if (disabledLookup.ContainsKey(dep.Key) && dep.Value.IsSatisfied(disabledLookup[dep.Key]))
                    {
                        disable = true;
                        Logger.loader.Warn($"Dependency {dep.Key} was found, but disabled. Disabling {meta.Name} too.");
                    }
                    else
                        Logger.loader.Warn($"{meta.Name} is missing dependency {dep.Key}@{dep.Value}");

                    break;
                }

                if (load)
                {
                    metadata.Add(meta);
                    if (meta.Id != null)
                        pluginsToLoad.Add(meta.Id, meta.Version);
                }
                else if (disable)
                {
                    DisabledPlugins.Add(meta);
                    DisabledConfig.Ref.Value.DisabledModIds.Add(meta.Id ?? meta.Name);
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

        internal static void ReleaseAll(bool full = false)
        {
            if (full)
                ignoredPlugins = new HashSet<PluginMetadata>();
            else
            {
                foreach (var m in PluginsMetadata)
                    ignoredPlugins.Add(m);
                foreach (var m in ignoredPlugins)
                { // clean them up so we can still use the metadata for updates
                    m.InternalFeatures.Clear();
                    m.PluginType = null;
                    m.Assembly = null;
                }
            }
            PluginsMetadata = new List<PluginMetadata>();
            DisabledPlugins = new List<PluginMetadata>();
            Feature.Reset();
            GC.Collect();
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

            if (meta.Manifest.GameVersion != BeatSaber.GameVersion)
                Logger.loader.Warn($"Mod {meta.Name} developed for game version {meta.Manifest.GameVersion}, so it may not work properly.");

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
                        feature.AfterInit(info, info.Plugin);
                    }
                    catch (Exception e)
                    {
                        Logger.loader.Critical($"Feature errored in {nameof(Feature.AfterInit)}: {e}");
                    }

                if (instance is IDisablablePlugin disable)
                    try // TODO: move this out to after all plugins have been inited
                    {
                        disable.OnEnable();
                    }
                    catch (Exception e)
                    {
                        Logger.loader.Error($"Error occurred trying to enable {meta.Name}");
                        Logger.loader.Error(e);
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

        internal static List<PluginInfo> LoadPlugins()
        {
            InitFeatures();
            DisabledPlugins.ForEach(Load); // make sure they get loaded into memory so their metadata and stuff can be read more easily
            return PluginsMetadata.Select(InitPlugin).Where(p => p != null).ToList();
        }
    }
}