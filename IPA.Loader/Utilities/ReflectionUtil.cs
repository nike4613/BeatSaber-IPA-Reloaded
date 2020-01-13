using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace IPA.Utilities
{
    /// <summary>
    /// A utility class providing reflection helper methods.
    /// </summary>
    public static partial class ReflectionUtil
    {
        internal static readonly FieldInfo DynamicMethodReturnType = 
            typeof(DynamicMethod).GetField("returnType", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        /// <summary>
        /// Sets a field on the target object, as gotten from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type to get the field from</typeparam>
        /// <typeparam name="U">the type of the field to set</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="fieldName">the field to set</param>
        /// <param name="value">the value to set it to</param>
        /// <exception cref="MissingFieldException">if <paramref name="fieldName"/> does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="FieldAccessor{T, U}.Set(ref T, string, U)"/>
        public static void SetField<T, U>(this T obj, string fieldName, U value)
            => FieldAccessor<T, U>.Set(ref obj, fieldName, value);

        /// <summary>
        /// Gets the value of a field.
        /// </summary>
        /// <typeparam name="T">the type to get the field from</typeparam>
        /// <typeparam name="U">the type of the field (result casted)</typeparam>
        /// <param name="obj">the object instance to pull from</param>
        /// <param name="fieldName">the name of the field to read</param>
        /// <returns>the value of the field</returns>
        /// <exception cref="MissingFieldException">if <paramref name="fieldName"/> does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="FieldAccessor{T, U}.Get(ref T, string)"/>
        public static U GetField<U, T>(this T obj, string fieldName)
            => FieldAccessor<T, U>.Get(ref obj, fieldName);

        /// <summary>
        /// Sets a property on the target object, as gotten from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type to get the property from</typeparam>
        /// <typeparam name="U">the type of the property to set</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="propertyName">the property to set</param>
        /// <param name="value">the value to set it to</param>
        /// <exception cref="MissingMemberException">if <paramref name="propertyName"/> does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="PropertyAccessor{T, U}.Set(ref T, string, U)"/>
        public static void SetProperty<T, U>(this T obj, string propertyName, U value)
            => PropertyAccessor<T, U>.Set(ref obj, propertyName, value);

        /// <summary>
        /// Gets a property on the target object, as gotten from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type to get the property from</typeparam>
        /// <typeparam name="U">the type of the property to get</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="propertyName">the property to get</param>
        /// <returns>the value of the property</returns>
        /// <exception cref="MissingMemberException">if <paramref name="propertyName"/> does not exist on <typeparamref name="T"/></exception>
        /// <seealso cref="PropertyAccessor{T, U}.Get(ref T, string)"/>
        public static U GetProperty<U, T>(this T obj, string propertyName)
            => PropertyAccessor<T, U>.Get(ref obj, propertyName);

        /// <summary>
        /// Invokes a method from <typeparamref name="T"/> on an object.
        /// </summary>
        /// <typeparam name="U">the type of the property to get</typeparam>
        /// <typeparam name="T">the type to search for the method on</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="methodName">the method's name</param>
        /// <param name="args">the method arguments</param>
        /// <returns>the return value</returns>
        /// <exception cref="MissingMethodException">if <paramref name="methodName"/> does not exist on <typeparamref name="T"/></exception>
        public static U InvokeMethod<U, T>(this T obj, string methodName, params object[] args)
        {
            var dynMethod = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (dynMethod == null) throw new MissingMethodException($"Method {methodName} does not exist", nameof(methodName));
            return (U)dynMethod?.Invoke(obj, args);
        }

        /// <summary>
        /// Copies a component <paramref name="original"/> to a component of <paramref name="overridingType"/> on the destination <see cref="GameObject"/>.
        /// </summary>
        /// <param name="original">the original component</param>
        /// <param name="overridingType">the new component's type</param>
        /// <param name="destination">the destination GameObject</param>
        /// <param name="originalTypeOverride">overrides the source component type (for example, to a superclass)</param>
        /// <returns>the copied component</returns>
        public static Component CopyComponent(this Component original, Type overridingType, GameObject destination, Type originalTypeOverride = null)
        {
            var copy = destination.AddComponent(overridingType);
            var originalType = originalTypeOverride ?? original.GetType();

            Type type = originalType;
            while (type != typeof(MonoBehaviour))
            {
                CopyForType(type, original, copy);
                type = type?.BaseType;
            }

            return copy;
        }

        /// <summary>
        /// A generic version of <see cref="CopyComponent(Component, Type, GameObject, Type)"/>. 
        /// </summary>
        /// <seealso cref="CopyComponent(Component, Type, GameObject, Type)"/>
        /// <typeparam name="T">the overriding type</typeparam>
        /// <param name="original">the original component</param>
        /// <param name="destination">the destination game object</param>
        /// <param name="originalTypeOverride">overrides the source component type (for example, to a superclass)</param>
        /// <returns>the copied component</returns>
        public static T CopyComponent<T>(this Component original, GameObject destination, Type originalTypeOverride = null)
            where T : Component
        {
            var copy = destination.AddComponent<T>();
            var originalType = originalTypeOverride ?? original.GetType();

            Type type = originalType;
            while (type != typeof(MonoBehaviour))
            {
                CopyForType(type, original, copy);
                type = type?.BaseType;
            }

            return copy;
        }

        private static void CopyForType(Type type, Component source, Component destination)
        {
            FieldInfo[] myObjectFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo fi in myObjectFields)
            {
                fi.SetValue(destination, fi.GetValue(source));
            }
        }
    }
}
