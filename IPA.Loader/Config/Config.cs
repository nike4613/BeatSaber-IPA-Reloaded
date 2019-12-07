using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using IPA.Config.Providers;
using IPA.Utilities;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Config
{
    /// <summary>
    /// A class to handle updating ConfigProviders automatically
    /// </summary>
    public static class Config
    {
        static Config()
        {
            //JsonConfigProvider.RegisterConfig();
        }

        /// <inheritdoc />
        /// <summary>
        /// Defines the type of the <see cref="T:IPA.Config.IConfigProvider" />
        /// </summary>
        [AttributeUsage(AttributeTargets.Class)]
        public class TypeAttribute : Attribute
        {
            /// <summary>
            /// The extension associated with this type, without the '.'
            /// </summary>
            /// <value>the extension to register the config provider as</value>
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string Extension { get; private set; }

            /// <inheritdoc />
            /// <summary>
            /// Constructs the attribute with a specified extension.
            /// </summary>
            /// <param name="ext">the extension associated with this type, without the '.'</param>
            public TypeAttribute(string ext)
            {
                Extension = ext;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Specifies that a particular parameter is preferred to be a specific type of <see cref="T:IPA.Config.IConfigProvider" />. If it is not available, also specifies backups. If none are available, the default is used.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter)]
        public class PreferAttribute : Attribute
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

        /// <inheritdoc />
        /// <summary>
        /// Specifies a preferred config name, instead of using the plugin's name.
        /// </summary>
        public class NameAttribute : Attribute
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

        private static readonly Dictionary<string, Type> registeredProviders = new Dictionary<string, Type>();

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

            registeredProviders.Add(inst.Extension, type);
        }

        private static List<IConfigProvider> configProviders = new List<IConfigProvider>();
        private static ConditionalWeakTable<IConfigProvider, FileInfo> file = new ConditionalWeakTable<IConfigProvider, FileInfo>();

        /// <summary>
        /// Gets an <see cref="IConfigProvider"/> using the specified list of preferred config types.
        /// </summary>
        /// <param name="configName">the name of the mod for this config</param>
        /// <param name="extensions">the preferred config types to try to get</param>
        /// <returns>an <see cref="IConfigProvider"/> of the requested type, or of type JSON.</returns>
        public static IConfigProvider GetProviderFor(string configName, params string[] extensions)
        {
            var chosenExt = extensions.FirstOrDefault(s => registeredProviders.ContainsKey(s)) ?? "json";
            var type = registeredProviders[chosenExt];
            var provider = Activator.CreateInstance(type) as IConfigProvider;
            configProviders.Add(provider);

            // TODO: rething this one a bit

            return provider;
        }
        
        internal static IConfigProvider GetProviderFor(string modName, ParameterInfo info)
        {
            var prefs = new string[0];
            if (info.GetCustomAttribute<PreferAttribute>() is PreferAttribute prefer)
                prefs = prefer.PreferenceOrder;
            if (info.GetCustomAttribute<NameAttribute>() is NameAttribute name)
                modName = name.Name;

            return GetProviderFor(modName, prefs);
        }
    }
}
