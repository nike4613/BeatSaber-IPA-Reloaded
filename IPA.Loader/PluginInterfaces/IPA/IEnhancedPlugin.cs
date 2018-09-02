using System;
using System.Collections.Generic;
using System.Text;

namespace IPA.Old
{
    /// <summary>
    /// An enhanced version of the standard IPA plugin.
    /// </summary>
    [Obsolete("When building plugins for Beat Saber, use IEnhancedBeatSaberPlugin")]
    public interface IEnhancedPlugin : IPlugin, IGenericEnhancedPlugin
    {
    }
}