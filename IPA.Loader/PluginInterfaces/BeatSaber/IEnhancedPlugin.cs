using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA
{
    /// <inheritdoc cref="IPlugin" />
    /// <summary>
    /// An enhanced version of a standard BeatSaber plugin.
    /// </summary>
    public interface IEnhancedPlugin : IPlugin, IGenericEnhancedPlugin
    {
    }
}
