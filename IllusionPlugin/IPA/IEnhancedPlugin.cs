using System;
using System.Collections.Generic;
using System.Text;

namespace IllusionPlugin
{
    [Obsolete("When building plugins for Beat Saber, use IEnhancedBeatSaberPlugin")]
    public interface IEnhancedPlugin : IPlugin, IGenericEnhancedPlugin
    {
    }
}