using IPA.Utilities.Async;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Utilities
{
    /// <summary>
    /// A type containing utilities for accessing non-public fields of objects.
    /// </summary>
    /// <typeparam name="T">the type that the fields are on</typeparam>
    /// <typeparam name="U">the type of the field to access</typeparam>
    /// <seealso cref="PropertyAccessor{T, U}"/>
    public static class FieldAccessor<T, U>
    {
        /// <summary>
        /// A delegate for a field accessor taking a <typeparamref name="T"/> ref and returning a <typeparamref name="U"/> ref.
        /// </summary>
        /// <param name="obj">the object to access the field of</param>
        /// <returns>a reference to the field's value</returns>
        public delegate ref U Accessor(ref T obj);

        private static Accessor MakeAccessor(string fieldName)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field == null)
                throw new MissingFieldException(typeof(T).Name, fieldName);
            if (field.FieldType != typeof(U))
                throw new ArgumentException($"Field '{fieldName}' not of type {typeof(U)}");

            var dynMethodName = $"<>_accessor__{fieldName}";
            // unfortunately DynamicMethod doesn't like having a ByRef return type, so reflection it
            var dyn = new DynamicMethod(dynMethodName, typeof(U), new[] { typeof(T).MakeByRefType() }, typeof(FieldAccessor<T, U>), true);
            ReflectionUtil.DynamicMethodReturnType.SetValue(dyn, typeof(U).MakeByRefType());

            var il = dyn.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldflda, field);
            il.Emit(OpCodes.Ret);

            return (Accessor)dyn.CreateDelegate(typeof(Accessor));
        }

        // field name -> accessor
        private static readonly SingleCreationValueCache<string, Accessor> accessors = new SingleCreationValueCache<string, Accessor>();

        /// <summary>
        /// Gets an <see cref="Accessor"/> for the field named <paramref name="name"/> on <typeparamref name="T"/>.
        /// </summary>
        /// <param name="name">the field name</param>
        /// <returns>an accessor for the field</returns>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        public static Accessor GetAccessor(string name)
            => accessors.GetOrAdd(name, MakeAccessor);

        /// <summary>
        /// Accesses a field for an object by name.
        /// </summary>
        /// <param name="obj">the object to access the field of</param>
        /// <param name="name">the name of the field to access</param>
        /// <returns>a reference to the object at the field</returns>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="GetAccessor(string)"/>
        public static ref U Access(ref T obj, string name) => ref GetAccessor(name)(ref obj);
        /// <summary>
        /// Gets the value of a field of an object by name.
        /// </summary>
        /// <remarks>
        /// The only good reason to use this over <see cref="Get(T, string)"/> is when you are working with a value type,
        /// as it prevents a copy.
        /// </remarks>
        /// <param name="obj">the object to access the field of</param>
        /// <param name="name">the name of the field to access</param>
        /// <returns>the value of the field</returns>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="Get(T, string)"/>
        /// <seealso cref="Access(ref T, string)"/>
        /// <seealso cref="GetAccessor(string)"/>
        public static U Get(ref T obj, string name) => Access(ref obj, name);
        /// <summary>
        /// Gets the value of a field of an object by name.
        /// </summary>
        /// <param name="obj">the object to access the field of</param>
        /// <param name="name">the name of the field to access</param>
        /// <returns>the value of the field</returns>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="Get(ref T, string)"/>
        /// <seealso cref="Access(ref T, string)"/>
        /// <seealso cref="GetAccessor(string)"/>
        public static U Get(T obj, string name) => Get(ref obj, name);
        /// <summary>
        /// Sets the value of a field for an object by name.
        /// </summary>
        /// <remarks>
        /// This overload must be used for value types.
        /// </remarks>
        /// <param name="obj">the object to set the field of</param>
        /// <param name="name">the name of the field</param>
        /// <param name="value">the value to set it to</param>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="Set(T, string, U)"/>
        /// <seealso cref="Access(ref T, string)"/>
        /// <seealso cref="GetAccessor(string)"/>
        public static void Set(ref T obj, string name, U value) => Access(ref obj, name) = value;
        /// <summary>
        /// Sets the value of a field for an object by name.
        /// </summary>
        /// <remarks>
        /// This overload cannot be safely used for value types. Use <see cref="Set(ref T, string, U)"/> instead.
        /// </remarks>
        /// <param name="obj">the object to set the field of</param>
        /// <param name="name">the name of the field</param>
        /// <param name="value">the value to set it to</param>
        /// <exception cref="MissingFieldException">if the field does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="Set(ref T, string, U)"/>
        /// <seealso cref="Access(ref T, string)"/>
        /// <seealso cref="GetAccessor(string)"/>
        public static void Set(T obj, string name, U value) => Set(ref obj, name, value);
    }

    /// <summary>
    /// A type containing utilities for accessing non-public properties of an object.
    /// </summary>
    /// <typeparam name="T">the type that the properties are on</typeparam>
    /// <typeparam name="U">the type of the property to access</typeparam>
    public static class PropertyAccessor<T, U>
    {
        /// <summary>
        /// A getter for a property.
        /// </summary>
        /// <param name="obj">the object it is a member of</param>
        /// <returns>the value of the property</returns>
        public delegate U Getter(ref T obj);
        /// <summary>
        /// A setter for a property.
        /// </summary>
        /// <param name="obj">the object it is a member of</param>
        /// <param name="val">the new property value</param>
        public delegate void Setter(ref T obj, U val);

        private static (Getter, Setter) MakeAccessors(string propName)
        {
            var prop = typeof(T).GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop == null)
                throw new MissingMemberException(typeof(T).Name, propName);
            if (prop.PropertyType != typeof(U))
                throw new ArgumentException($"Property '{propName}' on {typeof(T)} is not of type {typeof(U)}");

            var getM = prop.GetGetMethod(true);
            var setM = prop.GetSetMethod(true);
            Getter getter = null;
            Setter setter = null;

            if (typeof(T).IsValueType)
            {
                if (getM != null)
                    getter = (Getter)Delegate.CreateDelegate(typeof(Getter), getM);
                if (setM != null)
                    setter = (Setter)Delegate.CreateDelegate(typeof(Setter), setM);
            }
            else
            {
                if (getM != null)
                {
                    var dyn = new DynamicMethod($"<>_get__{propName}", typeof(U), new[] { typeof(T).MakeByRefType() }, typeof(PropertyAccessor<T, U>), true);
                    var il = dyn.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldind_Ref);
                    il.Emit(OpCodes.Tailcall);
                    il.Emit(getM.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getM);
                    il.Emit(OpCodes.Ret);
                    getter = (Getter)dyn.CreateDelegate(typeof(Getter));
                }
                if (setM != null)
                {
                    var dyn = new DynamicMethod($"<>_set__{propName}", typeof(void), new[] { typeof(T).MakeByRefType(), typeof(U) }, typeof(PropertyAccessor<T, U>), true);
                    var il = dyn.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldind_Ref);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Tailcall);
                    il.Emit(setM.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, setM);
                    il.Emit(OpCodes.Ret);
                    setter = (Setter)dyn.CreateDelegate(typeof(Setter));
                }
            }

            return (getter, setter);
        }

        private static readonly SingleCreationValueCache<string, (Getter get, Setter set)> props 
            = new SingleCreationValueCache<string, (Getter get, Setter set)>();

        private static (Getter get, Setter set) GetAccessors(string propName)
            => props.GetOrAdd(propName, MakeAccessors);

        /// <summary>
        /// Gets a <see cref="Getter"/> for the property identified by <paramref name="name"/>.
        /// </summary>
        /// <param name="name">the name of the property</param>
        /// <returns>a <see cref="Getter"/> that can access that property</returns>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        public static Getter GetGetter(string name) => GetAccessors(name).get;
        /// <summary>
        /// Gets a <see cref="Setter"/> for the property identified by <paramref name="name"/>.
        /// </summary>
        /// <param name="name">the name of the property</param>
        /// <returns>a <see cref="Setter"/> that can access that property</returns>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        public static Setter GetSetter(string name) => GetAccessors(name).set;

        /// <summary>
        /// Gets the value of the property identified by <paramref name="name"/> on <paramref name="obj"/>.
        /// </summary>
        /// <remarks>
        /// The only reason to use this over <see cref="Get(T, string)"/> is if you are using a value type because 
        /// it avoids a copy.
        /// </remarks>
        /// <param name="obj">the instance to access</param>
        /// <param name="name">the name of the property</param>
        /// <returns>the value of the property</returns>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        /// <seealso cref="Get(T, string)"/>
        /// <seealso cref="GetGetter(string)"/>
        public static U Get(ref T obj, string name) => GetGetter(name)(ref obj);
        /// <summary>
        /// Gets the value of the property identified by <paramref name="name"/> on <paramref name="obj"/>.
        /// </summary>
        /// <param name="obj">the instance to access</param>
        /// <param name="name">the name of the property</param>
        /// <returns>the value of the property</returns>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        /// <seealso cref="Get(ref T, string)"/>
        /// <seealso cref="GetGetter(string)"/>
        public static U Get(T obj, string name) => GetGetter(name)(ref obj);
        /// <summary>
        /// Sets the value of the property identified by <paramref name="name"/> on <paramref name="obj"/>.
        /// </summary>
        /// <remarks>
        /// This overload must be used for value types.
        /// </remarks>
        /// <param name="obj">the instance to access</param>
        /// <param name="name">the name of the property</param>
        /// <param name="val">the new value of the property</param>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        /// <seealso cref="Set(T, string, U)"/>
        /// <seealso cref="GetSetter(string)"/>
        public static void Set(ref T obj, string name, U val) => GetSetter(name)(ref obj, val);
        /// <summary>
        /// Sets the value of the property identified by <paramref name="name"/> on <paramref name="obj"/>.
        /// </summary>
        /// <remarks>
        /// This overload cannot be safely used for value types. Use <see cref="Set(ref T, string, U)"/> instead.
        /// </remarks>
        /// <param name="obj">the instance to access</param>
        /// <param name="name">the name of the property</param>
        /// <param name="val">the new value of the property</param>
        /// <exception cref="MissingMemberException">if the property does not exist</exception>
        /// <seealso cref="Set(ref T, string, U)"/>
        /// <seealso cref="GetSetter(string)"/>
        public static void Set(T obj, string name, U val) => GetSetter(name)(ref obj, val);
    }

    internal class AccessorDelegateInfo<TDelegate> where TDelegate : Delegate
    {
        public static readonly Type Type = typeof(TDelegate);
        public static readonly MethodInfo Invoke = Type.GetMethod("Invoke");
        public static readonly ParameterInfo[] Parameters = Invoke.GetParameters();
    }

    /// <summary>
    /// A type containing utilities for calling non-public methods on an object.
    /// </summary>
    /// <typeparam name="T">the type to find the methods on</typeparam>
    /// <typeparam name="TDelegate">the delegate type to create, and to use as a signature to search for</typeparam>
    public static class MethodAccessor<T, TDelegate> where TDelegate : Delegate
    {
        static MethodAccessor()
        {
            // ensure that first argument of delegate type is valid
            var firstArg = AccessorDelegateInfo<TDelegate>.Parameters.First();
            var firstType = firstArg.ParameterType;

            if (typeof(T).IsValueType)
            {
                if (!firstType.IsByRef)
                    throw new InvalidOperationException("First parameter of a method accessor to a value type is not byref");
                else
                    firstType = firstType.GetElementType(); // get the non-byref type to check compatability
            }

            if (!typeof(T).IsAssignableFrom(firstType))
                throw new InvalidOperationException("First parameter of a method accessor is not assignable to the method owning type");
        }

        private static TDelegate MakeDelegate(string name)
        {
            var delParams = AccessorDelegateInfo<TDelegate>.Parameters;
            var method = typeof(T).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, delParams.Skip(1).Select(p => p.ParameterType).ToArray(), Array.Empty<ParameterModifier>());

            if (method == null)
                throw new MissingMethodException(typeof(T).FullName, name);

            var retType = AccessorDelegateInfo<TDelegate>.Invoke.ReturnType;
            if (!retType.IsAssignableFrom(method.ReturnType))
                throw new ArgumentException($"The method found returns a type incompatable with the return type of {typeof(TDelegate)}");

            return (TDelegate)Delegate.CreateDelegate(AccessorDelegateInfo<TDelegate>.Type, method, true);
        }

        private static readonly SingleCreationValueCache<string, TDelegate> methods = new SingleCreationValueCache<string, TDelegate>();

        /// <summary>
        /// Gets a delegate to the named method with the signature specified by <typeparamref name="TDelegate"/>.
        /// </summary>
        /// <param name="name">the name of the method to get</param>
        /// <returns>a delegate that can call the specified method</returns>
        /// <exception cref="MissingMethodException">if <paramref name="name"/> does not represent the name of a method with the given signature</exception>
        /// <exception cref="ArgumentException">if the method found returns a type incompatable with the return type of <typeparamref name="TDelegate"/></exception>
        public static TDelegate GetDelegate(string name)
            => methods.GetOrAdd(name, MakeDelegate);
    }

}
