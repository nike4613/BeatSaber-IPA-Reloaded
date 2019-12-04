using System;
using System.Reflection;
using UnityEngine;

namespace IPA.Utilities
{
    /// <summary>
    /// A utility class providing reflection helper methods.
    /// </summary>
	public static class ReflectionUtil
	{
        /// <summary>
        /// Sets a field on the target object.
        /// </summary>
        /// <param name="obj">the object instance</param>
        /// <param name="fieldName">the field to set</param>
        /// <param name="value">the value to set it to</param>
		public static void SetField(this object obj, string fieldName, object value)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop?.SetValue(obj, value);
		}

        /// <summary>
        /// Sets a field on the target object, as gotten from <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">the type to get the field from</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="fieldName">the field to set</param>
        /// <param name="value">the value to set it to</param>
        public static void SetField<T>(this T obj, string fieldName, object value) where T : class
        {
            var prop = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(obj, value);
        }
		
        /// <summary>
        /// Gets the value of a field.
        /// </summary>
        /// <typeparam name="T">the type of the field (result casted)</typeparam>
        /// <param name="obj">the object instance to pull from</param>
        /// <param name="fieldName">the name of the field to read</param>
        /// <returns>the value of the field</returns>
		public static T GetField<T>(this object obj, string fieldName)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			var value = prop?.GetValue(obj);
			return (T) value;
		}
		
        /// <summary>
        /// Sets a property on the target object.
        /// </summary>
        /// <param name="obj">the target object instance</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="value">the value to set it to</param>
		public static void SetProperty(this object obj, string propertyName, object value)
		{
			var prop = obj.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop?.SetValue(obj, value, null);
		}

        /// <summary>
        /// Sets a property on the target object, as gotten from <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">the type to get the property from</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="propertyName">the property to set</param>
        /// <param name="value">the value to set it to</param>
        public static void SetProperty<T>(this T obj, string propertyName, object value) where T : class
        {
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(obj, value, null);
        }

        /// <summary>
        /// Invokes a method on an object.
        /// </summary>
        /// <param name="obj">the object to call from</param>
        /// <param name="methodName">the method name</param>
        /// <param name="methodArgs">the method arguments</param>
        /// <returns>the return value</returns>
		public static object InvokeMethod(this object obj, string methodName, params object[] methodArgs)
		{
			MethodInfo dynMethod = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			return dynMethod?.Invoke(obj, methodArgs);
		}

        /// <summary>
        /// Invokes a method from <typeparamref name="T"/> on an object.
        /// </summary>
        /// <typeparam name="T">the type to search for the method on</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="methodName">the method's name</param>
        /// <param name="args">the method arguments</param>
        /// <returns>the return value</returns>
        public static object InvokeMethod<T>(this T obj, string methodName, params object[] args) where T : class
        {
            var dynMethod = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return dynMethod?.Invoke(obj, args);
        }

        /// <summary>
        /// Invokes a method.
        /// </summary>
        /// <typeparam name="T">the return type</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="methodName">the method name to call</param>
        /// <param name="methodArgs">the method's arguments</param>
        /// <returns>the return value</returns>
        public static T InvokeMethod<T>(this object obj, string methodName, params object[] methodArgs)
        {
            return (T)InvokeMethod(obj, methodName, methodArgs);
        }

        /// <summary>
        /// Invokes a method from <typeparamref name="U"/> on an object.
        /// </summary>
        /// <typeparam name="T">the return type</typeparam>
        /// <typeparam name="U">the type to search for the method on</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="methodName">the method name to call</param>
        /// <param name="methodArgs">the method's arguments</param>
        /// <returns>the return value</returns>
        public static T InvokeMethod<T, U>(this U obj, string methodName, params object[] methodArgs) where U : class
        {
            return (T)obj.InvokeMethod(methodName, methodArgs);
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
