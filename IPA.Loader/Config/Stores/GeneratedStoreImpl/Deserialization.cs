using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Boolean = IPA.Config.Data.Boolean;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private static void EmitDeserializeGeneratedValue(ILGenerator il, SerializedMemberInfo member, Type srcType, LocalAllocator GetLocal,
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            var IGeneratedStore_Deserialize = typeof(IGeneratedStore).GetMethod(nameof(IGeneratedStore.Deserialize));

            using var valuel = GetLocal.Allocate(srcType);
            var noCreate = il.DefineLabel();

            il.Emit(OpCodes.Stloc, valuel);
            EmitLoad(il, member, thisarg);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
            il.Emit(OpCodes.Brtrue_S, noCreate);
            il.Emit(OpCodes.Pop);
            EmitCreateChildGenerated(il, member.Type, parentobj);
            il.MarkLabel(noCreate);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, valuel);
            il.Emit(OpCodes.Callvirt, IGeneratedStore_Deserialize);
        }

        private static void EmitDeserializeNullable(ILGenerator il, SerializedMemberInfo member, Type expected, LocalAllocator GetLocal, 
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            thisarg ??= il => il.Emit(OpCodes.Ldarg_0);
            parentobj ??= thisarg;
            EmitDeserializeValue(il, member, member.NullableWrappedType, expected, GetLocal, thisarg, parentobj);
            il.Emit(OpCodes.Newobj, member.Nullable_Construct);
        }

        // top of stack is the Value to deserialize; the type will be as returned from GetExpectedValueTypeForType
        // after, top of stack will be thing to write to field
        private static void EmitDeserializeValue(ILGenerator il, SerializedMemberInfo member, Type targetType, Type expected, LocalAllocator GetLocal, 
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
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
                if (!targetType.IsValueType)
                {
                    EmitDeserializeGeneratedValue(il, member, expected, GetLocal, thisarg, parentobj);
                }
                else
                {

                    using var mapLocal = GetLocal.Allocate(typeof(Map));
                    using var resultLocal = GetLocal.Allocate(targetType);
                    using var valueLocal = GetLocal.Allocate(typeof(Value));

                    var structure = ReadObjectMembers(targetType);
                    if (!structure.Any())
                    {
                        Logger.config.Warn($"Custom value type {targetType.FullName} (when compiling serialization of" +
                            $" {member.Name} on {member.Member.DeclaringType.FullName}) has no accessible members");
                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ldloca, resultLocal);
                        il.Emit(OpCodes.Initobj, targetType);
                    }
                    else
                    {
                        il.Emit(OpCodes.Stloc, mapLocal);

                        EmitLoad(il, member, thisarg);
                        il.Emit(OpCodes.Stloc, resultLocal);

                        EmitDeserializeStructure(il, structure, mapLocal, valueLocal, GetLocal, il => il.Emit(OpCodes.Ldloca, resultLocal), parentobj);
                    }

                    il.Emit(OpCodes.Ldloc, resultLocal);
                }
            }
            else
            {
                Logger.config.Warn($"Implicit conversions to {expected} are not currently implemented");
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
        }

        private static void EmitDeserializeStructure(ILGenerator il, IEnumerable<SerializedMemberInfo> structure, 
            LocalBuilder mapLocal, LocalBuilder valueLocal,
            LocalAllocator GetLocal, Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            var Map_TryGetValue = typeof(Map).GetMethod(nameof(Map.TryGetValue));

            // head of stack is Map instance
            foreach (var mem in structure)
            {
                var nextLabel = il.DefineLabel();

                var endErrorLabel = il.DefineLabel();

                il.Emit(OpCodes.Ldloc, mapLocal);
                il.Emit(OpCodes.Ldstr, mem.Name);
                il.Emit(OpCodes.Ldloca_S, valueLocal);
                il.Emit(OpCodes.Call, Map_TryGetValue);
                il.Emit(OpCodes.Brtrue_S, endErrorLabel);

                EmitLogError(il, $"Missing key {mem.Name}", tailcall: false);
                il.Emit(OpCodes.Br, nextLabel);

                il.MarkLabel(endErrorLabel);

                il.Emit(OpCodes.Ldloc_S, valueLocal);
                EmitDeserializeMember(il, mem, nextLabel, il => il.Emit(OpCodes.Ldloc_S, valueLocal), GetLocal, thisobj, parentobj);

                il.MarkLabel(nextLabel);
            }
        }

        private static void EmitDeserializeConverter(ILGenerator il, SerializedMemberInfo member, Label nextLabel, LocalAllocator GetLocal,
            Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            using var stlocal = GetLocal.Allocate(typeof(Value));
            using var valLocal = GetLocal.Allocate(member.Type);

            il.Emit(OpCodes.Stloc, stlocal);
            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldsfld, member.ConverterField);
            il.Emit(OpCodes.Ldloc, stlocal);
            parentobj(il);

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
        private static void EmitDeserializeMember(ILGenerator il, SerializedMemberInfo member, Label nextLabel, Action<ILGenerator> getValue, LocalAllocator GetLocal, 
            Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

            var implLabel = il.DefineLabel();
            var passedTypeCheck = il.DefineLabel();
            var expectType = GetExpectedValueTypeForType(member.ConversionType);

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
                using var valTLocal = GetLocal.Allocate(member.Type);
                il.Emit(OpCodes.Ldloca, valTLocal);
                il.Emit(OpCodes.Initobj, member.Type);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, valTLocal), thisobj);
                il.Emit(OpCodes.Br, nextLabel);
            }
            else
            {
                il.Emit(OpCodes.Pop);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldnull), thisobj);
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

            using var local = GetLocal.Allocate(member.Type);
            if (member.HasConverter) EmitDeserializeConverter(il, member, nextLabel, GetLocal, thisobj, parentobj);
            else if (member.IsNullable) EmitDeserializeNullable(il, member, expectType, GetLocal, thisobj, parentobj);
            else EmitDeserializeValue(il, member, member.Type, expectType, GetLocal, thisobj, parentobj);
            il.Emit(OpCodes.Stloc, local);
            EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, local), thisobj);
        }
    }
}
