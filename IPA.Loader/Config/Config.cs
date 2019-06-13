using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IPA.Config.ConfigProviders;
using IPA.Utilities;

namespace IPA.Config
{
    /// <summary>
    /// A class to handle updating ConfigProviders automatically
    /// </summary>
    public static class Config
    {
        static Config()
        {
            JsonConfigProvider.RegisterConfig();
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
            if (!(type.GetCustomAttribute(typeof(TypeAttribute)) is TypeAttribute ext))
                throw new InvalidOperationException("Type does not have TypeAttribute");

            if (!typeof(IConfigProvider).IsAssignableFrom(type))
                throw new InvalidOperationException("Type not IConfigProvider");

            if (registeredProviders.ContainsKey(ext.Extension))
                throw new InvalidOperationException($"Extension provider for {ext.Extension} already exists");

            registeredProviders.Add(ext.Extension, type);
        }

        private static List<Tuple<Ref<DateTime>, IConfigProvider>> configProviders = new List<Tuple<Ref<DateTime>, IConfigProvider>>();

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
            if (provider != null)
            {
                provider.Filename = Path.Combine(BeatSaber.UserDataPath, configName);
                configProviders.Add(Tuple.Create(Ref.Create(provider.LastModified), provider));
            }

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

        private static Dictionary<IConfigProvider, Action> linkedProviders =
            new Dictionary<IConfigProvider, Action>();

        /// <summary>
        /// Creates a linked <see cref="Ref{T}"/> for the config provider. This <see cref="Ref{T}"/> will be automatically updated whenever the file on-disk changes.
        /// </summary>
        /// <typeparam name="T">the type of the parsed value</typeparam>
        /// <param name="config">the <see cref="IConfigProvider"/> to create a link to</param>
        /// <param name="onChange">an action to perform on value change</param>
        /// <returns>a <see cref="Ref{T}"/> to an ever-changing value, mirroring whatever the file contains.</returns>
        public static Ref<T> MakeLink<T>(this IConfigProvider config, Action<IConfigProvider, Ref<T>> onChange = null)
        {
            Ref<T> @ref = config.Parse<T>();
            void ChangeDelegate()
            {
                @ref.Value = config.Parse<T>();
                onChange?.Invoke(config, @ref);
            }

            if (linkedProviders.ContainsKey(config))
                linkedProviders[config] = (Action) Delegate.Combine(linkedProviders[config], (Action) ChangeDelegate);
            else
                linkedProviders.Add(config, ChangeDelegate);

            ChangeDelegate();

            return @ref;
        }

        /// <summary>
        /// Removes all linked <see cref="Ref{T}"/> such that they are no longer updated.
        /// </summary>
        /// <param name="config">the <see cref="IConfigProvider"/> to unlink</param>
        public static void RemoveLinks(this IConfigProvider config)
        {
            if (linkedProviders.ContainsKey(config))
                linkedProviders.Remove(config);
        }

        internal static void Update()
        {
            foreach (var provider in configProviders)
            {
                if (provider.Item2.LastModified > provider.Item1.Value)
                {
                    try
                    {
                        provider.Item2.Load(); // auto reload if it changes
                        provider.Item1.Value = provider.Item2.LastModified;
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.config.Error("Error when trying to load config");
                        Logging.Logger.config.Error(e);
                    }
                }
                if (provider.Item2.HasChanged)
                {
                    try
                    {
                        provider.Item2.Save();
                        provider.Item1.Value = DateTime.Now;
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.config.Error("Error when trying to save config");
                        Logging.Logger.config.Error(e);
                    }
                }

                if (provider.Item2.InMemoryChanged)
                {
                    provider.Item2.InMemoryChanged = false;
                    try
                    {
                        if (linkedProviders.ContainsKey(provider.Item2))
                            linkedProviders[provider.Item2]();
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.config.Error("Error running link change events");
                        Logging.Logger.config.Error(e);
                    }
                }
            }
        }

        internal static void Save()
        {
            foreach (var provider in configProviders)
                if (provider.Item2.HasChanged)
                    try
                    {
                        provider.Item2.Save();
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.config.Error("Error when trying to save config");
                        Logging.Logger.config.Error(e);
                    }
        }

    }
}
