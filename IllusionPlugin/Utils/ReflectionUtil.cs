using System;
using System.Reflection;
using UnityEngine;

namespace IllusionPlugin.Utils
{
    /// <summary>
    /// A utility class providing reflection helper methods.
    /// </summary>
	public static class ReflectionUtil
	{
        /// <summary>
        /// Sets a (potentially) private field on the target object.
        /// </summary>
        /// <param name="obj">the object instance</param>
        /// <param name="fieldName">the field to set</param>
        /// <param name="value">the value to set it to</param>
		public static void SetPrivateField(this object obj, string fieldName, object value)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop.SetValue(obj, value);
		}
		
        /// <summary>
        /// Gets the value of a (potentially) private field.
        /// </summary>
        /// <typeparam name="T">the type of te field (result casted)</typeparam>
        /// <param name="obj">the object instance to pull from</param>
        /// <param name="fieldName">the name of the field to read</param>
        /// <returns>the value of the field</returns>
		public static T GetPrivateField<T>(this object obj, string fieldName)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			var value = prop.GetValue(obj);
			return (T) value;
		}
		
        /// <summary>
        /// Sets a (potentially) private propert on the target object.
        /// </summary>
        /// <param name="obj">the target object instance</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="value">the value to set it to</param>
		public static void SetPrivateProperty(this object obj, string propertyName, object value)
		{
			var prop = obj.GetType()
				.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop.SetValue(obj, value, null);
		}

        /// <summary>
        /// Invokes a (potentially) private method.
        /// </summary>
        /// <param name="obj">the object to call from</param>
        /// <param name="methodName">the method name</param>
        /// <param name="methodParams">the method parameters</param>
        /// <returns>the return value</returns>
		public static object InvokePrivateMethod(this object obj, string methodName, params object[] methodParams)
		{
			MethodInfo dynMethod = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
			return dynMethod.Invoke(obj, methodParams);
		}

        /// <summary>
        /// Invokes a (potentially) private method.
        /// </summary>
        /// <typeparam name="T">the return type</typeparam>
        /// <param name="obj">the object to call from</param>
        /// <param name="methodName">the method name to call</param>
        /// <param name="methodParams">the method's parameters</param>
        /// <returns>the return value</returns>
        public static T InvokePrivateMethod<T>(this object obj, string methodName, params object[] methodParams)
        {
            return (T)InvokePrivateMethod(obj, methodName, methodParams);
        }

        /// <summary>
        /// Copies a component of type originalType to a component of overridingType on the destination GameObject.
        /// </summary>
        /// <param name="original">the original component</param>
        /// <param name="originalType">the original component's type</param>
        /// <param name="overridingType">the new component's type</param>
        /// <param name="destination">the destination GameObject</param>
        /// <returns></returns>
        public static Component CopyComponent(Component original, Type originalType, Type overridingType, GameObject destination)
        {
            var copy = destination.AddComponent(overridingType);

            Type type = originalType;
            while (type != typeof(MonoBehaviour))
            {
                CopyForType(type, original, copy);
                type = type.BaseType;
            }

            return copy;
        }

        private static void CopyForType(Type type, Component source, Component destination)
        {
            FieldInfo[] myObjectFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField);

            foreach (FieldInfo fi in myObjectFields)
            {
                fi.SetValue(destination, fi.GetValue(source));
            }
        }
    }
}
