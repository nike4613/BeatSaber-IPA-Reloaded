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
    [Obsolete("Use the attribute-based system instead.")]
    public interface IEnhancedPlugin : IPlugin
    {

        /// <summary>
        /// Gets invoked on every graphic update.
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Gets invoked on ever physics update.
        /// </summary>
        void OnFixedUpdate();

        /// <summary>
        /// Called after Update.
        /// </summary>
        void OnLateUpdate();
    }
}
