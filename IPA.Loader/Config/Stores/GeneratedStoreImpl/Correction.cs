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
            {
                if (expectType.IsValueType)
                { // custom value type
                    return ReadObjectMembers(memberType).Any(NeedsCorrection);
                }
                return true;
            }
            return false;
        }

        // expects start value on stack, exits with final value on stack
        private static void EmitCorrectMember(ILGenerator il, SerializedMemberInfo member, bool shouldLock, bool alwaysNew, LocalAllocator GetLocal,
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

            var convType = member.ConversionType;
            if (!convType.IsValueType)
            {
                // currently the only thing for this is where expect == Map, so do generate shit
                var copyFrom = typeof(IGeneratedStore<>).MakeGenericType(convType).GetMethod(nameof(IGeneratedStore<Config>.CopyFrom));
                var noCreate = il.DefineLabel();
                using var valLocal = GetLocal.Allocate(convType);

                if (member.AllowNull)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brfalse_S, endLabel); // thing is null, just bypass it all
                }
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
                EmitCreateChildGenerated(il, convType, parentobj);
                il.MarkLabel(noCreate);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldloc, valLocal);
                il.Emit(shouldLock ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, copyFrom);
            }
            else
            {
                // for special value types, we'll go ahead and correct each of their members
                var structure = ReadObjectMembers(convType);

                using var valueLocal = GetLocal.Allocate(convType);
                il.Emit(OpCodes.Stloc, valueLocal);

                void LdlocaValueLocal(ILGenerator il)
                    => il.Emit(OpCodes.Ldloca, valueLocal);

                foreach (var mem in structure)
                {
                    if (NeedsCorrection(mem))
                        EmitLoadCorrectStore(il, mem, shouldLock, alwaysNew, GetLocal,
                            LdlocaValueLocal, LdlocaValueLocal, parentobj);
                }

                il.Emit(OpCodes.Ldloc, valueLocal);
            }

            if (member.IsNullable)
                il.Emit(OpCodes.Newobj, member.Nullable_Construct);

            il.MarkLabel(endLabel);
        }

        private static void EmitLoadCorrectStore(ILGenerator il, SerializedMemberInfo member, bool shouldLock, bool alwaysNew, LocalAllocator GetLocal,
            Action<ILGenerator> loadFrom, Action<ILGenerator> storeTo, Action<ILGenerator> parentobj)
        {
            EmitStore(il, member, il =>
            {
                EmitLoad(il, member, loadFrom); // load the member
                EmitCorrectMember(il, member, shouldLock, alwaysNew, GetLocal, storeTo, parentobj); // correct it
            }, storeTo);
        }
    }
}
