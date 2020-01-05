using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Net3_Proxy
{
    internal static class TypeUtils
    {
		public static void ValidateType(Type type, string paramName)
			=> ValidateType(type, paramName, false, false);

		// Token: 0x0600197D RID: 6525 RVA: 0x00053C07 File Offset: 0x00051E07
		public static void ValidateType(Type type, string paramName, bool allowByRef, bool allowPointer)
		{
			if (ValidateType(type, paramName, -1))
			{
				if (!allowByRef && type.IsByRef)
				{
					throw new ArgumentException("Type must not be ref", paramName);
				}
				if (!allowPointer && type.IsPointer)
				{
					throw new ArgumentException("Type must not be pointer", paramName);
				}
			}
		}

		// Token: 0x0600197E RID: 6526 RVA: 0x00053C37 File Offset: 0x00051E37
		public static bool ValidateType(Type type, string paramName, int index)
		{
			if (type == typeof(void))
				return false;
			if (type.ContainsGenericParameters)
				throw type.IsGenericTypeDefinition
					? new ArgumentException($"Type {type} is a generic type definition", GetParamName(paramName, index))
					: new ArgumentException($"Type {type} contains generic parameters", GetParamName(paramName, index));
			return true;
		}

		public static string GetParamName(string paramName, int index)
		{
			if (index >= 0)
			{
				return string.Format("{0}[{1}]", paramName, index);
			}
			return paramName;
		}

		public static bool AreEquivalent(Type t1, Type t2)
		{
			return t1 != null && t1 == t2;
		}

		public static bool AreReferenceAssignable(Type dest, Type src)
		{
			return TypeUtils.AreEquivalent(dest, src) || (!dest.IsValueType && !src.IsValueType && dest.IsAssignableFrom(src));
		}
	}
}
