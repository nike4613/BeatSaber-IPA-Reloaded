using IPA.Config.Data;
using System;

namespace IPA.Config.Stores.Converters
{
    /// <summary>
    /// A <see cref="ValueConverter{T}"/> for objects normally serialized to config via <see cref="GeneratedStore.Generated{T}(Config, bool)"/>.
    /// </summary>
    /// <typeparam name="T">the same type parameter that would be passed into <see cref="GeneratedStore.Generated{T}(Config, bool)"/></typeparam>
    /// <seealso cref="GeneratedStore.Generated{T}(Config, bool)"/>
    public class CustomObjectConverter<T> : ValueConverter<T> where T : class
    {
        private interface IImpl
        {
            T FromValue(Value value, object parent);
            Value ToValue(T obj, object parent);
        }
        private class Impl<U> : IImpl where U : class, GeneratedStoreImpl.IGeneratedStore<T>, T
        {
            private static readonly GeneratedStoreImpl.GeneratedStoreCreator creator = GeneratedStoreImpl.GetCreator(typeof(T));
            private static U Create(GeneratedStoreImpl.IGeneratedStore parent)
                => creator(parent) as U;

            public T FromValue(Value value, object parent)
            { // lots of casting here, but it works i promise (probably) (parent can be a non-IGeneratedStore, however it won't necessarily behave then)
                var obj = Create(parent as GeneratedStoreImpl.IGeneratedStore);
                obj.Deserialize(value);
                return obj;
            }

            public Value ToValue(T obj, object parent)
            {
                if (obj is GeneratedStoreImpl.IGeneratedStore store)
                    return store.Serialize();
                else
                {
                    var newObj = Create(null);
                    newObj.CopyFrom(obj, false); // don't use lock because it won't be used
                    return newObj.Serialize();
                }
            }
        }

        private static readonly IImpl impl = (IImpl)Activator.CreateInstance(
            typeof(Impl<>).MakeGenericType(GeneratedStoreImpl.GetGeneratedType(typeof(T))));

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
