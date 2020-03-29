using System;
// ReSharper disable CheckNamespace

namespace IPA.Old
{
    /// <summary>
    /// Interface for generic Illusion unity plugins. Every class that implements this will be loaded if the DLL is placed in
    /// Plugins.
    /// </summary>
    [Obsolete("When building plugins for Beat Saber, use the plugin attributes starting with PluginAttribute")]
    public interface IPlugin
    {
        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets invoked when the application is started.
        /// </summary>
        void OnApplicationStart();

        /// <summary>
        /// Gets invoked when the application is closed.
        /// </summary>
        void OnApplicationQuit();

        /// <summary>
        /// Gets invoked whenever a level is loaded.
        /// </summary>
        /// <param name="level"></param>
        void OnLevelWasLoaded(int level);

        /// <summary>
        /// Gets invoked after the first update cycle after a level was loaded.
        /// </summary>
        /// <param name="level"></param>
        void OnLevelWasInitialized(int level);

        /// <summary>
        /// Gets invoked on every graphic update.
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Gets invoked on ever physics update.
        /// </summary>
        void OnFixedUpdate();
    }
}