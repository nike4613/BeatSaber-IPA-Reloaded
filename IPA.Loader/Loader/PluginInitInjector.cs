using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IPA.Config;
using IPA.Logging;
using IPA.Utilities;

namespace IPA.Loader
{
    /// <summary>
    /// The type that handles value injecting into a plugin's Init.
    /// </summary>
    public static class PluginInitInjector
    {

        /// <summary>
        /// A typed injector for a plugin's Init method. When registered, called for all associated types. If it returns null, the default for the type will be used.
        /// </summary>
        /// <param name="previous">the previous return value of the function, or <see langword="null"/> if never called for plugin.</param>
        /// <param name="param">the <see cref="ParameterInfo"/> of the parameter being injected.</param>
        /// <param name="meta">the <see cref="PluginLoader.PluginMetadata"/> for the plugin being loaded.</param>
        /// <returns>the value to inject into that parameter.</returns>
        public delegate object InjectParameter(object previous, ParameterInfo param, PluginLoader.PluginMetadata meta);

        /// <summary>
        /// Adds an injector to be used when calling future plugins' Init methods.
        /// </summary>
        /// <param name="type">the type of the parameter.</param>
        /// <param name="injector">the function to call for injection.</param>
        public static void AddInjector(Type type, InjectParameter injector)
        {
            injectors.Add(new Tuple<Type, InjectParameter>(type, injector));
        }

        private static readonly List<Tuple<Type, InjectParameter>> injectors = new List<Tuple<Type, InjectParameter>>
        {
            new Tuple<Type, InjectParameter>(typeof(Logger), (prev, param, meta) => prev ?? new StandardLogger(meta.Name)),
#pragma warning disable CS0618 // Type or member is obsolete
            new Tuple<Type, InjectParameter>(typeof(IModPrefs), (prev, param, meta) => prev ?? new ModPrefs(meta)),
#pragma warning restore CS0618 // Type or member is obsolete
            new Tuple<Type, InjectParameter>(typeof(PluginLoader.PluginMetadata), (prev, param, meta) => prev ?? meta),
            new Tuple<Type, InjectParameter>(typeof(IConfigProvider), (prev, param, meta) =>
            {
                if (prev != null) return prev;
                var cfgProvider = Config.Config.GetProviderFor(meta.Name, param);
                cfgProvider.Load();
                return cfgProvider;
            })
        };

        internal static void Inject(MethodInfo init, PluginLoader.PluginInfo info)
        {
            var instance = info.Plugin;
            var meta = info.Metadata;

            var initArgs = new List<object>();
            var initParams = init.GetParameters();

            Dictionary<Tuple<Type, InjectParameter>, object> previousValues =
                new Dictionary<Tuple<Type, InjectParameter>, object>(injectors.Count);

            foreach (var param in initParams)
            {
                var paramType = param.ParameterType;

                var value = paramType.GetDefault();
                foreach (var pair in injectors.Where(t => paramType.IsAssignableFrom(t.Item1)))
                {
                    object prev = null;
                    if (previousValues.ContainsKey(pair))
                        prev = previousValues[pair];

                    var val = pair.Item2?.Invoke(prev, param, meta);
                    
                    if (previousValues.ContainsKey(pair))
                        previousValues[pair] = val;
                    else
                        previousValues.Add(pair, val);

                    if (val == null) continue;
                    value = val;
                    break;
                }

                initArgs.Add(value);
            }

            init.Invoke(instance, initArgs.ToArray());
        }
    }
}
