using IPA.Loader;
using System;

namespace IPA
{
    /// <summary>
    /// Marks a class as being a BSIPA plugin.
    /// </summary>
    /// <seealso cref="InitAttribute"/>
    /// <seealso cref="OnEnableAttribute"/>
    /// <seealso cref="OnDisableAttribute"/>
    /// <seealso cref="OnStartAttribute"/>
    /// <seealso cref="OnExitAttribute"/>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PluginAttribute : Attribute
    {
        /// <summary>
        /// The <see cref="IPA.RuntimeOptions"/> passed into the constructor of this attribute.
        /// </summary>
        // whenever this changes, PluginLoader.LoadMetadata must also change
        public RuntimeOptions RuntimeOptions { get; }
        /// <summary>
        /// Initializes a <see cref="PluginAttribute"/> with the given <see cref="IPA.RuntimeOptions"/>
        /// to indicate the runtime capabilities of the plugin.
        /// </summary>
        /// <param name="runtimeOptions">the options to use for this plugin</param>
        public PluginAttribute(RuntimeOptions runtimeOptions)
        {
            RuntimeOptions = runtimeOptions;
        }
    }

    /// <summary>
    /// Options that a plugin must specify to describe how it expects to be run.
    /// </summary>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="InitAttribute"/>
    /// <seealso cref="OnEnableAttribute"/>
    /// <seealso cref="OnDisableAttribute"/>
    /// <seealso cref="OnStartAttribute"/>
    /// <seealso cref="OnExitAttribute"/>
    // TODO: figure out a better name for this
    public enum RuntimeOptions
    {
        /// <summary>
        /// <para>
        /// Indicates that this plugin expects to be initialized and enabled with the game, and disabled with the game.
        /// </para>
        /// <para>
        /// With this option set, whether or not the plugin is disabled during a given run is constant for that entire run.
        /// </para>
        /// </summary>
        // enabled exactly once and never disabled
        SingleStartInit,
        /// <summary>
        /// <para>
        /// Indicates that this plugin supports runtime enabling and disabling.
        /// </para>
        /// <para>
        /// When this is set, the plugin may be disabled at reasonable points during runtime. As with <see cref="SingleStartInit"/>,
        /// it will be initialized and enabled with the game if it is enabled on startup, and disabled with the game if it is enabled
        /// on shutdown.
        /// </para>
        /// <para>
        /// When a plugin with this set is enabled mid-game, the first time it is enabled, its initialization methods will be called,
        /// then its enable methods. All subsequent enables will <b>NOT</b> re-initialize, however the enable methods will be called.
        /// </para>
        /// <para>
        /// When a plugin with this set is disabled mid-game, the plugin instance will <b>NOT</b> be destroyed, and will instead be
        /// re-used for subsequent enables. The plugin is expected to handle this gracefully, and behave in a way that makes sense.
        /// </para>
        /// </summary>
        // both enabled and disabled at runtime
        DynamicInit
    }

    /// <summary>
    /// Marks a method or a constructor as an inialization method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If more than one constructor is marked with this attribute, the one with the most parameters, whether or not they can be injected, will be used.
    /// </para>
    /// <para>
    /// Parameter injection is done with <see cref="PluginInitInjector"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="PluginInitInjector"/>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class InitAttribute : Attribute { }
}
