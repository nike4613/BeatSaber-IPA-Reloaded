using System;
// ReSharper disable CheckNamespace

namespace IPA.Old
{
    /// <inheritdoc cref="IPlugin" />
    /// <summary>
    /// An enhanced version of the standard IPA plugin.
    /// </summary>
    [Obsolete("When building plugins for Beat Saber, use IEnhancedBeatSaberPlugin")]
    public interface IEnhancedPlugin : IPlugin, IGenericEnhancedPlugin
    {
    }
}