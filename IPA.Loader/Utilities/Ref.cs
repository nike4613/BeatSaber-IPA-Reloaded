using System;
using System.Diagnostics;
using System.Reflection;

namespace IPA.Utilities
{
    /// <summary>
    /// Utilities to create <see cref="Ref{T}"/> using type inference.
    /// </summary>
    public static class Ref
    {
        /// <summary>
        /// Creates a <see cref="Ref{T}"/>.
        /// </summary>
        /// <typeparam name="T">the type to reference.</typeparam>
        /// <param name="val">the default value.</param>
        /// <returns>the new <see cref="Ref{T}"/>.</returns>
        public static Ref<T> Create<T>(T val)
        {
            return new Ref<T>(val);
        }
    }

    /// <summary>
    /// A class to store a reference for passing to methods which cannot take ref parameters.
    /// </summary>
    /// <typeparam name="T">the type of the value</typeparam>
    public class Ref<T> : IComparable<T>, IComparable<Ref<T>>
    {
        private T _value;
        /// <summary>
        /// The value of the reference
        /// </summary>
        /// <value>the value wrapped by this <see cref="Ref{T}"/></value>
        public T Value
        {
            get
            {
                if (Error != null) throw Error;
                return _value;
            }
            set => _value = value;
        }

        private Exception _error;
        /// <summary>
        /// An exception that was generated while creating the value.
        /// </summary>
        /// <value>the error held in this <see cref="Ref{T}"/></value>
        public Exception Error
        {
            get
            {
                return _error;
            }
            set
            {
                value.SetStackTrace(new StackTrace(1));
                _error = value;
            }
        }
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="reference">the initial value of the reference</param>
        public Ref(T reference)
        {
            _value = reference;
        }

        /// <summary>
        /// Converts to referenced type, returning the stored reference.
        /// </summary>
        /// <param name="self">the object to be de-referenced</param>
        /// <returns>the value referenced by the object</returns>
        public static implicit operator T(Ref<T> self)
        {
            return self.Value;
        }

        /// <summary>
        /// Converts a value T to a reference to that object. Will overwrite the reference in the left hand expression if there is one.
        /// </summary>
        /// <param name="toConvert">the value to wrap in the Ref</param>
        /// <returns>the Ref wrapping the value</returns>
        public static implicit operator Ref<T>(T toConvert)
        {
            return new Ref<T>(toConvert);
        }

        /// <summary>
        /// Throws error if one was set.
        /// </summary>
        public void Verify()
        {
            if (Error != null) throw Error;
        }
        
        /// <summary>
        /// Compares the wrapped object to the other object.
        /// </summary>
        /// <param name="other">the object to compare to</param>
        /// <returns>the value of the comparison</returns>
        public int CompareTo(T other)
        {
            if (Value is IComparable<T> compare)
                return compare.CompareTo(other);
            return Equals(Value, other) ? 0 : -1;
        }

        /// <summary>
        /// Compares the wrapped object to the other wrapped object.
        /// </summary>
        /// <param name="other">the wrapped object to compare to</param>
        /// <returns>the value of the comparison</returns>
        public int CompareTo(Ref<T> other) => CompareTo(other.Value);
    }
    
    internal static class ExceptionUtilities
    {
        private static readonly FieldInfo StackTraceStringFi = typeof(Exception).GetField("_stackTraceString", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Type TraceFormatTi = Type.GetType("System.Diagnostics.StackTrace")?.GetNestedType("TraceFormat", BindingFlags.NonPublic);
        private static readonly MethodInfo TraceToStringMi = typeof(StackTrace).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { TraceFormatTi }, null);

        public static Exception SetStackTrace(this Exception target, StackTrace stack)
        {
            var getStackTraceString = TraceToStringMi.Invoke(stack, new[] { Enum.GetValues(TraceFormatTi).GetValue(0) });
            StackTraceStringFi.SetValue(target, getStackTraceString);
            return target;
        }
    }
}
