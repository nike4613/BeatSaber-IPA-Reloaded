#nullable enable
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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using IPA.AntiMalware;
using Hive.Versioning;
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

    internal partial class PluginLoader
    {
        internal static PluginMetadata SelfMeta = null!;

        internal static Task LoadTask() =>
            TaskEx.Run(() =>
        {
            YeetIfNeeded();

            var sw = Stopwatch.StartNew();

            LoadMetadata();

            sw.Stop();
            Logger.Loader.Info($"Loading metadata took {sw.Elapsed}");
            sw.Reset();

            sw.Start();

            // Features contribute to load order considerations
            InitFeatures();
            DoOrderResolution();

            sw.Stop();
            Logger.Loader.Info($"Calculating load order took {sw.Elapsed}");
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
                    _ = Directory.CreateDirectory(pluginDir);
            }
        }

        internal static List<PluginMetadata> PluginsMetadata = new();
        internal static List<PluginMetadata> DisabledPlugins = new();

        private static readonly Regex embeddedTextDescriptionPattern = new(@"#!\[(.+)\]", RegexOptions.Compiled | RegexOptions.Singleline);

        public static string[] LoadFilesRecursively(string folderPath, string fileName)
        {
            var dlls = new List<string>();

            dlls.AddRange(Directory.GetFiles(folderPath, fileName));

            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                dlls.AddRange(LoadFilesRecursively(subfolder, fileName));
            }

            return dlls.ToArray();
        }

        public static string[] LoadDirectoriesRecursively(string folderPath)
        {
            var directories = new List<string>();

            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                directories.AddRange(LoadDirectoriesRecursively(subfolder));
            }

            directories.Add(folderPath);

            return directories.ToArray();
        }

        internal static void LoadMetadata()
        {
            string[] plugins = LoadFilesRecursively(UnityGame.PluginsPath, "*.dll");

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
                SelfMeta = selfMeta;
            }
            catch (Exception e)
            {
                Logger.Loader.Critical("Error loading own manifest");
                Logger.Loader.Critical(e);
            }

            using var resolver = new CecilLibLoader();

            foreach (var libSubDirectory in LoadDirectoriesRecursively(UnityGame.LibraryPath))
                resolver.AddSearchDirectory(libSubDirectory);

            foreach (var pluginSubDirectory in LoadDirectoriesRecursively(UnityGame.PluginsPath))
                resolver.AddSearchDirectory(pluginSubDirectory);
            
            foreach (var plugin in plugins)
            {
                var metadata = new PluginMetadata
                {
                    File = new FileInfo(plugin),
                    IsSelf = false
                };

                try
                {
                    var scanResult = AntiMalwareEngine.Engine.ScanFile(metadata.File);
                    if (scanResult is ScanResult.Detected)
                    {
                        Logger.Loader.Warn($"Scan of {plugin} found malware; not loading");
                        continue;
                    }
                    if (!SelfConfig.AntiMalware_.RunPartialThreatCode_ && scanResult is not ScanResult.KnownSafe and not ScanResult.NotDetected)
                    {
                        Logger.Loader.Warn($"Scan of {plugin} found partial threat; not loading. To load this, set AntiMalware.RunPartialThreatCode in the config.");
                        continue;
                    }

                    var pluginModule = AssemblyDefinition.ReadAssembly(metadata.File.FullName, new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Immediate,
                        ReadWrite = false,
                        AssemblyResolver = resolver
                    }).MainModule;

                    string pluginNs = "";

                    PluginManifest? pluginManifest = null;
                    foreach (var resource in pluginModule.Resources)
                    {
                        const string manifestSuffix = ".manifest.json";
                        if (resource is not EmbeddedResource embedded ||
                            !embedded.Name.EndsWith(manifestSuffix, StringComparison.Ordinal)) continue;

                        pluginNs = embedded.Name.Substring(0, embedded.Name.Length - manifestSuffix.Length);

                        string manifest;
                        using (var manifestReader = new StreamReader(embedded.GetResourceStream()))
                            manifest = manifestReader.ReadToEnd();

                        pluginManifest = JsonConvert.DeserializeObject<PluginManifest?>(manifest);
                        break;
                    }

                    if (pluginManifest == null)
                    {
#if DIRE_LOADER_WARNINGS
                        Logger.loader.Error($"Could not find manifest.json for {Path.GetFileName(plugin)}");
#else
                        Logger.Loader.Notice($"No manifest.json in {Path.GetFileName(plugin)}");
#endif
                        continue;
                    }

                    if (pluginManifest.Id == null)
                    {
                        Logger.Loader.Warn($"Plugin '{pluginManifest.Name}' does not have a listed ID, using name");
                        pluginManifest.Id = pluginManifest.Name;
                    }

                    metadata.Manifest = pluginManifest;

                    bool TryPopulatePluginType(TypeDefinition type, PluginMetadata meta)
                    {
                        if (!type.HasCustomAttributes)
                            return false;

                        var attr = type.CustomAttributes.FirstOrDefault(a => a.Constructor.DeclaringType.FullName == typeof(PluginAttribute).FullName);
                        if (attr is null)
                            return false;

                        if (!attr.HasConstructorArguments)
                        {
                            Logger.Loader.Warn($"Attribute plugin found in {type.FullName}, but attribute has no arguments");
                            return false;
                        }

                        var args = attr.ConstructorArguments;
                        if (args.Count != 1)
                        {
                            Logger.Loader.Warn($"Attribute plugin found in {type.FullName}, but attribute has unexpected number of arguments");
                            return false;
                        }
                        var rtOptionsArg = args[0];
                        if (rtOptionsArg.Type.FullName != typeof(RuntimeOptions).FullName)
                        {
                            Logger.Loader.Warn($"Attribute plugin found in {type.FullName}, but first argument is of unexpected type {rtOptionsArg.Type.FullName}");
                            return false;
                        }

                        var rtOptionsValInt = (int)rtOptionsArg.Value; // `int` is the underlying type of RuntimeOptions

                        meta.RuntimeOptions = (RuntimeOptions)rtOptionsValInt;
                        meta.PluginType = type;
                        return true;
                    }

                    void TryGetNamespacedPluginType(string ns, PluginMetadata meta)
                    {
                        foreach (var type in pluginModule.Types)
                        {
                            if (type.Namespace != ns) continue;

                            if (TryPopulatePluginType(type, meta))
                                return;
                        }
                    }

                    var hint = metadata.Manifest.Misc?.PluginMainHint;

                    if (hint != null)
                    {
                        var type = pluginModule.GetType(hint);
                        if (type == null || !TryPopulatePluginType(type, metadata))
                            TryGetNamespacedPluginType(hint, metadata);
                    }

                    if (metadata.PluginType == null)
                        TryGetNamespacedPluginType(pluginNs, metadata);

                    if (metadata.PluginType == null)
                    {
                        Logger.Loader.Error($"No plugin found in the manifest {(hint != null ? $"hint path ({hint}) or " : "")}namespace ({pluginNs}) in {Path.GetFileName(plugin)}");
                        continue;
                    }

                    Logger.Loader.Debug($"Adding info for {Path.GetFileName(plugin)}");
                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.Loader.Error($"Could not load data for plugin {Path.GetFileName(plugin)}");
                    Logger.Loader.Error(e);
                    ignoredPlugins.Add(metadata, new IgnoreReason(Reason.Error)
                    {
                        ReasonText = "An error occurred loading the data",
                        Error = e
                    });
                }
            }

            IEnumerable<string> bareManifests = LoadFilesRecursively(UnityGame.PluginsPath, "*.json");
            bareManifests = bareManifests.Concat(LoadFilesRecursively(UnityGame.PluginsPath, "*.manifest"));
            foreach (var manifest in bareManifests)
            {
                try
                {
                    var metadata = new PluginMetadata
                    {
                        File = new FileInfo(manifest),
                        IsSelf = false,
                        IsBare = true,
                    };

                    metadata.Manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifest));

                    if (metadata.Manifest.Files.Length < 1)
                        Logger.Loader.Warn($"Bare manifest {Path.GetFileName(manifest)} does not declare any files. " +
                            $"Dependency resolution and verification cannot be completed.");

                    Logger.Loader.Debug($"Adding info for bare manifest {Path.GetFileName(manifest)}");
                    PluginsMetadata.Add(metadata);
                }
                catch (Exception e)
                {
                    Logger.Loader.Error($"Could not load data for bare manifest {Path.GetFileName(manifest)}");
                    Logger.Loader.Error(e);
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
                        Logger.Loader.Warn($"Bare manifest cannot specify description file");
                        meta.Manifest.Description = string.Join("\n", lines.Skip(1).StrJP()); // ignore first line
                        continue;
                    }

                    var name = m.Groups[1].Value;
                    string description;
                    if (!meta.IsSelf)
                    {
                        // plugin type must be non-null for non-self plugins
                        var resc = meta.PluginType!.Module.Resources.Select(r => r as EmbeddedResource)
                                                                   .NonNull()
                                                                   .FirstOrDefault(r => r.Name == name);
                        if (resc == null)
                        {
                            Logger.Loader.Warn($"Could not find description file for plugin {meta.Name} ({name}); ignoring include");
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
    }

    #region Ignore stuff
    /// <summary>
    /// An enum that represents several categories of ignore reasons that the loader may encounter.
    /// </summary>
    /// <seealso cref="IgnoreReason"/>
    public enum Reason
    {
        /// <summary>
        /// An error was thrown either loading plugin information from disk, or when initializing the plugin.
        /// </summary>
        /// <remarks>
        /// When this is the set <see cref="Reason"/> in an <see cref="IgnoreReason"/> structure, the member
        /// <see cref="IgnoreReason.Error"/> will contain the thrown exception.
        /// </remarks>
        Error,
        /// <summary>
        /// The plugin this reason is associated with has the same ID as another plugin whose information was
        /// already loaded.
        /// </summary>
        /// <remarks>
        /// When this is the set <see cref="Reason"/> in an <see cref="IgnoreReason"/> structure, the member
        /// <see cref="IgnoreReason.RelatedTo"/> will contain the metadata of the already loaded plugin.
        /// </remarks>
        Duplicate,
        /// <summary>
        /// The plugin this reason is associated with conflicts with another already loaded plugin.
        /// </summary>
        /// <remarks>
        /// When this is the set <see cref="Reason"/> in an <see cref="IgnoreReason"/> structure, the member
        /// <see cref="IgnoreReason.RelatedTo"/> will contain the metadata of the plugin it conflicts with.
        /// </remarks>
        Conflict,
        /// <summary>
        /// The plugin this reason is associated with is missing a dependency.
        /// </summary>
        /// <remarks>
        /// Since this is only given when a dependency is missing, <see cref="IgnoreReason.RelatedTo"/> will
        /// not be set.
        /// </remarks>
        Dependency,
        /// <summary>
        /// The plugin this reason is associated with was released for a game update, but is still considered
        /// present for the purposes of updating.
        /// </summary>
        Released,
        /// <summary>
        /// The plugin this reason is associated with was denied from loading by a <see cref="Features.Feature"/>
        /// that it marks.
        /// </summary>
        Feature,
        /// <summary>
        /// The plugin this reason is associated with is unsupported.
        /// </summary>
        /// <remarks>
        /// Currently, there is no path in the loader that emits this <see cref="Reason"/>, however there may
        /// be in the future.
        /// </remarks>
        Unsupported,
        /// <summary>
        /// One of the files that a plugin declared in its manifest is missing.
        /// </summary>
        MissingFiles
    }
    /// <summary>
    /// A structure describing the reason that a plugin was ignored.
    /// </summary>
    public struct IgnoreReason : IEquatable<IgnoreReason>
    {
        /// <summary>
        /// Gets the ignore reason, as represented by the <see cref="Loader.Reason"/> enum.
        /// </summary>
        public Reason Reason { get; }
        /// <summary>
        /// Gets the textual description of the particular ignore reason. This will typically
        /// include details about why the plugin was ignored, if it is present.
        /// </summary>
        public string? ReasonText { get; internal set; }
        /// <summary>
        /// Gets the <see cref="Exception"/> that caused this plugin to be ignored, if any.
        /// </summary>
        public Exception? Error { get; internal set; }
        /// <summary>
        /// Gets the metadata of the plugin that this ignore was related to, if any.
        /// </summary>
        public PluginMetadata? RelatedTo { get; internal set; }
        /// <summary>
        /// Initializes an <see cref="IgnoreReason"/> with the provided data.
        /// </summary>
        /// <param name="reason">the <see cref="Loader.Reason"/> enum value that describes this reason</param>
        /// <param name="reasonText">the textual description of this ignore reason, if any</param>
        /// <param name="error">the <see cref="Exception"/> that caused this <see cref="IgnoreReason"/>, if any</param>
        /// <param name="relatedTo">the <see cref="PluginMetadata"/> this reason is related to, if any</param>
        public IgnoreReason(Reason reason, string? reasonText = null, Exception? error = null, PluginMetadata? relatedTo = null)
        {
            Reason = reason;
            ReasonText = reasonText;
            Error = error;
            RelatedTo = relatedTo;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is IgnoreReason ir && Equals(ir);
        /// <summary>
        /// Compares this <see cref="IgnoreReason"/> with <paramref name="other"/> for equality.
        /// </summary>
        /// <param name="other">the reason to compare to</param>
        /// <returns><see langword="true"/> if the two reasons compare equal, <see langword="false"/> otherwise</returns>
        public bool Equals(IgnoreReason other)
            => Reason == other.Reason && ReasonText == other.ReasonText
            && Error == other.Error && RelatedTo == other.RelatedTo;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 778404373;
            hashCode = (hashCode * -1521134295) + Reason.GetHashCode();
            hashCode = (hashCode * -1521134295) + ReasonText?.GetHashCode() ?? 0;
            hashCode = (hashCode * -1521134295) + Error?.GetHashCode() ?? 0;
            hashCode = (hashCode * -1521134295) + RelatedTo?.GetHashCode() ?? 0;
            return hashCode;
        }

        /// <summary>
        /// Checks if two <see cref="IgnoreReason"/>s are equal.
        /// </summary>
        /// <param name="left">the first <see cref="IgnoreReason"/> to compare</param>
        /// <param name="right">the second <see cref="IgnoreReason"/> to compare</param>
        /// <returns><see langword="true"/> if the two reasons compare equal, <see langword="false"/> otherwise</returns>
        public static bool operator ==(IgnoreReason left, IgnoreReason right)
            => left.Equals(right);

        /// <summary>
        /// Checks if two <see cref="IgnoreReason"/>s are not equal.
        /// </summary>
        /// <param name="left">the first <see cref="IgnoreReason"/> to compare</param>
        /// <param name="right">the second <see cref="IgnoreReason"/> to compare</param>
        /// <returns><see langword="true"/> if the two reasons are not equal, <see langword="false"/> otherwise</returns>
        public static bool operator !=(IgnoreReason left, IgnoreReason right)
            => !(left == right);
    }
    #endregion

    internal partial class PluginLoader
    {
        // keep track of these for the updater; it should still be able to update mods not loaded
        // the thing -> the reason
        internal static Dictionary<PluginMetadata, IgnoreReason> ignoredPlugins = new();

        internal static void DoOrderResolution()
        {
#if DEBUG
            // print starting order
            Logger.Loader.Debug(string.Join(", ", PluginsMetadata.StrJP()));
#endif

            PluginsMetadata.Sort((a, b) => b.HVersion.CompareTo(a.HVersion));

#if DEBUG
            // print base resolution order
            Logger.Loader.Debug(string.Join(", ", PluginsMetadata.StrJP()));
#endif

            var metadataCache = new Dictionary<string, (PluginMetadata Meta, bool Enabled)>(PluginsMetadata.Count);
            var pluginsToProcess = new List<PluginMetadata>(PluginsMetadata.Count);

            var disabledIds = DisabledConfig.Instance.DisabledModIds;
            var disabledPlugins = new List<PluginMetadata>();

            // build metadata cache
            foreach (var meta in PluginsMetadata)
            {
                if (!metadataCache.TryGetValue(meta.Id, out var existing))
                {
                    if (disabledIds.Contains(meta.Id))
                    {
                        metadataCache.Add(meta.Id, (meta, false));
                        disabledPlugins.Add(meta);
                    }
                    else
                    {
                        metadataCache.Add(meta.Id, (meta, true));
                        pluginsToProcess.Add(meta);
                    }
                }
                else
                {
                    Logger.Loader.Warn($"Found duplicates of {meta.Id}, using newest");
                    ignoredPlugins.Add(meta, new(Reason.Duplicate)
                    {
                        ReasonText = $"Duplicate entry of same ID ({meta.Id})",
                        RelatedTo = existing.Meta
                    });
                }
            }

            // preprocess LoadBefore into LoadAfter
            foreach (var (_, (meta, _)) in metadataCache)
            { // we iterate the metadata cache because it contains both disabled and enabled plugins
                var loadBefore = meta.Manifest.LoadBefore;
                foreach (var id in loadBefore)
                {
                    if (metadataCache.TryGetValue(id, out var plugin))
                    {
                        // if the id exists in our metadata cache, make sure it knows to load after the plugin in kvp
                        _ = plugin.Meta.LoadsAfter.Add(meta);
                    }
                }
            }

            // preprocess conflicts to be mutual
            foreach (var (_, (meta, _)) in metadataCache)
            {
                foreach (var (id, range) in meta.Manifest.Conflicts)
                {
                    if (metadataCache.TryGetValue(id, out var plugin)
                        && range.Matches(plugin.Meta.HVersion))
                    {
                        // make sure that there's a mutual dependency
                        var targetRange = VersionRange.ForVersion(meta.HVersion);
                        var targetConflicts = plugin.Meta.Manifest.Conflicts;
                        if (!targetConflicts.TryGetValue(meta.Id, out var realRange))
                        {
                            // there's not already a listed conflict
                            targetConflicts.Add(meta.Id, targetRange);
                        }
                        else if (!realRange.Matches(meta.HVersion))
                        {
                            // there is already a listed conflict that isn't mutual
                            targetRange = realRange | targetRange;
                            targetConflicts[meta.Id] = targetRange;
                        }
                    }
                }
            }

            var loadedPlugins = new Dictionary<string, (PluginMetadata Meta, bool Disabled, bool Ignored)>();
            var outputOrder = new List<PluginMetadata>(PluginsMetadata.Count);
            var isProcessing = new HashSet<PluginMetadata>();

            {
                bool TryResolveId(string id, [MaybeNullWhen(false)] out PluginMetadata meta, out bool disabled, out bool ignored, bool partial = false)
                {
                    meta = null;
                    disabled = false;
                    ignored = true;
                    Logger.Loader.Trace($"Trying to resolve plugin '{id}' partial:{partial}");
                    if (loadedPlugins.TryGetValue(id, out var foundMeta))
                    {
                        meta = foundMeta.Meta;
                        disabled = foundMeta.Disabled;
                        ignored = foundMeta.Ignored;
                        Logger.Loader.Trace($"- Found already processed");
                        return true;
                    }
                    if (metadataCache!.TryGetValue(id, out var plugin))
                    {
                        Logger.Loader.Trace($"- In metadata cache");
                        if (partial)
                        {
                            Logger.Loader.Trace($"  - but requested in a partial lookup");
                            return false;
                        }

                        disabled = !plugin.Enabled;
                        meta = plugin.Meta;
                        if (!disabled)
                        {
                            try
                            {
                                ignored = false;
                                Resolve(plugin.Meta, ref disabled, out ignored);
                            }
                            catch (Exception e)
                            {
                                if (e is not DependencyResolutionLoopException)
                                {
                                    Logger.Loader.Error($"While performing load order resolution for {id}:");
                                    Logger.Loader.Error(e);
                                }

                                if (!ignored)
                                {
                                    ignoredPlugins.Add(plugin.Meta, new(Reason.Error)
                                    {
                                        Error = e
                                    });
                                }

                                ignored = true;
                            }
                        }

                        if (!loadedPlugins.ContainsKey(id))
                        {
                            // this condition is specifically for when we fail resolution because of a graph loop
                            Logger.Loader.Trace($"- '{id}' resolved as ignored:{ignored},disabled:{disabled}");
                            loadedPlugins.Add(id, (plugin.Meta, disabled, ignored));
                        }
                        return true;
                    }
                    Logger.Loader.Trace($"- Not found");
                    return false;
                }

                void Resolve(PluginMetadata plugin, ref bool disabled, out bool ignored)
                {
                    Logger.Loader.Trace($">Resolving '{plugin.Name}'");

                    // first we need to check for loops in the resolution graph to prevent stack overflows
                    if (isProcessing.Contains(plugin))
                    {
                        Logger.Loader.Error($"Loop detected while processing '{plugin.Name}'; flagging as ignored");
                        throw new DependencyResolutionLoopException();
                    }

                    isProcessing.Add(plugin);
                    using var _removeProcessing = Utils.ScopeGuard(() => isProcessing.Remove(plugin));

                    // if this method is being called, this is the first and only time that it has been called for this plugin.

                    ignored = false;

                    // perform file existence check before attempting to load dependencies
                    foreach (var file in plugin.AssociatedFiles)
                    {
                        if (!file.Exists)
                        {
                            ignoredPlugins.Add(plugin, new IgnoreReason(Reason.MissingFiles)
                            {
                                ReasonText = $"File {Utils.GetRelativePath(file.FullName, UnityGame.InstallPath)} does not exist"
                            });
                            Logger.Loader.Warn($"File {Utils.GetRelativePath(file.FullName, UnityGame.InstallPath)}" +
                                $" (declared by '{plugin.Name}') does not exist! Mod installation is incomplete, not loading it.");
                            ignored = true;
                            return;
                        }
                    }

                    // first load dependencies
                    var dependsOnSelf = false;
                    foreach (var (id, range) in plugin.Manifest.Dependencies)
                    {
                        if (id == SelfMeta.Id)
                            dependsOnSelf = true;
                        if (!TryResolveId(id, out var depMeta, out var depDisabled, out var depIgnored)
                            || !range.Matches(depMeta.HVersion))
                        {
                            Logger.Loader.Warn($"'{plugin.Id}' is missing dependency '{id}@{range}'; ignoring");
                            ignoredPlugins.Add(plugin, new(Reason.Dependency)
                            {
                                ReasonText = $"Dependency '{id}@{range}' not found",
                            });
                            ignored = true;
                            return;
                        }
                        // make a point to propagate ignored
                        if (depIgnored)
                        {
                            Logger.Loader.Warn($"Dependency '{id}' for '{plugin.Id}' previously ignored; ignoring '{plugin.Id}'");
                            ignoredPlugins.Add(plugin, new(Reason.Dependency)
                            {
                                ReasonText = $"Dependency '{id}' ignored",
                                RelatedTo = depMeta
                            });
                            ignored = true;
                            return;
                        }
                        // make a point to propagate disabled
                        if (depDisabled)
                        {
                            Logger.Loader.Warn($"Dependency '{id}' for '{plugin.Id}' disabled; disabling");
                            disabledPlugins!.Add(plugin);
                            _ = disabledIds!.Add(plugin.Id);
                            disabled = true;
                        }

                        // we found our dep, lets save the metadata and keep going
                        _ = plugin.Dependencies.Add(depMeta);
                    }

                    // make sure the plugin depends on the loader (assuming it actually needs to)
                    if (!dependsOnSelf && !plugin.IsSelf && !plugin.IsBare)
                    {
                        Logger.Loader.Warn($"Plugin '{plugin.Id}' does not depend on any particular loader version; assuming its incompatible");
                        ignoredPlugins.Add(plugin, new(Reason.Dependency)
                        {
                            ReasonText = "Does not depend on any loader version, so it is assumed to be incompatible",
                            RelatedTo = SelfMeta
                        });
                        ignored = true;
                        return;
                    }

                    // exit early if we've decided we need to be disabled
                    if (disabled)
                        return;

                    // handle LoadsAfter populated by Features processing
                    foreach (var loadAfter in plugin.LoadsAfter)
                    {
                        if (TryResolveId(loadAfter.Id, out _, out _, out _))
                        {
                            // do nothing, because the plugin is already in the LoadsAfter set
                        }
                    }

                    // then handle loadafters
                    foreach (var id in plugin.Manifest.LoadAfter)
                    {
                        if (TryResolveId(id, out var meta, out var depDisabled, out var depIgnored))
                        {
                            // we only want to make sure to loadafter if its not ignored
                            // if its disabled, we still wanna track it where possible
                            _ = plugin.LoadsAfter.Add(meta);
                        }
                    }

                    // after we handle dependencies and loadafters, then check conflicts
                    foreach (var (id, range) in plugin.Manifest.Conflicts)
                    {
                        Logger.Loader.Trace($">- Checking conflict '{id}' {range}");
                        // this lookup must be partial to prevent loadBefore/conflictsWith from creating a recursion loop
                        if (TryResolveId(id, out var meta, out var conflDisabled, out var conflIgnored, partial: true)
                            && range.Matches(meta.HVersion)
                            && !conflIgnored && !conflDisabled) // the conflict is only *actually* a problem if it is both not ignored and not disabled
                        {
                            
                            Logger.Loader.Warn($"Plugin '{plugin.Id}' conflicts with {meta.Id}@{meta.HVersion}; ignoring '{plugin.Id}'");
                            ignoredPlugins.Add(plugin, new(Reason.Conflict)
                            {
                                ReasonText = $"Conflicts with {meta.Id}@{meta.HVersion}",
                                RelatedTo = meta
                            });
                            ignored = true;
                            return;
                        }
                    }

                    // specifically check if some strange stuff happened (like graph loops) causing this to be ignored
                    // from some other invocation
                    if (!ignoredPlugins.ContainsKey(plugin))
                    {
                        // we can now load the current plugin
                        Logger.Loader.Trace($"->'{plugin.Name}' loads here");
                        outputOrder!.Add(plugin);
                    }

                    // loadbefores have already been preprocessed into loadafters

                    Logger.Loader.Trace($">Processed '{plugin.Name}'");
                }

                // run TryResolveId over every plugin, which recursively calculates load order
                foreach (var plugin in pluginsToProcess)
                {
                    _ = TryResolveId(plugin.Id, out _, out _, out _);
                }
                // by this point, outputOrder contains the full load order
            }

            DisabledConfig.Instance.Changed();
            DisabledPlugins = disabledPlugins;
            PluginsMetadata = outputOrder;
        }

        internal static void InitFeatures()
        {
            foreach (var meta in PluginsMetadata)
            {
                foreach (var feature in meta.Manifest.Features
                    .SelectMany(f => f.Value.Select(o => (f.Key, o)))
                    .Select(t => new Feature.Instance(meta, t.Key, t.o)))
                {
                    if (feature.TryGetDefiningPlugin(out var plugin) && plugin == null)
                    { // this is a DefineFeature, so we want to initialize it early
                        if (!feature.TryCreate(out var inst))
                        {
                            Logger.Features.Error($"Error evaluating {feature.Name}: {inst.InvalidMessage}");
                        }
                        else
                        {
                            meta.InternalFeatures.Add(inst);
                        }
                    }
                    else
                    { // this is literally any other feature, so we want to delay its initialization
                        _ = meta.UnloadedFeatures.Add(feature);
                    }
                }
            }

            // at this point we have pre-initialized all features, so we can go ahead and use them to add stuff to the dep resolver
            foreach (var meta in PluginsMetadata)
            {
                foreach (var feature in meta.UnloadedFeatures)
                {
                    if (feature.TryGetDefiningPlugin(out var plugin))
                    {
                        if (plugin != meta && plugin != null)
                        { // if the feature is not applied to the defining feature
                            _ = meta.LoadsAfter.Add(plugin);
                        }

                        if (plugin != null)
                        {
                            plugin.CreateFeaturesWhenLoaded.Add(feature);
                        }
                    }
                    else
                    {
                        Logger.Features.Warn($"No such feature {feature.Name}");
                    }
                }
            }
        }

        internal static void ReleaseAll(bool full = false)
        {
            if (full)
            {
                ignoredPlugins = new();
            }
            else
            {
                foreach (var m in PluginsMetadata)
                    ignoredPlugins.Add(m, new IgnoreReason(Reason.Released));
                foreach (var m in ignoredPlugins.Keys)
                { // clean them up so we can still use the metadata for updates
                    m.InternalFeatures.Clear();
                    m.PluginType = null;
                    m.Assembly = null!;
                }
            }
            PluginsMetadata = new List<PluginMetadata>();
            DisabledPlugins = new List<PluginMetadata>();
            Feature.Reset();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        internal static void Load(PluginMetadata meta)
        {
            if (meta is { Assembly: null, PluginType: not null })
                meta.Assembly = Assembly.LoadFrom(meta.File.FullName);
        }

        internal static PluginExecutor? InitPlugin(PluginMetadata meta, IEnumerable<PluginMetadata> alreadyLoaded)
        {
            if (meta.Manifest.GameVersion is { } gv && gv != UnityGame.GameVersion)
                Logger.Loader.Warn($"Mod {meta.Name} developed for game version {gv}, so it may not work properly.");

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

            PluginExecutor exec;
            try
            {
                exec = new PluginExecutor(meta);
            }
            catch (Exception e)
            {
                Logger.Loader.Error($"Error creating executor for {meta.Name}");
                Logger.Loader.Error(e);
                return null;
            }

            foreach (var feature in meta.Features)
            {
                try
                {
                    feature.BeforeInit(meta);
                }
                catch (Exception e)
                {
                    Logger.Loader.Critical($"Feature errored in {nameof(Feature.BeforeInit)}:");
                    Logger.Loader.Critical(e);
                }
            }

            try
            {
                exec.Create();
            }
            catch (Exception e)
            {
                Logger.Loader.Error($"Could not init plugin {meta.Name}");
                Logger.Loader.Error(e);
                ignoredPlugins.Add(meta, new IgnoreReason(Reason.Error)
                {
                    ReasonText = "Error occurred while initializing",
                    Error = e
                });
                return null;
            }

            // TODO: make this new features system behave better wrt DynamicInit plugins
            foreach (var feature in meta.CreateFeaturesWhenLoaded)
            {
                if (!feature.TryCreate(out var inst))
                {
                    Logger.Features.Warn($"Could not create instance of feature {feature.Name}: {inst.InvalidMessage}");
                }
                else
                {
                    feature.AppliedTo.InternalFeatures.Add(inst);
                    _ = feature.AppliedTo.UnloadedFeatures.Remove(feature);
                }
            }
            meta.CreateFeaturesWhenLoaded.Clear(); // if a plugin is loaded twice, for the moment, we don't want to create the feature twice

            foreach (var feature in meta.Features)
                try
                {
                    feature.AfterInit(meta, exec.Instance);
                }
                catch (Exception e)
                {
                    Logger.Loader.Critical($"Feature errored in {nameof(Feature.AfterInit)}:");
                    Logger.Loader.Critical(e);
                }

            return exec;
        }

        internal static bool IsFirstLoadComplete { get; private set; }

        internal static List<PluginExecutor> LoadPlugins()
        {
            DisabledPlugins.ForEach(Load); // make sure they get loaded into memory so their metadata and stuff can be read more easily

            var list = new List<PluginExecutor>();
            var loaded = new HashSet<PluginMetadata>();
            foreach (var meta in PluginsMetadata)
            {
                try
                {
                    var exec = InitPlugin(meta, loaded);
                    if (exec != null)
                    {
                        list.Add(exec);
                        _ = loaded.Add(meta);
                    }
                }
                catch (Exception e)
                {
                    Logger.Default.Critical($"Uncaught exception while loading plugin {meta.Name}:");
                    Logger.Default.Critical(e);
                }
            }

            // TODO: should this be somewhere else?
            IsFirstLoadComplete = true;

            return list;
        }
    }
}