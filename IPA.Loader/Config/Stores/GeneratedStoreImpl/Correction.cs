using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private static bool NeedsCorrection(SerializedMemberInfo member)
        {
            if (member.HasConverter) return false;
            var memberType = member.ConversionType;
            var expectType = GetExpectedValueTypeForType(memberType);

            if (expectType == typeof(Map)) // TODO: make this slightly saner
                return true;
            return false;
        }

        // expects start value on stack, exits with final value on stack
        private static void EmitCorrectMember(ILGenerator il, SerializedMemberInfo member, bool shouldLock, bool alwaysNew, GetLocal GetLocal,
            Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            if (!NeedsCorrection(member)) return;

            var endLabel = il.DefineLabel();

            if (member.IsNullable)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, member.Nullable_HasValue.GetGetMethod());
                il.Emit(OpCodes.Brfalse, endLabel);
                il.Emit(OpCodes.Call, member.Nullable_Value.GetGetMethod());
            }

            if (!member.ConversionType.IsValueType)
            {
                // currently the only thing for this is where expect == Map, so do generate shit
                var copyFrom = typeof(IGeneratedStore<>).MakeGenericType(member.ConversionType).GetMethod(nameof(IGeneratedStore<Config>.CopyFrom));
                var noCreate = il.DefineLabel();
                var valLocal = GetLocal(member.Type);

                if (!alwaysNew)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
                    il.Emit(OpCodes.Brtrue_S, endLabel); // our input is already something we like
                }
                il.Emit(OpCodes.Stloc, valLocal);
                if (!alwaysNew)
                {
                    EmitLoad(il, member, thisobj);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
                    il.Emit(OpCodes.Brtrue_S, noCreate);
                    il.Emit(OpCodes.Pop);
                }
                EmitCreateChildGenerated(il, member.Type, parentobj);
                il.MarkLabel(noCreate);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldloc, valLocal);
                il.Emit(shouldLock ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, copyFrom);
            }
            else
            {
                // TODO: impl the rest of this
            }

            if (member.IsNullable)
                il.Emit(OpCodes.Newobj, member.Nullable_Construct);

            il.MarkLabel(endLabel);
        }
    }
}
