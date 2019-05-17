namespace IPA
{
    /// <summary>
    /// Provides methods to allow runtime enabling and disabling of a plugin.
    /// </summary>
    public interface IDisablablePlugin
    {
        /// <summary>
        /// Called when a plugin is enabled. This is where you should set up Harmony patches and the like.
        /// </summary>
        /// <remarks>
        /// This will be called after Init, and will be called when the plugin loads normally too.
        /// When a plugin is disabled at startup, neither this nor Init will be called until it is enabled.
        /// 
        /// Init will only ever be called once.
        /// </remarks>
        void OnEnable();

        /// <summary>
        /// Called when a plugin is disabled at runtime. This should disable things like Harmony patches and unsubscribe
        /// from events. After this is called there should be no lingering effects of the mod.
        /// </summary>
        /// <remarks>
        /// This will get called at shutdown, after <see cref="IBeatSaberPlugin.OnApplicationQuit"/>, as well as when the
        /// plugin is disabled at runtime.
        /// </remarks>
        void OnDisable();
    }
}
