using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    /// <summary>
    /// A class to store a reference for passing to methods which cannot take ref parameters.
    /// </summary>
    /// <typeparam name="T">the type of the value</typeparam>
    public class Ref<T>
    {
        private T _value;
        /// <summary>
        /// The value of the reference
        /// </summary>
        public T Value
        {
            get
            {
                if (Error != null) throw Error;
                return _value;
            }
            set => _value = value;
        }

        private Exception _error = null;
        /// <summary>
        /// An exception that was generated while creating the value.
        /// </summary>
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
        /// Throws error if one was set.
        /// </summary>
        public void Verify()
        {
            if (Error != null) throw new Exception("Found error", Error);
        }
    }
    
    internal static class ExceptionUtilities
    {
        private static readonly FieldInfo STACK_TRACE_STRING_FI = typeof(Exception).GetField("_stackTraceString", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Type TRACE_FORMAT_TI = Type.GetType("System.Diagnostics.StackTrace").GetNestedType("TraceFormat", BindingFlags.NonPublic);
        private static readonly MethodInfo TRACE_TO_STRING_MI = typeof(StackTrace).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { TRACE_FORMAT_TI }, null);

        public static Exception SetStackTrace(this Exception target, StackTrace stack)
        {
            var getStackTraceString = TRACE_TO_STRING_MI.Invoke(stack, new object[] { Enum.GetValues(TRACE_FORMAT_TI).GetValue(0) });
            STACK_TRACE_STRING_FI.SetValue(target, getStackTraceString);
            return target;
        }
    }
}
