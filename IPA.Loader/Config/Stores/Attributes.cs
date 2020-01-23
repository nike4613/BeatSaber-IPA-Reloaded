using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Stores.Attributes
{
    /// <summary>
    /// Indicates that the generated subclass of the attribute's target should implement <see cref="INotifyPropertyChanged"/>.
    /// If the type this is applied to already inherits it, this is implied.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class NotifyPropertyChangesAttribute : Attribute { }

    /// <summary>
    /// Causes a field or property in an object being wrapped by <see cref="GeneratedStore.Generated{T}(Config, bool)"/> to be
    /// ignored during serialization and deserialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class IgnoreAttribute : Attribute { }

    /// <summary>
    /// Indicates that a field or property in an object being wrapped by <see cref="GeneratedStore.Generated{T}(Config, bool)"/>
    /// that would otherwise be nullable (i.e. a reference type or a <see cref="Nullable{T}"/> type) should never be null, and the 
    /// member will be ignored if the deserialized value is <see langword="null"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class NonNullableAttribute : Attribute { }

    /// <summary>
    /// Indicates that a given field or property in an object being wrapped by <see cref="GeneratedStore.Generated{T}(Config, bool)"/>
    /// should be serialized and deserialized using the provided converter instead of the default mechanism. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class UseConverterAttribute : Attribute
    {
        /// <summary>
        /// Gets the type of the converter to use.
        /// </summary>
        public Type ConverterType { get; }
        /// <summary>
        /// Gets the target type of the converter if it is avaliable at instantiation time, otherwise
        /// <see langword="null"/>.
        /// </summary>
        public Type ConverterTargetType { get; }

        /// <summary>
        /// Gets whether or not this converter is a generic <see cref="ValueConverter{T}"/>.
        /// </summary>
        public bool IsGenericConverter => ConverterTargetType != null;

        /// <summary>
        /// Creates a new <see cref="UseConverterAttribute"/> with a  given <see cref="ConverterType"/>.
        /// </summary>
        /// <param name="converterType">the type to assign to <see cref="ConverterType"/></param>
        public UseConverterAttribute(Type converterType)
        {
            ConverterType = converterType;

            var baseT = ConverterType.BaseType;
            while (baseT != null && baseT != typeof(object) && 
                (!baseT.IsGenericType || baseT.GetGenericTypeDefinition() != typeof(ValueConverter<>)))
                baseT = baseT.BaseType;
            if (baseT == typeof(object)) ConverterTargetType = null;
            else ConverterTargetType = baseT.GetGenericArguments()[0];

            var implInterface = ConverterType.GetInterfaces().Contains(typeof(IValueConverter));

            if (ConverterTargetType == null && !implInterface) throw new ArgumentException("Type is not a value converter!");
        }
    }

    /// <summary>
    /// Specifies a name for the serialized field or property in an object being wrapped by 
    /// <see cref="GeneratedStore.Generated{T}(Config, bool)"/> that is different from the member name itself.
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
