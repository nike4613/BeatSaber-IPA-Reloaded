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
using SemVer;
#if NET4
using Task = System.Threading.Tasks.Task;
using TaskEx = System.Threading.Tasks.Task;
#endif
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
    internal class PluginLoader
    {
        internal static Task LoadTask() =>
            TaskEx.Run(() =>
        {
            YeetIfNeeded();

            LoadMetadata();
            Resolve();
            ComputeLoadOrder();
            FilterDisabled();

            ResolveDependencies();
        });

        internal static void YeetIfNeeded()
        {
            string pluginDir = UnityGame.PluginsPath;

            if (SelfConfig.YeetMods_ && UnityGame.IsGameVersionBoundary)
            {
                var oldPluginsName = Path.Combine(UnityGame.InstallPath, $"Old {UnityGame.OldVersion} Plugins");
                var newPluginsName = Path.Combine(UnityGame.InstallPath, $"Old {UnityGame.GameVersion} Plugins");

                if (Directory.Exists(oldPluginsName))
                    Directory.Delete(oldPluginsName, true);
                Directory.Move(pluginDir, oldPluginsName);
                if (Directory.Exists(newPluginsName))
                    Directory.Move(newPluginsName, pluginDir);
                else
                    Directory.CreateDirectory(pluginDir);
            }
        }

        internal static List<PluginMetadata> PluginsMetadata = new List<PluginMetadata>();
        internal static List<PluginMetadata> DisabledPlugins = new List<PluginMetadata>();

        private static readonly Regex embeddedTextDescriptionPattern = new Regex(@"#!\[(.+)\]", RegexOptions.Compiled | RegexOptions.Singleline);

        internal static void LoadMetadata()
        {
            string[] plugins = Directory.GetFiles(UnityGame.PluginsPath, "*.dll");

            try
            {
                var selfMeta = new PluginMetadata
                {
                    Assembly = Assembly.GetExecutingAssembly(),
                    File = new FileInfo(Path.Combine(UnityGame.InstallPath, "IPA.exe")),
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
                var metadata = new PluginMetadata
                {
                    File = new FileInfo(Path.Combine(UnityGame.PluginsPath, plugin)),
                    IsSelf = false
                };

                try
                {
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

                    void TryGetNamespacedPluginType(string ns, PluginMetadata meta)
                    {
                        foreach (var type in pluginModule.Types)
                        {
                            if (type.Namespace != ns) continue;

                            if (type.HasCustomAttributes)
                            {
                                var attr = type.CustomAttributes.FirstOrDefault(a => a.Constructor.DeclaringType.FullName == typeof(PluginAttribute).FullName);
                                if (attr != null)
                                {
                                    if (!attr.HasConstructorArguments)
                                    {
                                        Logger.loader.Warn($"Attribute plugin found in {type.FullName}, but attribute has no arguments");
                                        return;
                                    }

                                    var args = attr.ConstructorArguments;
                                    if (args.Count != 1)
                                    {
                                        Logger.loader.Warn($"Attribute plugin found in {type.FullName}, but attribute has unexpected number of arguments");
                                        return;
                                    }
                                    var rtOptionsArg = args[0];
                                    if (rtOptionsArg.Type.FullName != typeof(RuntimeOptions).FullName)
                                    {
                                        Logger.loader.Warn($"Attribute plugin found in {type.FullName}, but first argument is of unexpected type {rtOptionsArg.Type.FullName}");
                                        return;
                                    }

                                    var rtOptionsValInt = (int)rtOptionsArg.Value; // `int` is the underlying type of RuntimeOptions

                                    meta.RuntimeOptions = (RuntimeOptions)rtOptionsValInt;
                                    meta.PluginType = type;
                                    return;
                                }
                            }
                        }
                    }

                    var hint = metadata.Manifest.Misc?.PluginMainHint;

                    if (hint != null)
                    {
                        var type = pluginModule.GetType(hint);
                        if (type != null)
                            TryGetNamespacedPluginType(hint, metadata);
                    }

                    if (metadata.PluginType == null)
                        TryGetNamespacedPluginType(pluginNs, metadata);

                    if (metadata.PluginType == null)
                    {
                        Logger.loader.Error($"No plugin found in the manifest {(hint != null ? $"hint path ({hint}) or " : "")}namespace ({pluginNs}) in {Path.GetFileName(plugin)}");
                        continue;
                    }

                    Logger.loader.Debug($"Adding info for {Path.GetFileName(plugin)}");
                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.loader.Error($"Could not load data for plugin {Path.GetFileName(plugin)}");
                    Logger.loader.Error(e);
                    ignoredPlugins.Add(metadata, new IgnoreReason(Reason.Error)
                    {
                        ReasonText = "An error ocurred loading the data",
                        Error = e
                    });
                }
            }

            IEnumerable<string> bareManifests = Directory.GetFiles(UnityGame.PluginsPath, "*.json");
            bareManifests = bareManifests.Concat(Directory.GetFiles(UnityGame.PluginsPath, "*.manifest"));
            foreach (var manifest in bareManifests)
            { // TODO: maybe find a way to allow a bare manifest to specify an associated file
                try
                {
                    var metadata = new PluginMetadata
                    {
                        File = new FileInfo(Path.Combine(UnityGame.PluginsPath, manifest)),
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
                                                                   .NonNull()
                                                                   .FirstOrDefault(r => r.Name == name);
                        if (resc == null)
                        {
                            Logger.loader.Warn($"Could not find description file for plugin {meta.Name} ({name}); ignoring include");
                            meta.Manifest.Description = string.Join("\n", lines.Skip(1).StrJP()); // ignore first line
                            continue;
                        }

                        using var reader = new StreamReader(resc.GetResourceStream());
                        description = reader.ReadToEnd();
                    }
                    else
                    {
                        using var descriptionReader = new StreamReader(meta.Assembly.GetManifestResourceStream(name));
                        description = descriptionReader.ReadToEnd();
                    }

                    meta.Manifest.Description = description;
                }
            }
        }

        internal enum Reason
        {
            Error, Duplicate, Conflict, Dependency,
            Released, Feature, Unsupported
        }
        internal struct IgnoreReason
        {

            public Reason Reason { get; }
            public string ReasonText { get; set; }
            public Exception Error { get; set; }
            public PluginMetadata RelatedTo { get; set; }
            public IgnoreReason(Reason reason)
            {
                Reason = reason;
                ReasonText = null;
                Error = null;
                RelatedTo = null;
            }
        }

        // keep track of these for the updater; it should still be able to update mods not loaded
        // the thing -> the reason
        internal static Dictionary<PluginMetadata, IgnoreReason> ignoredPlugins = new Dictionary<PluginMetadata, IgnoreReason>();

        internal static void Resolve()
        { // resolves duplicates and conflicts, etc
            PluginsMetadata.Sort((a, b) => b.Version.CompareTo(a.Version));
            
            var ids = new HashSet<string>();
            var ignore = new Dictionary<PluginMetadata, IgnoreReason>();
            var resolved = new List<PluginMetadata>(PluginsMetadata.Count);
            foreach (var meta in PluginsMetadata)
            {
                if (meta.Id != null)
                {
                    if (ids.Contains(meta.Id))
                    {
                        Logger.loader.Warn($"Found duplicates of {meta.Id}, using newest");
                        var ireason = new IgnoreReason(Reason.Duplicate)
                        {
                            ReasonText = $"Duplicate entry of same ID ({meta.Id})",
                            RelatedTo = resolved.First(p => p.Id == meta.Id)
                        };
                        ignore.Add(meta, ireason);
                        ignoredPlugins.Add(meta, ireason);
                        continue; // because of sorted order, hightest order will always be the first one
                    }

                    bool processedLater = false;
                    foreach (var meta2 in PluginsMetadata)
                    {
                        if (ignore.ContainsKey(meta2)) continue;
                        if (meta == meta2)
                        {
                            processedLater = true;
                            continue;
                        }

                        if (!meta2.Manifest.Conflicts.ContainsKey(meta.Id)) continue;

                        var range = meta2.Manifest.Conflicts[meta.Id];
                        if (!range.IsSatisfied(meta.Version)) continue;

                        Logger.loader.Warn($"{meta.Id}@{meta.Version} conflicts with {meta2.Id}");

                        if (processedLater)
                        {
                            Logger.loader.Warn($"Ignoring {meta2.Name}");
                            ignore.Add(meta2, new IgnoreReason(Reason.Conflict)
                            {
                                ReasonText = $"{meta.Id}@{meta.Version} conflicts with {meta2.Id}",
                                RelatedTo = meta
                            });
                        }
                        else
                        {
                            Logger.loader.Warn($"Ignoring {meta.Name}");
                            ignore.Add(meta, new IgnoreReason(Reason.Conflict)
                            {
                                ReasonText = $"{meta2.Id}@{meta2.Version} conflicts with {meta.Id}",
                                RelatedTo = meta2
                            });
                            break;
                        }
                    }
                }

                if (ignore.TryGetValue(meta, out var reason))
                {
                    ignoredPlugins.Add(meta, reason);
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

            var disabled = DisabledConfig.Instance.DisabledModIds;
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

            static bool InsertInto(HashSet<PluginMetadata> root, PluginMetadata meta, bool isRoot = false)
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

            static void DeTree(List<PluginMetadata> into, HashSet<PluginMetadata> tree)
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
            var disabledLookup = DisabledPlugins.NonNull(m => m.Id).ToDictionary(m => m.Id, m => m.Version);
            foreach (var meta in PluginsMetadata)
            {
                var missingDeps = new List<(string id, Range version, bool disabled)>();
                foreach (var dep in meta.Manifest.Dependencies)
                {
#if DEBUG
                    Logger.loader.Debug($"Looking for dependency {dep.Key} with version range {dep.Value.Intersect(new SemVer.Range("*.*.*"))}");
#endif
                    if (pluginsToLoad.ContainsKey(dep.Key) && dep.Value.IsSatisfied(pluginsToLoad[dep.Key]))
                        continue;

                    if (disabledLookup.ContainsKey(dep.Key) && dep.Value.IsSatisfied(disabledLookup[dep.Key]))
                    {
                        Logger.loader.Warn($"Dependency {dep.Key} was found, but disabled. Disabling {meta.Name} too.");
                        missingDeps.Add((dep.Key, dep.Value, true));
                    }
                    else
                    {
                        Logger.loader.Warn($"{meta.Name} is missing dependency {dep.Key}@{dep.Value}");
                        missingDeps.Add((dep.Key, dep.Value, false));
                    }
                }

                if (missingDeps.Count == 0)
                {
                    metadata.Add(meta);
                    if (meta.Id != null)
                        pluginsToLoad.Add(meta.Id, meta.Version);
                }
                else if (missingDeps.Any(t => !t.disabled))
                { // missing deps
                    ignoredPlugins.Add(meta, new IgnoreReason(Reason.Dependency)
                    {
                        ReasonText = $"Missing dependencies {string.Join(", ", missingDeps.Where(t => !t.disabled).Select(t => $"{t.id}@{t.version}").StrJP())}"
                    });
                }
                else
                {
                    DisabledPlugins.Add(meta);
                    DisabledConfig.Instance.DisabledModIds.Add(meta.Id ?? meta.Name);
                }
            }

            DisabledConfig.Instance.Changed();
            PluginsMetadata = metadata;
        }

        internal static void InitFeatures()
        {
            var parsedFeatures = PluginsMetadata.Select(m =>
                    (metadata: m,
                     features: m.Manifest.Features.Select(feature => 
                            (feature, parsed: Ref.Create<Feature.FeatureParse?>(null))
                        ).ToList()
                    )
                ).ToList();

            while (DefineFeature.NewFeature)
            {
                DefineFeature.NewFeature = false;

                foreach (var (metadata, features) in parsedFeatures)
                    for (var i = 0; i < features.Count; i++)
                    {
                        var feature = features[i];

                        var success = Feature.TryParseFeature(feature.feature, metadata, out var featureObj,
                            out var exception, out var valid, out var parsed, feature.parsed.Value);

                        if (!success && !valid && featureObj == null && exception == null) // no feature of type found
                            feature.parsed.Value = parsed;
                        else if (success)
                        {
                            if (valid && featureObj.StoreOnPlugin)
                                metadata.InternalFeatures.Add(featureObj);
                            else if (!valid)
                                Logger.features.Warn(
                                    $"Feature not valid on {metadata.Name}: {featureObj.InvalidMessage}");
                            features.RemoveAt(i--);
                        }
                        else
                        {
                            Logger.features.Error($"Error parsing feature definition on {metadata.Name}");
                            Logger.features.Error(exception);
                            features.RemoveAt(i--);
                        }
                    }

                foreach (var plugin in PluginsMetadata)
                    foreach (var feature in plugin.Features)
                        feature.Evaluate();
            }

            foreach (var plugin in parsedFeatures)
            {
                if (plugin.features.Count <= 0) continue;

                Logger.features.Warn($"On plugin {plugin.metadata.Name}:");
                foreach (var feature in plugin.features)
                    Logger.features.Warn($"    Feature not found with name {feature.feature}");
            }
        }

        internal static void ReleaseAll(bool full = false)
        {
            if (full)
                ignoredPlugins = new Dictionary<PluginMetadata, IgnoreReason>();
            else
            {
                foreach (var m in PluginsMetadata)
                    ignoredPlugins.Add(m, new IgnoreReason(Reason.Released));
                foreach (var m in ignoredPlugins.Keys)
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

        internal static PluginExecutor InitPlugin(PluginMetadata meta, IEnumerable<PluginMetadata> alreadyLoaded)
        {
            if (meta.Manifest.GameVersion != UnityGame.GameVersion)
                Logger.loader.Warn($"Mod {meta.Name} developed for game version {meta.Manifest.GameVersion}, so it may not work properly.");

            if (meta.IsSelf)
                return new PluginExecutor(meta, PluginExecutor.Special.Self);

            foreach (var dep in meta.Dependencies)
            {
                if (alreadyLoaded.Contains(dep)) continue;

                // otherwise...

                if (ignoredPlugins.TryGetValue(dep, out var reason))
                { // was added to the ignore list
                    ignoredPlugins.Add(meta, new IgnoreReason(Reason.Dependency)
                    {
                        ReasonText = $"Dependency was ignored at load time: {reason.ReasonText}",
                        RelatedTo = dep
                    });
                }
                else
                { // was not added to ignore list
                    ignoredPlugins.Add(meta, new IgnoreReason(Reason.Dependency)
                    {
                        ReasonText = $"Dependency was not already loaded at load time, but was also not ignored",
                        RelatedTo = dep
                    });
                }

                return null;
            }

            if (meta.IsBare)
                return new PluginExecutor(meta, PluginExecutor.Special.Bare);

            Load(meta);

            foreach (var feature in meta.Features)
            {
                if (!feature.BeforeLoad(meta))
                {
                    Logger.loader.Warn(
                        $"Feature {feature?.GetType()} denied plugin {meta.Name} from loading! {feature?.InvalidMessage}");
                    ignoredPlugins.Add(meta, new IgnoreReason(Reason.Feature)
                    {
                        ReasonText = $"Denied in {nameof(Feature.BeforeLoad)} of feature {feature?.GetType()}:\n\t{feature?.InvalidMessage}"
                    });
                    return null;
                }
            }

            PluginExecutor exec;
            try
            {
                exec = new PluginExecutor(meta);
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Error creating executor for {meta.Name}");
                Logger.loader.Error(e);
                return null;
            }

            foreach (var feature in meta.Features)
            {
                if (!feature.BeforeInit(meta))
                {
                    Logger.loader.Warn(
                        $"Feature {feature?.GetType()} denied plugin {meta.Name} from initializing! {feature?.InvalidMessage}");
                    ignoredPlugins.Add(meta, new IgnoreReason(Reason.Feature)
                    {
                        ReasonText = $"Denied in {nameof(Feature.BeforeInit)} of feature {feature?.GetType()}:\n\t{feature?.InvalidMessage}"
                    });
                    return null;
                }
            }
                
            try
            {
                exec.Create();
            }
            catch (Exception e)
            {
                Logger.loader.Error($"Could not init plugin {meta.Name}");
                Logger.loader.Error(e);
                ignoredPlugins.Add(meta, new IgnoreReason(Reason.Error)
                {
                    ReasonText = "Error ocurred while initializing",
                    Error = e
                });
                return null;
            }

            foreach (var feature in meta.Features)
                try
                {
                    feature.AfterInit(meta, exec.Instance);
                }
                catch (Exception e)
                {
                    Logger.loader.Critical($"Feature errored in {nameof(Feature.AfterInit)}: {e}");
                }

            return exec;
        }

        internal static List<PluginExecutor> LoadPlugins()
        {
            InitFeatures();
            DisabledPlugins.ForEach(Load); // make sure they get loaded into memory so their metadata and stuff can be read more easily

            var list = new List<PluginExecutor>();
            var loaded = new HashSet<PluginMetadata>();
            foreach (var meta in PluginsMetadata)
            {
                var exec = InitPlugin(meta, loaded);
                if (exec != null)
                {
                    list.Add(exec);
                    loaded.Add(meta);
                }
            }

            return list;
        }
    }
}