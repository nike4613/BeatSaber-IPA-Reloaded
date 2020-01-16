using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Loader
{
    /// <summary>
    /// Indicates that a plugin cannot be disabled at runtime. Generally not considered an error, however.
    /// </summary>
    [Serializable]
    public class CannotRuntimeDisableException : Exception
    {
        /// <summary>
        /// The plugin that cannot be disabled at runtime.
        /// </summary>
        public PluginMetadata Plugin { get; }
        /// <summary>
        /// Creates an exception for the given plugin metadata.
        /// </summary>
        /// <param name="plugin">the plugin that cannot be disabled</param>
        public CannotRuntimeDisableException(PluginMetadata plugin) : base($"Cannot runtime disable plugin \"{plugin.Name}\" ({plugin.Id})")
            => Plugin = plugin;

        /// <summary>
        /// Creates an exception from a serialization context. Not currently implemented.
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        /// <exception cref="NotImplementedException"></exception>
        protected CannotRuntimeDisableException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }
}
