using System;
// ReSharper disable CheckNamespace

namespace IPA
{
    /// <summary>
    /// Interface for Beat Saber plugins. Every class that implements this will be loaded if the DLL is placed at
    /// data/Managed/Plugins.
    /// </summary>
    [Obsolete("Use IPA.IPlugin instead")]
    public interface IBeatSaberPlugin : _IPlugin
    {
        /// <summary>
        /// Gets invoked when the application is started.
        /// 
        /// THIS EVENT WILL NOT BE GUARANTEED TO FIRE. USE Init OR <see cref="IPlugin.OnEnable"/> INSTEAD.
        /// </summary>
        void OnApplicationStart();
    }
}
