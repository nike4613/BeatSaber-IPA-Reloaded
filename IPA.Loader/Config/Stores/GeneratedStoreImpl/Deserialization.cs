using IPA.Config.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Boolean = IPA.Config.Data.Boolean;

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private static Type GetExpectedValueTypeForType(Type valT)
        {
            if (typeof(Value).IsAssignableFrom(valT)) // this is a Value subtype
                return valT;
            if (valT == typeof(string)
             || valT == typeof(char)) return typeof(Text);
            if (valT == typeof(bool)) return typeof(Boolean);
            if (valT == typeof(byte)
             || valT == typeof(sbyte)
             || valT == typeof(short)
             || valT == typeof(ushort)
             || valT == typeof(int)
             || valT == typeof(uint)
             || valT == typeof(long)
             || valT == typeof(IntPtr)) return typeof(Integer);
            if (valT == typeof(float)
             || valT == typeof(double)
             || valT == typeof(decimal)
             || valT == typeof(ulong) // ulong gets put into this, because decimal can hold it
             || valT == typeof(UIntPtr)) return typeof(FloatingPoint);
            if (typeof(IEnumerable).IsAssignableFrom(valT)) return typeof(List);

            // TODO: fill this out the rest of the way

            return typeof(Map); // default for various objects
        }

        private static void EmitDeserializeGeneratedValue(ILGenerator il, SerializedMemberInfo member, Type srcType, GetLocal GetLocal)
        {
            var IGeneratedStore_Deserialize = typeof(IGeneratedStore).GetMethod(nameof(IGeneratedStore.Deserialize));

            var valuel = GetLocal(srcType, 0);
            var noCreate = il.DefineLabel();

            il.Emit(OpCodes.Stloc, valuel);
            EmitLoad(il, member, il => il.Emit(OpCodes.Ldarg_0));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
            il.Emit(OpCodes.Brtrue_S, noCreate);
            il.Emit(OpCodes.Pop);
            EmitCreateChildGenerated(il, member.Type);
            il.MarkLabel(noCreate);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, valuel);
            il.Emit(OpCodes.Callvirt, IGeneratedStore_Deserialize);
        }

        private static void EmitDeserializeNullable(ILGenerator il, SerializedMemberInfo member, Type expected, GetLocal GetLocal)
        {
            EmitDeserializeValue(il, member, member.NullableWrappedType, expected, GetLocal);
            il.Emit(OpCodes.Newobj, member.Nullable_Construct);
        }

        // top of stack is the Value to deserialize; the type will be as returned from GetExpectedValueTypeForType
        // after, top of stack will be thing to write to field
        private static void EmitDeserializeValue(ILGenerator il, SerializedMemberInfo member, Type targetType, Type expected, GetLocal GetLocal)
        {
            if (typeof(Value).IsAssignableFrom(targetType)) return; // do nothing

            if (expected == typeof(Text))
            {
                var getter = expected.GetProperty(nameof(Text.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                if (targetType == typeof(char))
                {
                    var strIndex = typeof(string).GetProperty("Chars").GetGetMethod(); // string's indexer is specially named Chars
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, strIndex);
                }
            }
            else if (expected == typeof(Boolean))
            {
                var getter = expected.GetProperty(nameof(Boolean.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
            }
            else if (expected == typeof(Integer))
            {
                var getter = expected.GetProperty(nameof(Integer.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                EmitNumberConvertTo(il, targetType, getter.ReturnType);
            }
            else if (expected == typeof(FloatingPoint))
            {
                var getter = expected.GetProperty(nameof(FloatingPoint.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                EmitNumberConvertTo(il, targetType, getter.ReturnType);
            } // TODO: implement stuff for lists and maps of various types (probably call out somewhere else to figure out what to do)
            else if (expected == typeof(Map))
            {
                EmitDeserializeGeneratedValue(il, member, expected, GetLocal);
            }
            else
            {
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
        }

        private static void EmitDeserializeConverter(ILGenerator il, SerializedMemberInfo member, Label nextLabel, GetLocal GetLocal)
        {
            var stlocal = GetLocal(typeof(Value));
            var valLocal = GetLocal(member.Type);

            il.Emit(OpCodes.Stloc, stlocal);
            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldsfld, member.ConverterField);
            il.Emit(OpCodes.Ldloc, stlocal);
            il.Emit(OpCodes.Ldarg_0);

            if (member.IsGenericConverter)
            {
                var fromValueBase = member.ConverterBase.GetMethod(nameof(ValueConverter<int>.FromValue),
                    new[] { typeof(Value), typeof(object) });
                var fromValue = member.Converter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetBaseDefinition() == fromValueBase) ?? fromValueBase;
                il.Emit(OpCodes.Call, fromValue);
            }
            else
            {
                var fromValueBase = typeof(IValueConverter).GetMethod(nameof(IValueConverter.FromValue),
                    new[] { typeof(Value), typeof(object) });
                var fromValue = member.Converter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetBaseDefinition() == fromValueBase) ?? fromValueBase;
                il.Emit(OpCodes.Call, fromValue);
                if (member.Type.IsValueType)
                    il.Emit(OpCodes.Unbox);
            }

            il.Emit(OpCodes.Stloc, valLocal);
            il.BeginCatchBlock(typeof(Exception));
            EmitWarnException(il, "Error occurred while deserializing");
            il.Emit(OpCodes.Leave, nextLabel);
            il.EndExceptionBlock();
            il.Emit(OpCodes.Ldloc, valLocal);
        }

        // emit takes the value being deserialized, logs on error, leaves nothing on stack
        private static void EmitDeserializeMember(ILGenerator il, SerializedMemberInfo member, Label nextLabel, Action<ILGenerator> getValue, GetLocal GetLocal)
        {
            var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

            var implLabel = il.DefineLabel();
            var passedTypeCheck = il.DefineLabel();
            var expectType = GetExpectedValueTypeForType(member.IsNullable ? member.NullableWrappedType : member.Type);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brtrue_S, implLabel); // null check

            if (!member.AllowNull)
            {
                il.Emit(OpCodes.Pop);
                EmitLogError(il, $"Member {member.Name} ({member.Type}) not nullable", tailcall: false,
                    expected: il => EmitTypeof(il, expectType));
                il.Emit(OpCodes.Br, nextLabel);
            }
            else if (member.IsNullable)
            {
                il.Emit(OpCodes.Pop);
                var valTLocal = GetLocal(member.Type, 0);
                il.Emit(OpCodes.Ldloca, valTLocal);
                il.Emit(OpCodes.Initobj, member.Type);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, valTLocal));
                il.Emit(OpCodes.Br, nextLabel);
            }
            else
            {
                il.Emit(OpCodes.Pop);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldnull));
                il.Emit(OpCodes.Br, nextLabel);
            }


            if (!member.HasConverter)
            {
                il.MarkLabel(implLabel);
                il.Emit(OpCodes.Isinst, expectType); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brtrue, passedTypeCheck); // null check
            }

            var errorHandle = il.DefineLabel();

            // special cases to handle coersion between Float and Int
            if (member.HasConverter)
                il.MarkLabel(implLabel);
            else if (expectType == typeof(FloatingPoint))
            {
                var specialTypeCheck = il.DefineLabel();
                il.Emit(OpCodes.Pop);
                getValue(il);
                il.Emit(OpCodes.Isinst, typeof(Integer)); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brfalse, errorHandle); // null check

                var Integer_CoerceToFloat = typeof(Integer).GetMethod(nameof(Integer.AsFloat));
                il.Emit(OpCodes.Call, Integer_CoerceToFloat);

                il.Emit(OpCodes.Br, passedTypeCheck);
            }
            else if (expectType == typeof(Integer))
            {
                var specialTypeCheck = il.DefineLabel();
                il.Emit(OpCodes.Pop);
                getValue(il);
                il.Emit(OpCodes.Isinst, typeof(FloatingPoint)); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brfalse, errorHandle); // null check

                var Float_CoerceToInt = typeof(FloatingPoint).GetMethod(nameof(FloatingPoint.AsInteger));
                il.Emit(OpCodes.Call, Float_CoerceToInt);

                il.Emit(OpCodes.Br, passedTypeCheck);
            }

            if (!member.HasConverter)
            {
                il.MarkLabel(errorHandle);
                il.Emit(OpCodes.Pop);
                EmitLogError(il, $"Unexpected type deserializing {member.Name}", tailcall: false,
                    expected: il => EmitTypeof(il, expectType), found: il =>
                    {
                        getValue(il);
                        il.Emit(OpCodes.Callvirt, Object_GetType);
                    });
                il.Emit(OpCodes.Br, nextLabel);
            }

            il.MarkLabel(passedTypeCheck);

            var local = GetLocal(member.Type, 0);
            if (member.HasConverter) EmitDeserializeConverter(il, member, nextLabel, GetLocal);
            else if (member.IsNullable) EmitDeserializeNullable(il, member, expectType, GetLocal);
            else EmitDeserializeValue(il, member, member.Type, expectType, GetLocal);
            il.Emit(OpCodes.Stloc, local);
            EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, local));
        }
    }
}
