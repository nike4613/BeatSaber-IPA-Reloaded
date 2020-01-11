using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using IPA.Config.Providers;
using IPA.Utilities;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config
{
    /// <summary>
    /// An abstraction of a config file on disk, which handles synchronizing between a memory representation and the
    /// disk representation.
    /// </summary>
    public class Config
    {
        static Config()
        {
            JsonConfigProvider.RegisterConfig();
        }

        /// <summary>
        /// Specifies that a particular parameter is preferred to use a particular <see cref="IConfigProvider" />. 
        /// If it is not available, also specifies backups. If none are available, the default is used.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter)]
        public sealed class PreferAttribute : Attribute
        {
            /// <summary>
            /// The order of preference for the config type. 
            /// </summary>
            /// <value>the list of config extensions in order of preference</value>
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string[] PreferenceOrder { get; private set; }

            /// <inheritdoc />
            /// <summary>
            /// Constructs the attribute with a specific preference list. Each entry is the extension without a '.'
            /// </summary>
            /// <param name="preference">The preferences in order of preference.</param>
            public PreferAttribute(params string[] preference)
            {
                PreferenceOrder = preference;
            }
        }

        /// <summary>
        /// Specifies a preferred config name, instead of using the plugin's name.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter)]
        public sealed class NameAttribute : Attribute
        {
            /// <summary>
            /// The name to use for the config.
            /// </summary>
            /// <value>the name to use for the config</value>
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string Name { get; private set; }

            /// <inheritdoc />
            /// <summary>
            /// Constructs the attribute with a specific name.
            /// </summary>
            /// <param name="name">the name to use for the config.</param>
            public NameAttribute(string name)
            {
                Name = name;
            }
        }

        private static readonly Dictionary<string, IConfigProvider> registeredProviders = new Dictionary<string, IConfigProvider>();

        /// <summary>
        /// Registers a <see cref="IConfigProvider"/> to use for configs.
        /// </summary>
        /// <typeparam name="T">the type to register</typeparam>
        public static void Register<T>() where T : IConfigProvider => Register(typeof(T));

        /// <summary>
        /// Registers a <see cref="IConfigProvider"/> to use for configs.
        /// </summary>
        /// <param name="type">the type to register</param>
        public static void Register(Type type)
        {
            var inst = Activator.CreateInstance(type) as IConfigProvider;
            if (inst == null)
                throw new ArgumentException($"Type not an {nameof(IConfigProvider)}");

            if (registeredProviders.ContainsKey(inst.Extension))
                throw new InvalidOperationException($"Extension provider for {inst.Extension} already exists");

            registeredProviders.Add(inst.Extension, inst);
        }

        /// <summary>
        /// Gets a <see cref="Config"/> object using the specified list of preferred config types.
        /// </summary>
        /// <param name="configName">the name of the mod for this config</param>
        /// <param name="extensions">the preferred config types to try to get</param>
        /// <returns>a <see cref="Config"/> using the requested format, or of type JSON.</returns>
        public static Config GetConfigFor(string configName, params string[] extensions)
        {
            var chosenExt = extensions.FirstOrDefault(s => registeredProviders.ContainsKey(s)) ?? "json";
            var provider = registeredProviders[chosenExt];

            var filename = Path.Combine(UnityGame.UserDataPath, configName + "." + provider.Extension);
            var config = new Config(configName, provider, new FileInfo(filename));

            ConfigRuntime.RegisterConfig(config);

            return config;
        }
        
        internal static Config GetConfigFor(string modName, ParameterInfo info)
        {
            var prefs = Array.Empty<string>();
            if (info.GetCustomAttribute<PreferAttribute>() is PreferAttribute prefer)
                prefs = prefer.PreferenceOrder;
            if (info.GetCustomAttribute<NameAttribute>() is NameAttribute name)
                modName = name.Name;

            return GetConfigFor(modName, prefs);
        }

        /// <summary>
        /// Gets the name associated with this <see cref="Config"/> object.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Gets the <see cref="IConfigProvider"/> associated with this <see cref="Config"/> object.
        /// </summary>
        public IConfigProvider Provider { get; }

        internal IConfigStore Store = null;
        internal readonly FileInfo File;
        internal readonly ConfigProvider configProvider;
        internal int Writes = 0;

        /// <summary>
        /// Sets this object's <see cref="IConfigStore"/>. Can only be called once.
        /// </summary>
        /// <param name="store">the <see cref="IConfigStore"/> to add to this instance</param>
        /// <exception cref="InvalidOperationException">If this was called before.</exception>
        public void SetStore(IConfigStore store)
        {
            if (Store != null)
                throw new InvalidOperationException($"{nameof(SetStore)} can only be called once");
            Store = store;
            ConfigRuntime.ConfigChanged();
        }

        /// <summary>
        /// Forces a synchronous load from disk.
        /// </summary>
        public void LoadSync() => LoadAsync().Wait();

        /// <summary>
        /// Forces an asynchronous load from disk.
        /// </summary>
        public Task LoadAsync() => ConfigRuntime.TriggerFileLoad(this);

        private Config(string name, IConfigProvider provider, FileInfo file)
        {
            Name = name; Provider = provider; File = file; 
            configProvider = new ConfigProvider(file, provider);
        }
    }
}
