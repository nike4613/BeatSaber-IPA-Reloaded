using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        #region Logs
        private static readonly MethodInfo LogErrorMethod = typeof(GeneratedStoreImpl).GetMethod(nameof(LogError), BindingFlags.NonPublic | BindingFlags.Static);
        internal static void LogError(Type expected, Type found, string message)
        {
            Logger.config.Notice($"{message}{(expected == null ? "" : $" (expected {expected}, found {found?.ToString() ?? "null"})")}");
        }
        private static readonly MethodInfo LogWarningMethod = typeof(GeneratedStoreImpl).GetMethod(nameof(LogWarning), BindingFlags.NonPublic | BindingFlags.Static);
        internal static void LogWarning(string message)
        {
            Logger.config.Warn(message);
        }
        private static readonly MethodInfo LogWarningExceptionMethod = typeof(GeneratedStoreImpl).GetMethod(nameof(LogWarningException), BindingFlags.NonPublic | BindingFlags.Static);
        internal static void LogWarningException(Exception exception)
        {
            Logger.config.Warn(exception);
        }
        #endregion

        //private delegate LocalBuilder LocalAllocator(Type type, int idx = 0);

        private static LocalAllocator MakeLocalAllocator(ILGenerator il)
            => new LocalAllocator(il);

        private struct AllocatedLocal : IDisposable
        {
            internal readonly LocalAllocator allocator;
            public LocalBuilder Local { get; }

            public AllocatedLocal(LocalAllocator alloc, LocalBuilder builder)
            {
                allocator = alloc;
                Local = builder;
            }

#if NET4
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public static implicit operator LocalBuilder(AllocatedLocal loc) => loc.Local;

            public void Dealloc() => allocator.Deallocate(this);
            public void Dispose() => Dealloc();
        }

        private sealed class LocalAllocator
        {
            private readonly ILGenerator ilSource;
            private readonly Dictionary<Type, Stack<LocalBuilder>> unallocatedLocals = new Dictionary<Type, Stack<LocalBuilder>>();
            public LocalAllocator(ILGenerator il)
                => ilSource = il;

            private Stack<LocalBuilder> GetLocalListForType(Type type)
            {
                if (!unallocatedLocals.TryGetValue(type, out var list))
                    unallocatedLocals.Add(type, list = new Stack<LocalBuilder>());
                return list;
            }

            public AllocatedLocal Allocate(Type type)
            {
                var list = GetLocalListForType(type);
                if (list.Count < 1) list.Push(ilSource.DeclareLocal(type));
                return new AllocatedLocal(this, list.Pop());
            }

            public void Deallocate(AllocatedLocal loc)
            {
                Debug.Assert(loc.allocator == this);
                var list = GetLocalListForType(loc.Local.LocalType);
                list.Push(loc.Local);
            }
        }

        private static void EmitLoad(ILGenerator il, SerializedMemberInfo member, Action<ILGenerator> thisarg)
        {
            thisarg(il); // load this

            if (member.IsField)
                il.Emit(OpCodes.Ldfld, member.Member as FieldInfo);
            else
            { // member is a property
                var prop = member.Member as PropertyInfo;
                var getter = prop.GetGetMethod();
                if (getter == null) throw new InvalidOperationException($"Property {member.Name} does not have a getter and is not ignored");

                il.Emit(OpCodes.Call, getter);
            }
        }

        private static void EmitStore(ILGenerator il, SerializedMemberInfo member, Action<ILGenerator> value, Action<ILGenerator> thisobj)
        {
            thisobj(il);
            value(il);

            if (member.IsField)
                il.Emit(OpCodes.Stfld, member.Member as FieldInfo);
            else
            { // member is a property
                var prop = member.Member as PropertyInfo;
                var setter = prop.GetSetMethod();
                if (setter == null) throw new InvalidOperationException($"Property {member.Name} does not have a setter and is not ignored");

                il.Emit(OpCodes.Call, setter);
            }
        }

        private static void EmitWarnException(ILGenerator il, string v)
        {
            il.Emit(OpCodes.Ldstr, v);
            il.Emit(OpCodes.Call, LogWarningMethod);
            il.Emit(OpCodes.Call, LogWarningExceptionMethod);
        }

        private static void EmitLogError(ILGenerator il, string message, bool tailcall = false, Action<ILGenerator> expected = null, Action<ILGenerator> found = null)
        {
            if (expected == null) expected = il => il.Emit(OpCodes.Ldnull);
            if (found == null) found = il => il.Emit(OpCodes.Ldnull);

            expected(il);
            found(il);
            il.Emit(OpCodes.Ldstr, message);
            if (tailcall) il.Emit(OpCodes.Tailcall);
            il.Emit(OpCodes.Call, LogErrorMethod);
        }

        private static readonly MethodInfo Type_GetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
        private static void EmitTypeof(ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, Type_GetTypeFromHandle);
        }

        private static readonly Type IDisposable_t = typeof(IDisposable);
        private static readonly MethodInfo IDisposable_Dispose = IDisposable_t.GetMethod(nameof(IDisposable.Dispose));

        private static readonly Type Decimal_t = typeof(decimal);
        private static readonly ConstructorInfo Decimal_FromFloat = Decimal_t.GetConstructor(new[] { typeof(float) });
        private static readonly ConstructorInfo Decimal_FromDouble = Decimal_t.GetConstructor(new[] { typeof(double) });
        private static readonly ConstructorInfo Decimal_FromInt = Decimal_t.GetConstructor(new[] { typeof(int) });
        private static readonly ConstructorInfo Decimal_FromUInt = Decimal_t.GetConstructor(new[] { typeof(uint) });
        private static readonly ConstructorInfo Decimal_FromLong = Decimal_t.GetConstructor(new[] { typeof(long) });
        private static readonly ConstructorInfo Decimal_FromULong = Decimal_t.GetConstructor(new[] { typeof(ulong) });
        private static void EmitNumberConvertTo(ILGenerator il, Type to, Type from)
        { // WARNING: THIS USES THE NO-OVERFLOW OPCODES
            if (to == from) return;
            if (to == Decimal_t)
            {
                if (from == typeof(float)) il.Emit(OpCodes.Newobj, Decimal_FromFloat);
                else if (from == typeof(double)) il.Emit(OpCodes.Newobj, Decimal_FromDouble);
                else if (from == typeof(long)) il.Emit(OpCodes.Newobj, Decimal_FromLong);
                else if (from == typeof(ulong)) il.Emit(OpCodes.Newobj, Decimal_FromULong);
                else if (from == typeof(int)) il.Emit(OpCodes.Newobj, Decimal_FromInt);
                else if (from == typeof(uint)) il.Emit(OpCodes.Newobj, Decimal_FromUInt);
                else if (from == typeof(IntPtr))
                {
                    EmitNumberConvertTo(il, typeof(long), from);
                    EmitNumberConvertTo(il, to, typeof(long));
                }
                else if (from == typeof(UIntPtr))
                {
                    EmitNumberConvertTo(il, typeof(ulong), from);
                    EmitNumberConvertTo(il, to, typeof(ulong));
                }
                else
                { // if the source is anything else, we first convert to int because that can contain all other values
                    EmitNumberConvertTo(il, typeof(int), from);
                    EmitNumberConvertTo(il, to, typeof(int));
                };
            }
            else if (from == Decimal_t)
            {
                if (to == typeof(IntPtr))
                {
                    EmitNumberConvertTo(il, typeof(long), from);
                    EmitNumberConvertTo(il, to, typeof(long));
                }
                else if (to == typeof(UIntPtr))
                {
                    EmitNumberConvertTo(il, typeof(ulong), from);
                    EmitNumberConvertTo(il, to, typeof(ulong));
                }
                else
                {
                    var method = Decimal_t.GetMethod($"To{to.Name}"); // conveniently, this is the pattern of the to* names
                    il.Emit(OpCodes.Call, method);
                }
            }
            else if (to == typeof(IntPtr)) il.Emit(OpCodes.Conv_I);
            else if (to == typeof(UIntPtr)) il.Emit(OpCodes.Conv_U);
            else if (to == typeof(sbyte)) il.Emit(OpCodes.Conv_I1);
            else if (to == typeof(byte)) il.Emit(OpCodes.Conv_U1);
            else if (to == typeof(short)) il.Emit(OpCodes.Conv_I2);
            else if (to == typeof(ushort)) il.Emit(OpCodes.Conv_U2);
            else if (to == typeof(int)) il.Emit(OpCodes.Conv_I4);
            else if (to == typeof(uint)) il.Emit(OpCodes.Conv_U4);
            else if (to == typeof(long)) il.Emit(OpCodes.Conv_I8);
            else if (to == typeof(ulong)) il.Emit(OpCodes.Conv_U8);
            else if (to == typeof(float))
            {
                if (from == typeof(byte)
                 || from == typeof(ushort)
                 || from == typeof(uint)
                 || from == typeof(ulong)
                 || from == typeof(UIntPtr)) il.Emit(OpCodes.Conv_R_Un);
                il.Emit(OpCodes.Conv_R4);
            }
            else if (to == typeof(double))
            {
                if (from == typeof(byte)
                 || from == typeof(ushort)
                 || from == typeof(uint)
                 || from == typeof(ulong)
                 || from == typeof(UIntPtr)) il.Emit(OpCodes.Conv_R_Un);
                il.Emit(OpCodes.Conv_R8);
            }
        }

        private static void EmitCreateChildGenerated(ILGenerator il, Type childType, Action<ILGenerator> parentobj)
        {
            var method = CreateGParent.MakeGenericMethod(childType);
            parentobj(il);
            il.Emit(OpCodes.Call, method);
        }

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
    }
}
