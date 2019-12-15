using IPA.Config.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Stores.Converters
{
    /// <summary>
    /// Provides utility functions for custom converters.
    /// </summary>
    public static class Converter 
    {
        /// <summary>
        /// Gets the integral value of a <see cref="Value"/>, coercing a <see cref="FloatingPoint"/> if necessary,
        /// or <see langword="null"/> if <paramref name="val"/> is not an <see cref="Integer"/> or <see cref="FloatingPoint"/>.
        /// </summary>
        /// <param name="val">the <see cref="Value"/> to get the integral value of</param>
        /// <returns>the integral value of <paramref name="val"/>, or <see langword="null"/></returns>
        public static long? IntValue(Value val)
            => val is Integer inte ? inte.Value :
               val is FloatingPoint fp ? fp.AsInteger()?.Value :
               null;
        /// <summary>
        /// Gets the floaing point value of a <see cref="Value"/>, coercing an <see cref="Integer"/> if necessary,
        /// or <see langword="null"/> if <paramref name="val"/> is not an <see cref="Integer"/> or <see cref="FloatingPoint"/>.
        /// </summary>
        /// <param name="val">the <see cref="Value"/> to get the floaing point value of</param>
        /// <returns>the floaing point value of <paramref name="val"/>, or <see langword="null"/></returns>
        public static decimal? FloatValue(Value val)
            => val is FloatingPoint fp ? fp.Value :
               val is Integer inte ? inte.AsFloat()?.Value :
               null;
    }

    /// <summary>
    /// A <see cref="ValueConverter{T}"/> for objects normally serialized to config via <see cref="GeneratedExtension.Generated{T}(Config, bool)"/>.
    /// </summary>
    /// <typeparam name="T">the same type parameter that would be passed into <see cref="GeneratedExtension.Generated{T}(Config, bool)"/></typeparam>
    /// <seealso cref="GeneratedExtension.Generated{T}(Config, bool)"/>
    public class CustomObjectConverter<T> : ValueConverter<T> where T : class
    {
        private interface IImpl
        {
            T FromValue(Value value, object parent);
            Value ToValue(T obj, object parent);
        }
        private class Impl<U> : IImpl where U : class, GeneratedStore.IGeneratedStore, T
        {
            private static readonly GeneratedStore.GeneratedStoreCreator creator = GeneratedStore.GetCreator(typeof(T));

            public T FromValue(Value value, object parent)
            { // lots of casting here, but it works i promise (parent can be a non-IGeneratedStore, however it won't necessarily behave then)
                var obj = creator(parent as GeneratedStore.IGeneratedStore) as U;
                obj.Deserialize(value);
                return obj;
            }

            public Value ToValue(T obj, object parent)
            {
                if (obj is GeneratedStore.IGeneratedStore store)
                    return store.Serialize();
                else
                    return null; // TODO: make this behave sanely instead of just giving null
            }
        }

        private static readonly IImpl impl = (IImpl)Activator.CreateInstance(
            typeof(Impl<>).MakeGenericType(GeneratedStore.GetGeneratedType(typeof(T))));

        /// <summary>
        /// Deserializes <paramref name="value"/> into a <typeparamref name="T"/> with the given <paramref name="parent"/>.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to deserialize</param>
        /// <param name="parent">the parent object that will own the deserialized value</param>
        /// <returns>the deserialized value</returns>
        /// <seealso cref="ValueConverter{T}.FromValue(Value, object)"/>
        public static T Deserialize(Value value, object parent)
            => impl.FromValue(value, parent);

        /// <summary>
        /// Serializes <paramref name="obj"/> into a <see cref="Value"/> structure, given <paramref name="parent"/>.
        /// </summary>
        /// <param name="obj">the object to serialize</param>
        /// <param name="parent">the parent object that owns <paramref name="obj"/></param>
        /// <returns>the <see cref="Value"/> tree that represents <paramref name="obj"/></returns>
        /// <seealso cref="ValueConverter{T}.ToValue(T, object)"/>
        public static Value Serialize(T obj, object parent)
            => impl.ToValue(obj, parent);

        /// <summary>
        /// Deserializes <paramref name="value"/> into a <typeparamref name="T"/> with the given <paramref name="parent"/>.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to deserialize</param>
        /// <param name="parent">the parent object that will own the deserialized value</param>
        /// <returns>the deserialized value</returns>
        /// <seealso cref="ValueConverter{T}.FromValue(Value, object)"/>
        public override T FromValue(Value value, object parent)
            => impl.FromValue(value, parent);

        /// <summary>
        /// Serializes <paramref name="obj"/> into a <see cref="Value"/> structure, given <paramref name="parent"/>.
        /// </summary>
        /// <param name="obj">the object to serialize</param>
        /// <param name="parent">the parent object that owns <paramref name="obj"/></param>
        /// <returns>the <see cref="Value"/> tree that represents <paramref name="obj"/></returns>
        /// <seealso cref="ValueConverter{T}.ToValue(T, object)"/>
        public override Value ToValue(T obj, object parent)
            => impl.ToValue(obj, parent);
    }

}
