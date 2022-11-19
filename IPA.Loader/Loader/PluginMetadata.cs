#nullable enable
using IPA.Loader.Features;
using IPA.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SVersion = SemVer.Version;
using Version = Hive.Versioning.Version;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Loader
{
    /// <summary>
    /// A class which describes a loaded plugin.
    /// </summary>
    public class PluginMetadata
    {
        /// <summary>
        /// The assembly the plugin was loaded from.
        /// </summary>
        /// <value>the loaded Assembly that contains the plugin main type</value>
        public Assembly Assembly { get; internal set; } = null!;

        /// <summary>
        /// The TypeDefinition for the main type of the plugin.
        /// </summary>
        /// <value>the Cecil definition for the plugin main type</value>
        public TypeDefinition? PluginType { get; internal set; }

        /// <summary>
        /// The human readable name of the plugin.
        /// </summary>
        /// <value>the name of the plugin</value>
        public string Name => manifest.Name;

        /// <summary>
        /// The ID of the plugin.
        /// </summary>
        /// <value>the ID of the plugin</value>
        public string Id => manifest.Id!; // by the time that this is publicly visible, it's always non-null

        /// <summary>
        /// The name of the author that wrote this plugin.
        /// </summary>
        /// <value>the name of the plugin's author</value>
        public string Author => manifest.Author;

        /// <summary>
        /// The description of this plugin.
        /// </summary>
        /// <value>the description of the plugin</value>
        public string Description => manifest.Description;

        /// <summary>
        /// The version of the plugin.
        /// </summary>
        /// <value>the version of the plugin</value>
        [Obsolete("Use HVersion instead.")]
        public SVersion Version => SVersion.ForHiveVersion(manifest.Version);

        /// <summary>
        /// The version of the plugin.
        /// </summary>
        /// <value>the version of the plugin</value>
        public Version HVersion => manifest.Version;

        /// <summary>
        /// The file the plugin was loaded from.
        /// </summary>
        /// <value>the file the plugin was loaded from</value>
        public FileInfo File { get; internal set; } = null!;

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        /// <summary>
        /// The features this plugin requests.
        /// </summary>
        /// <value>the list of features requested by the plugin</value>
        public IReadOnlyList<Feature> Features => InternalFeatures;

        internal readonly List<Feature> InternalFeatures = new();

        internal readonly HashSet<Feature.Instance> UnloadedFeatures = new();

        internal readonly List<Feature.Instance> CreateFeaturesWhenLoaded = new();

        /// <summary>
        /// A list of files (that aren't <see cref="File"/>) that are associated with this plugin.
        /// </summary>
        /// <value>a list of associated files</value>
        public IReadOnlyList<FileInfo> AssociatedFiles { get; private set; } = new List<FileInfo>();

        /// <summary>
        /// The name of the resource in the plugin assembly containing the plugin's icon.
        /// </summary>
        /// <value>the name of the plugin's icon</value>
        public string? IconName => manifest.IconPath;

        /// <summary>
        /// A link to this plugin's home page, if any.
        /// </summary>
        /// <value>the <see cref="Uri"/> of the plugin's home page</value>
        public Uri? PluginHomeLink => manifest.Links?.ProjectHome;

        /// <summary>
        /// A link to this plugin's source code, if avaliable.
        /// </summary>
        /// <value>the <see cref="Uri"/> of the plugin's source code</value>
        public Uri? PluginSourceLink => manifest.Links?.ProjectSource;
        /// <summary>
        /// A link to a donate page for the author of this plugin, if avaliable.
        /// </summary>
        /// <value>the <see cref="Uri"/> of the author's donate page</value>
        public Uri? DonateLink => manifest.Links?.Donate;

        internal bool IsSelf;

        /// <summary>
        /// Whether or not this metadata object represents a bare manifest.
        /// </summary>
        /// <value><see langword="true"/> if it is bare, <see langword="false"/> otherwise</value>
        public bool IsBare { get; internal set; }

        private PluginManifest manifest = null!;

        internal HashSet<PluginMetadata> Dependencies { get; } = new();
        internal HashSet<PluginMetadata> LoadsAfter { get; } = new();

        internal PluginManifest Manifest
        {
            get => manifest;
            set
            {
                manifest = value;
                AssociatedFiles = value.Files
                    .Select(f => Path.Combine(UnityGame.InstallPath, f))
                    .Select(p => new FileInfo(p)).ToList();
            }
        }

        /// <summary>
        /// The <see cref="IPA.RuntimeOptions"/> that the plugin specified in its <see cref="PluginAttribute"/>.
        /// </summary>
        public RuntimeOptions RuntimeOptions { get; internal set; }

        /// <summary>
        /// Gets all of the metadata as a readable string.
        /// </summary>
        /// <returns>the readable printable metadata string</returns>
        public override string ToString() => $"{Name}({Id}@{HVersion})({PluginType?.FullName}) from '{Utils.GetRelativePath(File?.FullName ?? "", UnityGame.InstallPath)}'";
    }
}