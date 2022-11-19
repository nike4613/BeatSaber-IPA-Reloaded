using System;

namespace IPA
{
    internal enum EdgeLifecycleType
    {
        Enable, Disable
    }

    internal interface IEdgeLifecycleAttribute
    {
        EdgeLifecycleType Type { get; }
    }

    // TODO: is there a better way to manage this mess?

    /// <summary>
    /// Indicates that the target method should be called when the plugin is enabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is interchangable with <see cref="OnStartAttribute"/>, and is treated identically.
    /// They are seperate to allow plugin code to more clearly describe the intent of the methods.
    /// </para>
    /// <para>
    /// Typically, this will be used when the <see cref="RuntimeOptions"/> parameter of the plugins's
    /// <see cref="PluginAttribute"/> is <see cref="RuntimeOptions.DynamicInit"/>.
    /// </para>
    /// <para>
    /// The method marked by this attribute will always be called from the Unity main thread.
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="OnStartAttribute"/>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnEnableAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Enable;
    }

    /// <summary>
    /// Indicates that the target method should be called when the game starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is interchangable with <see cref="OnEnableAttribute"/>, and is treated identically.
    /// They are seperate to allow plugin code to more clearly describe the intent of the methods.
    /// </para>
    /// <para>
    /// Typically, this will be used when the <see cref="RuntimeOptions"/> parameter of the plugins's
    /// <see cref="PluginAttribute"/> is <see cref="RuntimeOptions.SingleStartInit"/>.
    /// </para>
    /// <para>
    /// The method marked by this attribute will always be called from the Unity main thread.
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="OnEnableAttribute"/>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnStartAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Enable;
    }

    /// <summary>
    /// Indicates that the target method should be called when the plugin is disabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is interchangable with <see cref="OnExitAttribute"/>, and is treated identically.
    /// They are seperate to allow plugin code to more clearly describe the intent of the methods.
    /// </para>
    /// <para>
    /// Typically, this will be used when the <see cref="RuntimeOptions"/> parameter of the plugins's
    /// <see cref="PluginAttribute"/> is <see cref="RuntimeOptions.DynamicInit"/>.
    /// </para>
    /// <para>
    /// The method marked by this attribute will always be called from the Unity main thread.
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="OnExitAttribute"/>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnDisableAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Disable;
    }

    /// <summary>
    /// Indicates that the target method should be called when the game exits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is interchangable with <see cref="OnDisableAttribute"/>, and is treated identically.
    /// They are seperate to allow plugin code to more clearly describe the intent of the methods.
    /// </para>
    /// <para>
    /// Typically, this will be used when the <see cref="RuntimeOptions"/> parameter of the plugins's
    /// <see cref="PluginAttribute"/> is <see cref="RuntimeOptions.SingleStartInit"/>.
    /// </para>
    /// <para>
    /// The method marked by this attribute will always be called from the Unity main thread.
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginAttribute"/>
    /// <seealso cref="OnDisableAttribute"/>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OnExitAttribute : Attribute, IEdgeLifecycleAttribute
    {
        EdgeLifecycleType IEdgeLifecycleAttribute.Type => EdgeLifecycleType.Disable;
    }

    /// <summary>
    /// Indicates that the applied plugin class does not need <see cref="OnEnableAttribute"/> or
    /// <see cref="OnDisableAttribute"/> methods.
    /// </summary>
    /// <remarks>
    /// This is typically only the case when some other utility mod handles their lifecycle for
    /// them, such as with SiraUtil and Zenject.
    /// </remarks>
    /// <seealso cref="OnEnableAttribute"/>
    /// <seealso cref="OnDisableAttribute"/>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NoEnableDisableAttribute : Attribute
    {

    }
}
