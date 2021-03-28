#nullable enable
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// Initializes the feature with the data provided in the definition.
        /// </summary>
        /// <remarks>
        /// <para>This gets called AFTER your <c>Init</c> method, but BEFORE the target's <c>Init</c> method. If it is applied to the defining plugin, <c>BeforeInit</c> is not called.</para>
        /// <para>Returning <see langword="false" /> does <i>not</i> prevent the plugin from being loaded. It simply prevents the feature from being used.</para>
        /// </remarks>
        /// <param name="meta">the metadata of the plugin that is being prepared</param>
        /// <param name="featureData">the data provided with the feature</param>
        /// <returns><see langword="true"/> if the feature is valid for the plugin, <see langword="false"/> otherwise</returns>
        protected abstract bool Initialize(PluginMetadata meta, JObject featureData);

        /// <summary>
        /// The message to be logged when the feature is not valid for a plugin.
        /// This should also be set whenever either <see cref="BeforeInit"/> returns false.
        /// </summary>
        /// <value>the message to show when the feature is marked invalid</value>
        public virtual string? InvalidMessage { get; protected set; }

        /// <summary>
        /// Called before a plugin's `Init` method is called. This will not be called if there is no `Init` method. This should never throw an exception. An exception will abort the loading of the plugin with an error.
        /// </summary>
        /// <param name="plugin">the plugin to be initialized</param>
        /// <returns>whether or not to call the Init method</returns>
        public virtual void BeforeInit(PluginMetadata plugin) { }

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
        /// Called after a plugin with this feature appplied is disabled.
        /// </summary>
        /// <param name="plugin">the plugin that was disabled</param>
        public virtual void AfterDisable(PluginMetadata plugin) { }

        // TODO: rework features to take arguments as JSON objects

        static Feature()
        {
            Reset();
        }

        internal static void Reset()
        {
            featureTypes = new()
            {
                { "IPA.DefineFeature", typeof(DefineFeature) }
            };
            featureDelcarers = new()
            {
                { "IPA.DefineFeature", null }
            };
        }

        private static Dictionary<string, Type> featureTypes = null!;
        private static Dictionary<string, PluginMetadata?> featureDelcarers = null!;

        internal static bool HasFeature(string name) => featureTypes.ContainsKey(name);

        internal static bool PreregisterFeature(PluginMetadata defining, string name)
        {
            if (featureDelcarers.ContainsKey(name)) return false;
            featureDelcarers.Add(name, defining);
            return true;
        }

        internal static bool RegisterFeature(PluginMetadata definingPlugin, string name, Type type)
        {
            if (!typeof(Feature).IsAssignableFrom(type))
                throw new ArgumentException($"Feature type not subclass of {nameof(Feature)}", nameof(type));

            if (featureTypes.ContainsKey(name)) return false;

            if (featureDelcarers.TryGetValue(name, out var declarer))
            {
                if (definingPlugin != declarer)
                    return false;
            }
            else
            {
                featureDelcarers.Add(name, definingPlugin);
            }

            featureTypes.Add(name, type);
            return true;
        }

        private class EmptyFeature : Feature
        {
            protected override bool Initialize(PluginMetadata meta, JObject featureData)
            {
                throw new NotImplementedException();
            }
        }

        internal string FeatureName = null!;

        internal class Instance
        {
            public readonly PluginMetadata AppliedTo;
            public readonly string Name;
            public readonly JObject Data;

            public Instance(PluginMetadata appliedTo, string name, JObject data)
            {
                AppliedTo = appliedTo;
                Name = name;
                Data = data;
                type = null;
            }

            private Type? type;
            public bool TryGetDefiningPlugin(out PluginMetadata? plugin)
            {
                return featureDelcarers.TryGetValue(Name, out plugin);
            }

            // returns whether or not Initialize returned true, feature is always set when the thing exists
            public bool TryCreate(out Feature feature)
            {
                if (type == null)
                {
                    if (!featureTypes.TryGetValue(Name, out type))
                    {
                        feature = new EmptyFeature() { InvalidMessage = "No such feature type found", FeatureName = Name };
                        return false;
                    }
                }

                bool result;
                try
                {
                    feature = (Feature)Activator.CreateInstance(type);
                    feature.FeatureName = Name;

                    result = feature.Initialize(AppliedTo, Data);
                }
                catch (Exception e)
                {
                    result = false;
                    feature = new EmptyFeature() { InvalidMessage = e.ToString(), FeatureName = Name };
                }
                return result;
            }
        }
    }
}