using IPA.Config.Data;
using System;
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
        // emit takes no args, leaves Value at top of stack
        private static void EmitSerializeMember(ILGenerator il, SerializedMemberInfo member, GetLocal GetLocal)
        {
            EmitLoad(il, member);

            var endSerialize = il.DefineLabel();

            if (member.AllowNull)
            {
                var passedNull = il.DefineLabel();

                il.Emit(OpCodes.Dup);
                if (member.IsNullable)
                    il.Emit(OpCodes.Call, member.Nullable_HasValue.GetGetMethod());
                il.Emit(OpCodes.Brtrue, passedNull);

                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Br, endSerialize);

                il.MarkLabel(passedNull);
            }

            if (member.IsNullable)
                il.Emit(OpCodes.Call, member.Nullable_Value.GetGetMethod());

            var memberConversionType = member.ConversionType;
            var targetType = GetExpectedValueTypeForType(memberConversionType);
            if (member.HasConverter)
            {
                var stlocal = GetLocal(member.Type);
                var valLocal = GetLocal(typeof(Value));

                il.Emit(OpCodes.Stloc, stlocal);
                il.BeginExceptionBlock();
                il.Emit(OpCodes.Ldsfld, member.ConverterField);
                il.Emit(OpCodes.Ldloc, stlocal);

                if (member.IsGenericConverter)
                {
                    var toValueBase = member.ConverterBase.GetMethod(nameof(ValueConverter<int>.ToValue),
                        new[] { member.ConverterTarget, typeof(object) });
                    var toValue = member.Converter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.GetBaseDefinition() == toValueBase) ?? toValueBase;
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, toValue);
                }
                else
                {
                    var toValueBase = typeof(IValueConverter).GetMethod(nameof(IValueConverter.ToValue),
                        new[] { typeof(object), typeof(object) });
                    var toValue = member.Converter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.GetBaseDefinition() == toValueBase) ?? toValueBase;
                    il.Emit(OpCodes.Box);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, toValue);
                }

                il.Emit(OpCodes.Stloc, valLocal);
                il.BeginCatchBlock(typeof(Exception));
                EmitWarnException(il, "Error serializing member using converter");
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stloc, valLocal);
                il.EndExceptionBlock();
                il.Emit(OpCodes.Ldloc, valLocal);
            }
            else if (targetType == typeof(Text))
            { // only happens when arg is a string or char
                var TextCreate = typeof(Value).GetMethod(nameof(Value.Text));
                if (member.Type == typeof(char))
                {
                    var strFromChar = typeof(char).GetMethod(nameof(char.ToString), new[] { typeof(char) });
                    il.Emit(OpCodes.Call, strFromChar);
                }
                il.Emit(OpCodes.Call, TextCreate);
            }
            else if (targetType == typeof(Boolean))
            {
                var BoolCreate = typeof(Value).GetMethod(nameof(Value.Bool));
                il.Emit(OpCodes.Call, BoolCreate);
            }
            else if (targetType == typeof(Integer))
            {
                var IntCreate = typeof(Value).GetMethod(nameof(Value.Integer));
                EmitNumberConvertTo(il, IntCreate.GetParameters()[0].ParameterType, member.Type);
                il.Emit(OpCodes.Call, IntCreate);
            }
            else if (targetType == typeof(FloatingPoint))
            {
                var FloatCreate = typeof(Value).GetMethod(nameof(Value.Float));
                EmitNumberConvertTo(il, FloatCreate.GetParameters()[0].ParameterType, member.Type);
                il.Emit(OpCodes.Call, FloatCreate);
            }
            else if (targetType == typeof(List))
            {
                // TODO: impl this (enumerables)
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
            else if (targetType == typeof(Map))
            {
                // TODO: support other aggregate types
                if (!memberConversionType.IsValueType)
                {
                    // if it is a reference type, we assume that its a generated type implementing IGeneratedStore
                    var IGeneratedStore_Serialize = typeof(IGeneratedStore).GetMethod(nameof(IGeneratedStore.Serialize));
                    var IGeneratedStoreT_CopyFrom = typeof(IGeneratedStore<>).MakeGenericType(member.Type)
                        .GetMethod(nameof(IGeneratedStore<object>.CopyFrom));

                    if (!member.IsVirtual)
                    {
                        var noCreate = il.DefineLabel();
                        var stlocal = GetLocal(member.Type);

                        // first check to make sure that this is an IGeneratedStore, because we don't control assignments to it
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
                        il.Emit(OpCodes.Brtrue_S, noCreate);
                        il.Emit(OpCodes.Stloc, stlocal);
                        EmitCreateChildGenerated(il, member.Type);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldloc, stlocal);
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Callvirt, IGeneratedStoreT_CopyFrom);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Stloc, stlocal);
                        EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, stlocal));
                        il.MarkLabel(noCreate);
                    }
                    il.Emit(OpCodes.Callvirt, IGeneratedStore_Serialize);
                }
                else
                { // generate serialization for value types

                }
            }

            il.MarkLabel(endSerialize);
        }
    }
}
