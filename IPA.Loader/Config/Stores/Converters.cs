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
            { // lots of casting here, but it works i promise
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

        public static T Deserialize(Value value, object parent)
            => impl.FromValue(value, parent);

        public static Value Serialize(T obj, object parent)
            => impl.ToValue(obj, parent);

        public override T FromValue(Value value, object parent)
            => impl.FromValue(value, parent);

        public override Value ToValue(T obj, object parent)
            => impl.ToValue(obj, parent);
    }

}
