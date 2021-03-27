#if !NETSTANDARD2_1
namespace System.Diagnostics.CodeAnalysis
{
    // Effectively the Microsoft implementation for when it doesn't exist for my convenience

    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>,
    /// the parameter may be <see langword="null"/> even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes the attribute with the specified return value condition.
        /// </summary>
        /// <param name="returnValue">The return value condition. If the method returns this
        /// value, the associated parameter may be null.</param>
        public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>
        /// Gets the return value condition.
        /// </summary>
        /// <value>The return value condition. If the method returns this value, the
        /// associated parameter may be null.</value>
        public bool ReturnValue { get; }
    }
    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue"/>,
    /// the parameter is not <see langword="null"/> even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes the attribute with the specified return value condition.
        /// </summary>
        /// <param name="returnValue">The return value condition. If the method returns this
        /// value, the associated parameter is not null.</param>
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>
        /// Gets the return value condition.
        /// </summary>
        /// <value>The return value condition. If the method returns this value, the
        /// associated parameter is not null.</value>
        public bool ReturnValue { get; }
    }
    /// <summary>
    /// Specifies that an output may be <see langword="null"/> even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class MaybeNullAttribute : Attribute
    {
    }
    /// <summary>
    /// Specifies that the method will not return if the associated Boolean parameter is passed the specified value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class DoesNotReturnIfAttribute : Attribute
    {
        /// <summary>
        /// Initializes the attribute with the specified parameter value.
        /// </summary>
        /// <param name="parameterValue">
        /// The condition parameter value. Code after the method will be considered unreachable by diagnostics if the argument to
        /// the associated parameter matches this value.
        /// </param>
        public DoesNotReturnIfAttribute(bool parameterValue) => ParameterValue = parameterValue;

        /// <summary>
        /// Gets the condition parameter value.
        /// </summary>
        public bool ParameterValue { get; }
    }
}
#endif
