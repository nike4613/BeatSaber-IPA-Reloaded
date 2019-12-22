namespace IPA
{
    /// <summary>
    /// Provides methods to allow runtime disabling of a plugin.
    /// </summary>
    public interface IDisablablePlugin : IPlugin
    {

        /// <summary>
        /// Called when a plugin is disabled at runtime. This should disable things like Harmony patches and unsubscribe
        /// from events. After this is called there should be no lingering effects of the mod.
        /// </summary>
        /// <remarks>
        /// This will get called at shutdown, after <see cref="IPlugin.OnApplicationQuit"/>, as well as when the
        /// plugin is disabled at runtime.
        /// </remarks>
        void OnDisable();
    }
}
