using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Stores.Attributes
{
    /// <summary>
    /// Causes a field or property in an object being wrapped by <see cref="GeneratedExtension.Generated{T}(Config, bool)"/> to be
    /// ignored during serialization and deserialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class IgnoreAttribute : Attribute { }

    /// <summary>
    /// Indicates that a field or property in an object being wrapped by <see cref="GeneratedExtension.Generated{T}(Config, bool)"/>
    /// that would otherwise be nullable (i.e. a reference type or a <see cref="Nullable{T}"/> type) should never be null, and the 
    /// member will be ignored if the deserialized value is <see langword="null"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class NonNullableAttribute : Attribute { }

    /// <summary>
    /// Specifies a name for the serialized field or property in an object being wrapped by 
    /// <see cref="GeneratedExtension.Generated{T}(Config, bool)"/> that is different from the member name itself.
    /// </summary>
    /// <example>
    /// <para>
    /// When serializing the following object, we might get the JSON that follows.
    /// <code>
    /// public class PluginConfig 
    /// {
    ///     public virtual bool BooleanField { get; set; } = true;
    /// }
    /// </code>
    /// <code>
    /// {
    ///     "BooleanField": true
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// However, if we were to add a <see cref="SerializedNameAttribute"/> to that field, we would get the following.
    /// <code>
    /// public class PluginConfig 
    /// {
    ///     [SerializedName("bool")]
    ///     public virtual bool BooleanField { get; set; } = true;
    /// }
    /// </code>
    /// <code>
    /// {
    ///     "bool": true
    /// }
    /// </code>
    /// </para>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedNameAttribute : Attribute 
    {
        /// <summary>
        /// Gets the name to replace the member name with.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Creates a new <see cref="SerializedNameAttribute"/> with the given <see cref="Name"/>.
        /// </summary>
        /// <param name="name">the value to assign to <see cref="Name"/></param>
        public SerializedNameAttribute(string name)
        {
            Name = name;
        }
    }


}
