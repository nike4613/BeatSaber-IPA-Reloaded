namespace IPA.Loader.Features
{
    /// <summary>
    /// The root interface for a mod Feature.
    /// </summary>
    public abstract class Feature
    {
        /// <summary>
        /// Initializes the feature with the parameters provided in the definition.
        ///
        /// Note: When no parenthesis are provided, <paramref name="parameters"/> is null.
        /// </summary>
        /// <param name="meta">the metadata of the plugin that is being prepared</param>
        /// <param name="parameters">the parameters passed to the feature definition, or null</param>
        /// <returns><see langword="true"/> if the feature is valid for the plugin, <see langword="false"/> otherwise</returns>
        public abstract bool Initialize(PluginLoader.PluginMetadata meta, string[] parameters);

        /// <summary>
        /// Called before a plugin is loaded.
        /// </summary>
        /// <param name="plugin">the plugin about to be loaded</param>
        /// <returns>whether or not the plugin should be loaded</returns>
        public virtual bool BeforeLoad(PluginLoader.PluginMetadata plugin) => true;

        /// <summary>
        /// Called before a plugin's Init method is called.
        /// </summary>
        /// <param name="plugin">the plugin to be initialized</param>
        /// <returns>whether or not to call the Init method</returns>
        public virtual bool BeforeInit(PluginLoader.PluginInfo plugin) => true;

        /// <summary>
        /// Called after a plugin has been fully initialized, whether or not there is an Init method.
        /// </summary>
        /// <param name="plugin">the plugin that was just initialized</param>
        public virtual void AfterInit(PluginLoader.PluginInfo plugin) { }
    }
}