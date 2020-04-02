using IPA.Config.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {

        internal delegate Value SerializeObject<T>(T obj);
        internal delegate T DeserializeObject<T>(Value val, object parent);

        private static class DelegateStore<T> 
        {
            public static SerializeObject<T> Serialize;
            public static DeserializeObject<T> Deserialize;
        }

        internal static SerializeObject<T> GetSerializerDelegate<T>()
            => DelegateStore<T>.Serialize ??= GetSerializerDelegateInternal<T>();

        private static SerializeObject<T> GetSerializerDelegateInternal<T>()
        {
            var type = typeof(T);
#if DEBUG
            var defType = Module.DefineType($"{type.FullName}<SerializeTypeContainer>", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            var dynMethod = defType.DefineMethod("SerializeType", MethodAttributes.Public | MethodAttributes.Static, typeof(Value), new[] { type });
#else
            var dynMethod = new DynamicMethod($"SerializeType>>{type.FullName}", typeof(Value), new[] { type }, Module, true);
#endif

            var structure = ReadObjectMembers(type);

            //CreateAndInitializeConvertersFor(type, structure);

            var loadObject = type.IsValueType
                ? (Action<ILGenerator>)(il => il.Emit(OpCodes.Ldarga_S, 0))
                : il => il.Emit(OpCodes.Ldarg_0);
            var loadParent = type.IsValueType
                ? (Action<ILGenerator>)(il => il.Emit(OpCodes.Ldnull))
                : loadObject;
            {
                var il = dynMethod.GetILGenerator();

                var GetLocal = MakeLocalAllocator(il);

                if (!type.IsValueType)
                {
                    var notIGeneratedStore = il.DefineLabel();
                    var IGeneratedStore_t = typeof(IGeneratedStore);
                    var IGeneratedStore_Serialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Serialize));

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Isinst, IGeneratedStore_t);
                    il.Emit(OpCodes.Brfalse, notIGeneratedStore);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Castclass, IGeneratedStore_t);
                    il.Emit(OpCodes.Callvirt, IGeneratedStore_Serialize);
                    il.Emit(OpCodes.Ret);

                    il.MarkLabel(notIGeneratedStore);
                }

                EmitSerializeStructure(il, structure, GetLocal, loadObject, loadParent);

                il.Emit(OpCodes.Ret);
            }

#if DEBUG
            defType.CreateType();
            return (SerializeObject<T>)Delegate.CreateDelegate(typeof(SerializeObject<T>), dynMethod);
#else
            return (SerializeObject<T>)dynMethod.CreateDelegate(typeof(SerializeObject<T>));
#endif
        }

        internal static DeserializeObject<T> GetDeserializerDelegate<T>()
            => DelegateStore<T>.Deserialize ??= GetDeserializerDelegateInternal<T>();

        private static DeserializeObject<T> GetDeserializerDelegateInternal<T>()
        {
            var type = typeof(T);
            //var dynMethod = new DynamicMethod($"DeserializeType>>{type.FullName}", type, new[] { typeof(Value), typeof(object) }, Module, true);

#if DEBUG
            var defType = Module.DefineType($"{type.FullName}<DeserializeTypeContainer>", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            var dynMethod = defType.DefineMethod("DeserializeType", MethodAttributes.Public | MethodAttributes.Static, type, new[] { typeof(Value), typeof(object) });
#else
            var dynMethod = new DynamicMethod($"DeserializeType>>{type.FullName}", type, new[] { typeof(Value), typeof(object) }, Module, true);
#endif

            var structure = ReadObjectMembers(type);

            //CreateAndInitializeConvertersFor(type, structure);

            {
                var il = dynMethod.GetILGenerator();

                var GetLocal = MakeLocalAllocator(il);

                var IGeneratedStore_t = typeof(IGeneratedStore);
                var IGeneratedStore_Deserialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Deserialize));

                void ParentObj(ILGenerator il)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Isinst, IGeneratedStore_t);
                }

                if (!type.IsValueType)
                {
                    EmitCreateChildGenerated(il, type, ParentObj);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Castclass, IGeneratedStore_t);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, IGeneratedStore_Deserialize);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    var Map_t = typeof(Map);
                    var Map_TryGetValue = Map_t.GetMethod(nameof(Map.TryGetValue));
                    var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

                    var valueLocal = il.DeclareLocal(typeof(Value));
                    var mapLocal = il.DeclareLocal(typeof(Map));
                    var resultLocal = il.DeclareLocal(type);

                    var nonNull = il.DefineLabel();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Brtrue, nonNull);

                    EmitLogError(il, "Attempting to deserialize null", tailcall: false);
                    il.Emit(OpCodes.Ldloc, resultLocal);
                    il.Emit(OpCodes.Ret);

                    il.MarkLabel(nonNull);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Isinst, Map_t);
                    il.Emit(OpCodes.Dup); // duplicate cloned value
                    il.Emit(OpCodes.Stloc, mapLocal);
                    var notMapError = il.DefineLabel();
                    il.Emit(OpCodes.Brtrue, notMapError);
                    // handle error
                    EmitLogError(il, $"Invalid root for deserializing {type.FullName}", tailcall: false,
                        expected: il => EmitTypeof(il, Map_t), found: il =>
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Callvirt, Object_GetType);
                        });
                    il.Emit(OpCodes.Ldloc, resultLocal);
                    il.Emit(OpCodes.Ret);

                    il.MarkLabel(notMapError);

                    EmitDeserializeStructure(il, structure, mapLocal, valueLocal, GetLocal, il => il.Emit(OpCodes.Ldloca, resultLocal), ParentObj);

                    il.Emit(OpCodes.Ldloc, resultLocal);
                    il.Emit(OpCodes.Ret);
                }
            }

#if DEBUG
            defType.CreateType();
            return (DeserializeObject<T>)Delegate.CreateDelegate(typeof(DeserializeObject<T>), dynMethod);
#else
            return (DeserializeObject<T>)dynMethod.CreateDelegate(typeof(DeserializeObject<T>));
#endif
        }
    }
}
