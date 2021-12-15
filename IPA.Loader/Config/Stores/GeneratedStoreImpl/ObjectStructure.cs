#nullable enable
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using IPA.Logging;
using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private class SerializedMemberInfo
        {
            public string Name = null!;
            public MemberInfo Member = null!;
            public Type Type = null!;
            public bool AllowNull;
            public bool IsVirtual;
            public bool IsField;
            [MemberNotNullWhen(true, nameof(NullableWrappedType))]
            public bool IsNullable { get; set; } // signifies whether this is a Nullable<T>

            [MemberNotNullWhen(true, nameof(Converter), nameof(ConverterBase))]
            public bool HasConverter { get; set; }
            public bool IsGenericConverter { get; set; } // used so we can call directly to the generic version if it is
            public Type? Converter;
            public Type? ConverterBase;
            public Type? ConverterTarget;
            public FieldInfo? ConverterField;

            // invalid for objects with IsNullable false
            public Type? NullableWrappedType => Nullable.GetUnderlyingType(Type);
            // invalid for objects with IsNullable false
            public PropertyInfo Nullable_HasValue => Type.GetProperty(nameof(Nullable<int>.HasValue));
            // invalid for objects with IsNullable false
            public PropertyInfo Nullable_Value => Type.GetProperty(nameof(Nullable<int>.Value));
            // invalid for objects with IsNullable false
            public ConstructorInfo Nullable_Construct => Type.GetConstructor(new[] { NullableWrappedType });

            public Type ConversionType => IsNullable ? NullableWrappedType : Type;
        }
        private static bool IsMethodInvalid(MethodInfo m, Type ret) => !m.IsVirtual || m.ReturnType != ret;
        private static bool ProcessAttributesFor(Type type, ref SerializedMemberInfo member)
        {
            var attrs = member.Member.GetCustomAttributes(true);
            var ignores = attrs.Select(o => o as IgnoreAttribute).NonNull();
            if (ignores.Any() || typeof(Delegate).IsAssignableFrom(member.Type))
            { // we ignore delegates completely because there is no a good way to serialize them
                return false;
            }

            var nonNullables = attrs.Select(o => o as NonNullableAttribute).NonNull();

            member.Name = member.Member.Name;
            member.IsNullable = member.Type.IsGenericType
                      && member.Type.GetGenericTypeDefinition() == typeof(Nullable<>);
            member.AllowNull = !nonNullables.Any() && (!member.Type.IsValueType || member.IsNullable);

            var nameAttr = attrs.Select(o => o as SerializedNameAttribute).NonNull().FirstOrDefault();
            if (nameAttr != null)
                member.Name = nameAttr.Name;

            member.HasConverter = false;
            var converterAttr = attrs.Select(o => o as UseConverterAttribute).NonNull().FirstOrDefault();
            if (converterAttr != null)
            {
                if (converterAttr.UseDefaultConverterForType)
                    converterAttr = new UseConverterAttribute(Converter.GetDefaultConverterType(member.Type));
                if (converterAttr.UseDefaultConverterForType)
                    throw new InvalidOperationException("How did we get here?"); 

                member.Converter = converterAttr.ConverterType;
                member.IsGenericConverter = converterAttr.IsGenericConverter;

                if (member.Converter.GetConstructor(Type.EmptyTypes) == null)
                {
                    Logger.Config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not default-constructible");
                    goto endConverterAttr; // is there a better control flow structure to do this?
                }

                if (member.Converter.ContainsGenericParameters)
                {
                    Logger.Config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that has unfilled type parameters");
                    goto endConverterAttr;
                }

                if (member.Converter.IsInterface || member.Converter.IsAbstract)
                {
                    Logger.Config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not constructible");
                    goto endConverterAttr;
                }

                var targetType = converterAttr.ConverterTargetType;
                if (!member.IsGenericConverter)
                {
                    try
                    {
                        var conv = (IValueConverter)Activator.CreateInstance(converterAttr.ConverterType);
                        targetType = conv.Type;
                    }
                    catch
                    {
                        Logger.Config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter who's target type could not be determined");
                        goto endConverterAttr;
                    }
                }
                if (targetType != member.Type)
                {
                    Logger.Config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not of the member's type");
                    goto endConverterAttr;
                }

                member.ConverterTarget = targetType;
                if (member.IsGenericConverter)
                    member.ConverterBase = typeof(ValueConverter<>).MakeGenericType(targetType);
                else
                    member.ConverterBase = typeof(IValueConverter);

                member.HasConverter = true;
            }
        endConverterAttr:

            return true;
        }

        private static readonly SingleCreationValueCache<Type, SerializedMemberInfo[]> objectStructureCache = new();

        private static IEnumerable<SerializedMemberInfo> ReadObjectMembers(Type type)
            => objectStructureCache.GetOrAdd(type, t => ReadObjectMembersInternal(type).ToArray());

        private static IEnumerable<SerializedMemberInfo> ReadObjectMembersInternal(Type type)
        {
            var structure = new List<SerializedMemberInfo>();

            // only looks at public/protected properties
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue; // we skip anything with index parameters

                if (prop.GetSetMethod(true)?.IsPrivate ?? true)
                { // we enter this block if the setter is inacessible or doesn't exist
                    continue; // ignore props without setter
                }
                if (prop.GetGetMethod(true)?.IsPrivate ?? true)
                { // we enter this block if the getter is inacessible or doesn't exist
                    continue; // ignore props without getter
                }

                var smi = new SerializedMemberInfo
                {
                    Member = prop,
                    IsVirtual = (prop.GetGetMethod(true)?.IsVirtual ?? false) ||
                                (prop.GetSetMethod(true)?.IsVirtual ?? false),
                    IsField = false,
                    Type = prop.PropertyType
                };

                if (!ProcessAttributesFor(type, ref smi)) continue;

                structure.Add(smi);
            }

            // only look at public/protected fields
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsPrivate)
                    continue;

                var smi = new SerializedMemberInfo
                {
                    Member = field,
                    IsVirtual = false,
                    IsField = true,
                    Type = field.FieldType
                };

                if (!ProcessAttributesFor(type, ref smi)) continue;

                structure.Add(smi);
            }

            CreateAndInitializeConvertersFor(type, structure);
            return structure;
        }
    }
}
