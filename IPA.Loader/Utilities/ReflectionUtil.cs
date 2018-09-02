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
			MethodInfo dynMethod = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
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
                type = type.BaseType;
            }

            return copy;
        }

        /// <summary>
        /// A generic version of CopyComponent. 
        /// <see cref="CopyComponent(Component, Type, GameObject, Type)"/>
        /// </summary>
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

        /// <summary>
        /// Calls an instance method on a type specified by functionClass and dependency.
        /// <seealso cref="CallNonStaticMethod(Type, string, Type[], object[])"/>
        /// </summary>
        /// <param name="functionClass">the type name</param>
        /// <param name="dependency">the assembly the type is in</param>
        /// <param name="function">the name of the method to call</param>
        /// <param name="methodSig">the type signature of the method</param>
        /// <param name="parameters">the method parameters</param>
        /// <returns>the result of the call</returns>
        public static object CallNonStaticMethod(string functionClass, string dependency, string function, Type[] methodSig, params object[] parameters)
        {
            return CallNonStaticMethod(Type.GetType(string.Format("{0},{1}", functionClass, dependency)), function, methodSig, parameters);
        }

        /// <summary>
        /// Calls an instance method on a new object.
        /// </summary>
        /// <param name="type">the object type</param>
        /// <param name="function">the name of the method to call</param>
        /// <param name="methodSig">the type signature</param>
        /// <param name="parameters">the parameters</param>
        /// <returns>the result of the call</returns>
        public static object CallNonStaticMethod(this Type type, /*string functionClass, string dependency,*/ string function, Type[] methodSig, params object[] parameters)
        {
            //Type FunctionClass = Type.GetType(string.Format("{0},{1}", functionClass, dependency));
            if (type != null)
            {
                object instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    Type instType = instance.GetType();
                    MethodInfo methodInfo = instType.GetMethod(function, methodSig);
                    if (methodInfo != null)
                    {
                        return methodInfo.Invoke(instance, parameters);
                    }
                    else
                    {
                        throw new Exception("Method not found");
                    }
                }
                else
                {
                    throw new Exception("Unable to instantiate object of type");
                }
            }
            else
            {
                throw new ArgumentNullException("type");
            }
        }

        /// <summary>
        /// Calls an instance method on a new object.
        /// <seealso cref="CallNonStaticMethod(Type, string, Type[], object[])"/>
        /// </summary>
        /// <typeparam name="T">the return type</typeparam>
        /// <param name="type">the object type</param>
        /// <param name="function">the name of the method to call</param>
        /// <param name="methodSig">the type signature</param>
        /// <param name="parameters">the parameters</param>
        /// <returns>the result of the call</returns>
        public static T CallNonStaticMethod<T>(this Type type, string function, Type[] methodSig, params object[] parameters)
        {
            return (T)CallNonStaticMethod(type, function, methodSig, parameters);
        }
    }
}
