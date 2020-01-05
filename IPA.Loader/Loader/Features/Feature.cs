using System;
using System.Collections.Generic;
using System.Text;
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Loader.Features
{
    /// <summary>
    /// The root interface for a mod Feature.
    /// </summary>
    /// <remarks>
    /// Avoid storing any data in any subclasses. If you do, it may result in a failure to load the feature.
    /// </remarks>
    public abstract class Feature
    {
        /// <summary>
        /// Initializes the feature with the parameters provided in the definition.
        ///
        /// Note: When no parenthesis are provided, <paramref name="parameters"/> is an empty array.
        /// </summary>
        /// <remarks>
        /// This gets called BEFORE *your* `Init` method.
        /// 
        /// Returning <see langword="false" /> does *not* prevent the plugin from being loaded. It simply prevents the feature from being used.
        /// </remarks>
        /// <param name="meta">the metadata of the plugin that is being prepared</param>
        /// <param name="parameters">the parameters passed to the feature definition, or null</param>
        /// <returns><see langword="true"/> if the feature is valid for the plugin, <see langword="false"/> otherwise</returns>
        public abstract bool Initialize(PluginMetadata meta, string[] parameters);

        /// <summary>
        /// Evaluates the Feature for use in conditional meta-Features. This should be re-calculated on every call, unless it can be proven to not change.
        ///
        /// This will be called on every feature that returns <see langword="true" /> from <see cref="Initialize"/>
        /// </summary>
        /// <returns>the truthiness of the Feature.</returns>
        public virtual bool Evaluate() => true;

        /// <summary>
        /// The message to be logged when the feature is not valid for a plugin.
        /// This should also be set whenever either <see cref="BeforeLoad"/> or <see cref="BeforeInit"/> returns false.
        /// </summary>
        /// <value>the message to show when the feature is marked invalid</value>
        public virtual string InvalidMessage { get; protected set; }

        /// <summary>
        /// Called before a plugin is loaded. This should never throw an exception. An exception will abort the loading of the plugin with an error.
        /// </summary>
        /// <remarks>
        /// The assembly will still be loaded, but the plugin will not be constructed if this returns <see langword="false" />.
        /// Any features it defines, for example, will still be loaded.
        /// </remarks>
        /// <param name="plugin">the plugin about to be loaded</param>
        /// <returns>whether or not the plugin should be loaded</returns>
        public virtual bool BeforeLoad(PluginMetadata plugin) => true;

        /// <summary>
        /// Called before a plugin's `Init` method is called. This will not be called if there is no `Init` method. This should never throw an exception. An exception will abort the loading of the plugin with an error.
        /// </summary>
        /// <param name="plugin">the plugin to be initialized</param>
        /// <returns>whether or not to call the Init method</returns>
        public virtual bool BeforeInit(PluginMetadata plugin) => true;

        /// <summary>
        /// Called after a plugin has been fully initialized, whether or not there is an `Init` method. This should never throw an exception.
        /// </summary>
        /// <param name="plugin">the plugin that was just initialized</param>
        /// <param name="pluginInstance">the instance of the plugin being initialized</param>
        public virtual void AfterInit(PluginMetadata plugin, object pluginInstance) => AfterInit(plugin);

        /// <summary>
        /// Called after a plugin has been fully initialized, whether or not there is an `Init` method. This should never throw an exception.
        /// </summary>
        /// <param name="plugin">the plugin that was just initialized</param>
        public virtual void AfterInit(PluginMetadata plugin) { }

        /// <summary>
        /// Ensures a plugin's assembly is loaded. Do not use unless you need to.
        /// </summary>
        /// <param name="plugin">the plugin to ensure is loaded.</param>
        protected void RequireLoaded(PluginMetadata plugin) => PluginLoader.Load(plugin);

        /// <summary>
        /// Defines whether or not this feature will be accessible from the plugin metadata once loaded.
        /// </summary>
        /// <value><see langword="true"/> if this <see cref="Feature"/> will be stored on the plugin metadata, <see langword="false"/> otherwise</value>
        protected internal virtual bool StoreOnPlugin => true;

        static Feature()
        {
            Reset();
        }

        internal static void Reset()
        {
            featureTypes = new Dictionary<string, Type>
            {
                { "define-feature", typeof(DefineFeature) }
            };
        }

        private static Dictionary<string, Type> featureTypes;

        internal static bool HasFeature(string name) => featureTypes.ContainsKey(name);

        internal static bool RegisterFeature(string name, Type type)
        {
            if (!typeof(Feature).IsAssignableFrom(type))
                throw new ArgumentException($"Feature type not subclass of {nameof(Feature)}", nameof(type));
            if (featureTypes.ContainsKey(name)) return false;
            featureTypes.Add(name, type);
            return true;
        }

        internal struct FeatureParse
        {
            public readonly string Name;
            public readonly string[] Parameters;

            public FeatureParse(string name, string[] parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }

        // returns false with both outs null for no such feature
        internal static bool TryParseFeature(string featureString, PluginMetadata plugin,
            out Feature feature, out Exception failException, out bool featureValid, out FeatureParse parsed,
            FeatureParse? preParsed = null)
        {
            failException = null;
            feature = null;
            featureValid = false;

            if (preParsed == null)
            {
                var builder = new StringBuilder();
                string name = null;
                var parameters = new List<string>();

                bool escape = false;
                int parens = 0;
                bool removeWhitespace = true;
                foreach (var chr in featureString)
                {
                    if (escape)
                    {
                        builder.Append(chr);
                        escape = false;
                    }
                    else
                    {
                        switch (chr)
                        {
                            case '\\':
                                escape = true;
                                break;
                            case '(':
                                parens++;
                                if (parens != 1) goto default;
                                removeWhitespace = true;
                                name = builder.ToString();
                                builder.Clear();
                                break;
                            case ')':
                                parens--;
                                if (parens != 0) goto default;
                                goto case ',';
                            case ',':
                                if (parens > 1) goto default;
                                parameters.Add(builder.ToString());
                                builder.Clear();
                                removeWhitespace = true;
                                break;
                            default:
                                if (removeWhitespace && !char.IsWhiteSpace(chr))
                                    removeWhitespace = false;
                                if (!removeWhitespace)
                                    builder.Append(chr);
                                break;
                        }
                    }
                }

                if (name == null)
                    name = builder.ToString();

                parsed = new FeatureParse(name, parameters.ToArray());

                if (parens != 0)
                {
                    failException = new Exception("Malformed feature definition");
                    return false;
                }
            }
            else
                parsed = preParsed.Value;

            if (!featureTypes.TryGetValue(parsed.Name, out var featureType))
                return false;

            try
            {
                if (!(Activator.CreateInstance(featureType) is Feature aFeature))
                {
                    failException = new InvalidCastException("Feature type not a subtype of Feature");
                    return false;
                }

                featureValid = aFeature.Initialize(plugin, parsed.Parameters);
                feature = aFeature;
                return true;
            }
            catch (Exception e)
            {
                failException = e;
                return false;
            }
        }
    }
}