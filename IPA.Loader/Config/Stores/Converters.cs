using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Boolean = IPA.Config.Data.Boolean;

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

        internal interface IValConv<T>
        {
            ValueConverter<T> Get();
        }
        internal class ValConv<T> : IValConv<T> where T : struct
        {
            private static readonly IValConv<T> Impl = ValConvImpls.Impl as IValConv<T> ?? new ValConv<T>();
            public ValueConverter<T> Get() => Impl.Get();
            ValueConverter<T> IValConv<T>.Get()
                => null; // default to null
        }
        private class ValConvImpls : IValConv<char>,
            IValConv<IntPtr>, IValConv<UIntPtr>,
            IValConv<long>, IValConv<ulong>,
            IValConv<int>, IValConv<uint>,
            IValConv<short>, IValConv<ushort>,
            IValConv<sbyte>, IValConv<byte>,
            IValConv<float>, IValConv<double>,
            IValConv<decimal>, IValConv<bool>
        {
            internal static readonly ValConvImpls Impl = new ValConvImpls();
            ValueConverter<char> IValConv<char>.Get() => new CharConverter();
            ValueConverter<long> IValConv<long>.Get() => new LongConverter();
            ValueConverter<ulong> IValConv<ulong>.Get() => new ULongConverter();
            ValueConverter<IntPtr> IValConv<IntPtr>.Get() => new IntPtrConverter();
            ValueConverter<UIntPtr> IValConv<UIntPtr>.Get() => new UIntPtrConverter();
            ValueConverter<int> IValConv<int>.Get() => new IntConverter();
            ValueConverter<uint> IValConv<uint>.Get() => new UIntConverter();
            ValueConverter<short> IValConv<short>.Get() => new ShortConverter();
            ValueConverter<ushort> IValConv<ushort>.Get() => new UShortConverter();
            ValueConverter<byte> IValConv<byte>.Get() => new ByteConverter();
            ValueConverter<sbyte> IValConv<sbyte>.Get() => new SByteConverter();
            ValueConverter<float> IValConv<float>.Get() => new FloatConverter();
            ValueConverter<double> IValConv<double>.Get() => new DoubleConverter();
            ValueConverter<decimal> IValConv<decimal>.Get() => new DecimalConverter();
            ValueConverter<bool> IValConv<bool>.Get() => new BooleanConverter();
        }
    }

    /// <summary>
    /// Provides generic utilities for converters for certain types.
    /// </summary>
    /// <typeparam name="T">the type of the <see cref="ValueConverter{T}"/> that this works on</typeparam>
    public static class Converter<T>
    {
        private static ValueConverter<T> defaultConverter = null;
        /// <summary>
        /// Gets the default <see cref="ValueConverter{T}"/> for the current type.
        /// </summary>
        public static ValueConverter<T> Default 
        {
            get
            {
                if (defaultConverter == null) 
                    defaultConverter = MakeDefault();
                return defaultConverter;
            }
        }

        private static ValueConverter<T> MakeDefault()
        {
            var t = typeof(T);

            if (t.IsValueType)
            { // we have to do this garbo to make it accept the thing that we know is a value type at instantiation time
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                { // this is a Nullable
                    return Activator.CreateInstance(typeof(NullableConverter<>).MakeGenericType(Nullable.GetUnderlyingType(t))) as ValueConverter<T>;
                }

                var valConv = Activator.CreateInstance(typeof(Converter.ValConv<>).MakeGenericType(t)) as Converter.IValConv<T>;
                return valConv.Get();
            }
            else if (t == typeof(string))
            {
                return new StringConverter() as ValueConverter<T>;
            }
            else
            {
                return Activator.CreateInstance(typeof(CustomObjectConverter<>).MakeGenericType(t)) as ValueConverter<T>;
            }
        }
    }

    /// <summary>
    /// A converter for a <see cref="Nullable{T}"/>.
    /// </summary>
    /// <typeparam name="T">the underlying type of the <see cref="Nullable{T}"/></typeparam>
    public class NullableConverter<T> : ValueConverter<T?> where T : struct
    {
        private readonly ValueConverter<T> baseConverter;
        /// <summary>
        /// Creates a converter with the default converter for the base type.
        /// Equivalent to 
        /// <code>
        /// new NullableConverter(Converter&lt;T&gt;.Default)
        /// </code>
        /// </summary>
        /// <seealso cref="NullableConverter{T}.NullableConverter(ValueConverter{T})"/>
        /// <seealso cref="Converter{T}.Default"/>
        public NullableConverter() : this(Converter<T>.Default) { }
        /// <summary>
        /// Creates a converter with the given underlying <see cref="ValueConverter{T}"/>.
        /// </summary>
        /// <param name="underlying">the undlerlying <see cref="ValueConverter{T}"/> to use</param>
        public NullableConverter(ValueConverter<T> underlying)
            => baseConverter = underlying;
        /// <summary>
        /// Converts a <see cref="Value"/> tree to a value.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> tree to convert</param>
        /// <param name="parent">the object which will own the created object</param>
        /// <returns>the object represented by <paramref name="value"/></returns>
        public override T? FromValue(Value value, object parent)
            => value == null ? null : new T?(baseConverter.FromValue(value, parent));
        /// <summary>
        /// Converts a nullable <typeparamref name="T"/> to a <see cref="Value"/> tree.
        /// </summary>
        /// <param name="obj">the value to serialize</param>
        /// <param name="parent">the object which owns <paramref name="obj"/></param>
        /// <returns>a <see cref="Value"/> tree representing <paramref name="obj"/>.</returns>
        public override Value ToValue(T? obj, object parent)
            => obj == null ? null : baseConverter.ToValue(obj.Value, parent);
    }

    /// <summary>
    /// A converter for a <see cref="Nullable{T}"/> that default-constructs a converter of type <typeparamref name="TConverter"/>
    /// to use as the underlying converter. Use this in the <see cref="UseConverterAttribute"/>.
    /// </summary>
    /// <typeparam name="T">the underlying type of the <see cref="Nullable{T}"/></typeparam>
    /// <typeparam name="TConverter">the type to use as an underlying converter</typeparam>
    /// <seealso cref="NullableConverter{T}"/>
    public sealed class NullableConverter<T, TConverter> : NullableConverter<T> 
        where T : struct 
        where TConverter : ValueConverter<T>, new()
    {
        /// <summary>
        /// Creates a converter with a new <typeparamref name="TConverter"/> as the underlying converter.
        /// </summary>
        /// <seealso cref="NullableConverter{T}.NullableConverter(ValueConverter{T})"/>
        public NullableConverter() : base(new TConverter()) { }
    }

    internal class StringConverter : ValueConverter<string>
    {
        public override string FromValue(Value value, object parent)
            => (value as Text)?.Value;

        public override Value ToValue(string obj, object parent)
            => Value.From(obj);
    }

    internal class CharConverter : ValueConverter<char>
    {
        public override char FromValue(Value value, object parent)
            => (value as Text).Value[0]; // can throw nullptr

        public override Value ToValue(char obj, object parent)
            => Value.From(char.ToString(obj));
    }

    internal class LongConverter : ValueConverter<long>
    {
        public override long FromValue(Value value, object parent)
            => Converter.IntValue(value) ?? throw new ArgumentException("Value not a numeric value", nameof(value));

        public override Value ToValue(long obj, object parent)
            => Value.From(obj);
    }

    internal class ULongConverter : ValueConverter<ulong>
    {
        public override ulong FromValue(Value value, object parent)
            => (ulong)(Converter.FloatValue(value) ?? throw new ArgumentException("Value not a numeric value", nameof(value)));

        public override Value ToValue(ulong obj, object parent)
            => Value.From(obj);
    }

    internal class IntPtrConverter : ValueConverter<IntPtr>
    {
        public override IntPtr FromValue(Value value, object parent)
            => (IntPtr)Converter<long>.Default.FromValue(value, parent);

        public override Value ToValue(IntPtr obj, object parent)
            => Value.From((long)obj);
    }

    internal class UIntPtrConverter : ValueConverter<UIntPtr>
    {
        public override UIntPtr FromValue(Value value, object parent)
            => (UIntPtr)Converter<ulong>.Default.FromValue(value, parent);

        public override Value ToValue(UIntPtr obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class IntConverter : ValueConverter<int>
    {
        public override int FromValue(Value value, object parent)
            => (int)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(int obj, object parent)
            => Value.From(obj);
    }

    internal class UIntConverter : ValueConverter<uint>
    {
        public override uint FromValue(Value value, object parent)
            => (uint)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(uint obj, object parent)
            => Value.From(obj);
    }

    internal class ShortConverter : ValueConverter<short>
    {
        public override short FromValue(Value value, object parent)
            => (short)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(short obj, object parent)
            => Value.From(obj);
    }

    internal class UShortConverter : ValueConverter<ushort>
    {
        public override ushort FromValue(Value value, object parent)
            => (ushort)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(ushort obj, object parent)
            => Value.From(obj);
    }

    internal class ByteConverter : ValueConverter<byte>
    {
        public override byte FromValue(Value value, object parent)
            => (byte)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(byte obj, object parent)
            => Value.From(obj);
    }

    internal class SByteConverter : ValueConverter<sbyte>
    {
        public override sbyte FromValue(Value value, object parent)
            => (sbyte)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(sbyte obj, object parent)
            => Value.From(obj);
    }

    internal class DecimalConverter : ValueConverter<decimal>
    {
        public override decimal FromValue(Value value, object parent)
            => Converter.FloatValue(value) ?? throw new ArgumentException("Value not a numeric value", nameof(value));
        public override Value ToValue(decimal obj, object parent)
            => Value.From(obj);
    }

    internal class FloatConverter : ValueConverter<float>
    {
        public override float FromValue(Value value, object parent)
            => (float)Converter<decimal>.Default.FromValue(value, parent);
        public override Value ToValue(float obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class DoubleConverter : ValueConverter<double>
    {
        public override double FromValue(Value value, object parent)
            => (double)Converter<decimal>.Default.FromValue(value, parent);
        public override Value ToValue(double obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class BooleanConverter : ValueConverter<bool>
    {
        public override bool FromValue(Value value, object parent)
            => (value as Boolean).Value;
        public override Value ToValue(bool obj, object parent)
            => Value.From(obj);
    }
}
