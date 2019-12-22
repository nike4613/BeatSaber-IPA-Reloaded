using System;
// ReSharper disable CheckNamespace

namespace IPA.Old
{
    /// <inheritdoc cref="IPlugin" />
    /// <summary>
    /// An enhanced version of the standard IPA plugin.
    /// </summary>
    [Obsolete("When building plugins for Beat Saber, use IPA.IEnhancedPlugin")]
    public interface IEnhancedPlugin : IPlugin
    {
        /// <summary>
        /// Gets a list of executables this plugin should be executed on (without the file ending)
        /// </summary>
        /// <example>{ "PlayClub", "PlayClubStudio" }</example>
        string[] Filter { get; }

        /// <summary>
        /// Called after Update.
        /// </summary>
        void OnLateUpdate();
    }
}