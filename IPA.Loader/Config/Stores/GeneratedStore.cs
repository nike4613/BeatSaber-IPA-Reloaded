using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    /// <summary>
    /// A class providing an extension for <see cref="Config"/> to make it easy to use generated
    /// config stores.
    /// </summary>
    public static class GeneratedStoreExtensions
    {
        /// <summary>
        /// Creates a generated <see cref="IConfigStore"/> of type <typeparamref name="T"/>, registers it to
        /// the <see cref="Config"/> object, and returns it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <typeparamref name="T"/> must be a non-<see langword="sealed"/> <see langword="class"/>.
        /// </para>
        /// <para>
        /// TODO: describe details of generated stores
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <param name="cfg">the <see cref="Config"/> to register to</param>
        /// <returns>a generated instance of <typeparamref name="T"/> as a special <see cref="IConfigStore"/></returns>
        public static T Generated<T>(this Config cfg) where T : class
        {
            var ret = GeneratedStore.Create<T>();
            cfg.AddStore(ret as IConfigStore);
            return ret;
        }
    }

    internal static class GeneratedStore
    {
        private interface IGeneratedStore
        {
            /// <summary>
            /// serializes/deserializes to Value
            /// </summary>
            Value Values { get; set; }
            Type Type { get; }
            IGeneratedStore Parent { get; }
            Impl Impl { get; }
        }

        private class Impl : IConfigStore
        {
            private IGeneratedStore generated;

            internal static ConstructorInfo Ctor = typeof(Impl).GetConstructor(new[] { typeof(IGeneratedStore) });
            public Impl(IGeneratedStore store) => generated = store;

            private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
            public WaitHandle SyncObject => resetEvent;
            internal static MethodInfo SyncObjectGetMethod = typeof(Impl).GetProperty(nameof(SyncObject)).GetGetMethod();

            public ReaderWriterLockSlim WriteSyncObject { get; } = new ReaderWriterLockSlim();
            internal static MethodInfo WriteSyncObjectGetMethod = typeof(Impl).GetProperty(nameof(WriteSyncObject)).GetGetMethod();

            internal static MethodInfo ImplSignalChangedMethod = typeof(Impl).GetMethod(nameof(ImplSignalChanged));
            internal static void ImplSignalChanged(IGeneratedStore s) => FindImpl(s).SignalChanged();
            internal void SignalChanged() => resetEvent.Set(); 

            internal static MethodInfo ImplTakeReadMethod = typeof(Impl).GetMethod(nameof(ImplTakeRead));
            internal static void ImplTakeRead(IGeneratedStore s) => FindImpl(s).TakeRead();
            internal void TakeRead() => WriteSyncObject.EnterReadLock();

            internal static MethodInfo ImplReleaseReadMethod = typeof(Impl).GetMethod(nameof(ImplReleaseRead));
            internal static void ImplReleaseRead(IGeneratedStore s) => FindImpl(s).ReleaseRead();
            internal void ReleaseRead() => WriteSyncObject.ExitWriteLock();

            internal static MethodInfo ImplTakeWriteMethod = typeof(Impl).GetMethod(nameof(ImplTakeWrite));
            internal static void ImplTakeWrite(IGeneratedStore s) => FindImpl(s).TakeWrite();
            internal void TakeWrite() => WriteSyncObject.EnterWriteLock();

            internal static MethodInfo ImplReleaseWriteMethod = typeof(Impl).GetMethod(nameof(ImplReleaseWrite));
            internal static void ImplReleaseWrite(IGeneratedStore s) => FindImpl(s).ReleaseWrite();
            internal void ReleaseWrite() => WriteSyncObject.ExitWriteLock();

            internal static MethodInfo FindImplMethod = typeof(Impl).GetMethod(nameof(FindImpl));
            internal static Impl FindImpl(IGeneratedStore store)
            {
                while (store != null) store = store.Parent; // walk to the top of the tree
                return store?.Impl;
            }



            internal static MethodInfo ReadFromMethod = typeof(Impl).GetMethod(nameof(ReadFrom));
            public void ReadFrom(IConfigProvider provider)
            {
                // TODO: implement
                Logger.config.Debug("Generated impl ReadFrom");
                Logger.config.Debug($"Read {provider.Load()}");
            }

            internal static MethodInfo WriteToMethod = typeof(Impl).GetMethod(nameof(WriteTo));
            public void WriteTo(IConfigProvider provider)
            {
                var values = generated.Values;
                // TODO: implement
                Logger.config.Debug("Generated impl WriteTo");
            }
        }

        private static Dictionary<Type, Func<IGeneratedStore, IConfigStore>> generatedCreators = new Dictionary<Type, Func<IGeneratedStore, IConfigStore>>();
        private static Dictionary<Type, Dictionary<string, Type>> memberMaps = new Dictionary<Type, Dictionary<string, Type>>();

        public static T Create<T>() where T : class => (T)Create(typeof(T));

        public static IConfigStore Create(Type type) => Create(type, null);

        private static IConfigStore Create(Type type, IGeneratedStore parent)
        {
            if (generatedCreators.TryGetValue(type, out var creator))
                return creator(parent);
            else
            {
                creator = MakeCreator(type);
                generatedCreators.Add(type, creator);
                return creator(parent);
            }
        }

        private static AssemblyBuilder assembly = null;
        private static AssemblyBuilder Assembly
        {
            get
            {
                if (assembly == null)
                {
                    var name = new AssemblyName("IPA.Config.Generated");
                    assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
                }

                return assembly;
            }
        }
        private static ModuleBuilder module = null;
        private static ModuleBuilder Module
        {
            get
            {
                if (module == null)
                    module = Assembly.DefineDynamicModule(Assembly.GetName().Name);

                return module;
            }
        }

        private struct SerializedMemberInfo
        {
            public string Name;
            public MemberInfo Member;
            public bool IsVirtual;
            public Type Type;
        }

        private static Func<IGeneratedStore, IConfigStore> MakeCreator(Type type)
        {
            var baseCtor = type.GetConstructor(Type.EmptyTypes); // get a default constructor
            if (baseCtor == null)
                throw new ArgumentException("Config type does not have a public parameterless constructor");

            var typeBuilder = Module.DefineType($"{type.FullName}.Generated", 
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);

            var GetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));

            // TODO: possibly move all of this manual IL over to Linq.Expressions

            #region Parse base object structure
            var baseChanged = type.GetMethod("Changed", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChanged != null && !baseChanged.IsVirtual) baseChanged = null; // limit this to just the one thing

            var structure = new Dictionary<string, SerializedMemberInfo>();

            // TODO: incorporate attributes
            
            // only looks at public properties
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var smi = new SerializedMemberInfo
                {
                    Name = prop.Name,
                    Member = prop,
                    IsVirtual = (prop.GetGetMethod(true)?.IsVirtual ?? false) ||
                                (prop.GetSetMethod(true)?.IsVirtual ?? false),
                    Type = prop.PropertyType
                };

                structure.Add(smi.Name, smi);
            }

            // only look at public fields
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var smi = new SerializedMemberInfo
                {
                    Name = field.Name,
                    Member = field,
                    IsVirtual = false,
                    Type = field.FieldType
                };

                structure.Add(smi.Name, smi);
            }
            #endregion

            #region Constructor
            // takes its parent
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(IGeneratedStore) });
            {
                var il = ctor.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // keep this at bottom of stack

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, baseCtor);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldarg_1); // load parent
                il.Emit(OpCodes.Stfld, parentField);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldtoken, type);
                il.Emit(OpCodes.Call, GetTypeFromHandle); // effectively typeof(type)
                il.Emit(OpCodes.Stfld, typeField);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Newobj, Impl.Ctor);
                il.Emit(OpCodes.Stfld, implField);

                foreach (var kvp in structure)
                    EmitMemberFix(il, kvp.Value);

                il.Emit(OpCodes.Pop);

                il.Emit(OpCodes.Ret);
            }
            #endregion

            const MethodAttributes propertyMethodAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            const MethodAttributes virtualPropertyMethodAttr = propertyMethodAttr | MethodAttributes.Virtual | MethodAttributes.Final;
            const MethodAttributes virtualMemberMethod = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final;

            #region IGeneratedStore
            typeBuilder.AddInterfaceImplementation(typeof(IGeneratedStore));

            var IGeneratedStore_t = typeof(IGeneratedStore);
            var IGeneratedStore_GetImpl = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Impl)).GetGetMethod();
            var IGeneratedStore_GetType = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Type)).GetGetMethod();
            var IGeneratedStore_GetParent = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Parent)).GetGetMethod();
            var IGeneratedStore_GetValues = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Values)).GetGetMethod();
            var IGeneratedStore_SetValues = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Values)).GetSetMethod();

            #region IGeneratedStore.Impl
            var implProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Impl), PropertyAttributes.None, typeof(Impl), null);
            var implPropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Impl)}", virtualPropertyMethodAttr, implProp.PropertyType, Type.EmptyTypes);
            implProp.SetGetMethod(implPropGet);
            typeBuilder.DefineMethodOverride(implPropGet, IGeneratedStore_GetImpl);

            {
                var il = implPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, implField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Type
            var typeProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Type), PropertyAttributes.None, typeof(Type), null);
            var typePropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Type)}", virtualPropertyMethodAttr, typeProp.PropertyType, Type.EmptyTypes);
            typeProp.SetGetMethod(typePropGet);
            typeBuilder.DefineMethodOverride(typePropGet, IGeneratedStore_GetType);

            {
                var il = typePropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, typeField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Parent
            var parentProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Parent), PropertyAttributes.None, typeof(IGeneratedStore), null);
            var parentPropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Parent)}", virtualPropertyMethodAttr, parentProp.PropertyType, Type.EmptyTypes);
            parentProp.SetGetMethod(parentPropGet);
            typeBuilder.DefineMethodOverride(parentPropGet, IGeneratedStore_GetParent);

            {
                var il = parentPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, parentField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Values
            var valuesProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Values), PropertyAttributes.None, typeof(Value), null);
            var valuesPropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Values)}", virtualPropertyMethodAttr, valuesProp.PropertyType, Type.EmptyTypes);
            var valuesPropSet = typeBuilder.DefineMethod($"<s>{nameof(IGeneratedStore.Values)}", virtualPropertyMethodAttr, null, new[] { valuesProp.PropertyType });
            valuesProp.SetGetMethod(valuesPropGet);
            typeBuilder.DefineMethodOverride(valuesPropGet, IGeneratedStore_GetValues);
            valuesProp.SetSetMethod(valuesPropSet);
            typeBuilder.DefineMethodOverride(valuesPropSet, IGeneratedStore_SetValues);

            { // this is non-locking because the only code that will call this will already own the correct lock
                var il = valuesPropGet.GetILGenerator();

                // TODO: implement get_Values
                il.Emit(OpCodes.Ldnull);

                il.Emit(OpCodes.Ret);
            }

            { // this is non-locking because the only code that will call this will already own the correct lock
                var il = valuesPropSet.GetILGenerator();

                // TODO: implement set_Values

                il.Emit(OpCodes.Ret);
            }
            #endregion
            #endregion

            #region IConfigStore
            typeBuilder.AddInterfaceImplementation(typeof(IConfigStore));

            var IConfigStore_t = typeof(IConfigStore);
            var IConfigStore_GetSyncObject = IConfigStore_t.GetProperty(nameof(IConfigStore.SyncObject)).GetGetMethod();
            var IConfigStore_GetWriteSyncObject = IConfigStore_t.GetProperty(nameof(IConfigStore.WriteSyncObject)).GetGetMethod();
            var IConfigStore_WriteTo = IConfigStore_t.GetMethod(nameof(IConfigStore.WriteTo));
            var IConfigStore_ReadFrom = IConfigStore_t.GetMethod(nameof(IConfigStore.ReadFrom));

            #region IConfigStore.SyncObject
            var syncObjProp = typeBuilder.DefineProperty(nameof(IConfigStore.SyncObject), PropertyAttributes.None, typeof(WaitHandle), null);
            var syncObjPropGet = typeBuilder.DefineMethod($"<g>{nameof(IConfigStore.SyncObject)}", virtualPropertyMethodAttr, syncObjProp.PropertyType, Type.EmptyTypes);
            syncObjProp.SetGetMethod(syncObjPropGet);
            typeBuilder.DefineMethodOverride(syncObjPropGet, IConfigStore_GetSyncObject);

            {
                var il = syncObjPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Impl.FindImplMethod);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.SyncObjectGetMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IConfigStore.WriteSyncObject
            var writeSyncObjProp = typeBuilder.DefineProperty(nameof(IConfigStore.WriteSyncObject), PropertyAttributes.None, typeof(WaitHandle), null);
            var writeSyncObjPropGet = typeBuilder.DefineMethod($"<g>{nameof(IConfigStore.WriteSyncObject)}", virtualPropertyMethodAttr, writeSyncObjProp.PropertyType, Type.EmptyTypes);
            writeSyncObjProp.SetGetMethod(writeSyncObjPropGet);
            typeBuilder.DefineMethodOverride(writeSyncObjPropGet, IConfigStore_GetWriteSyncObject);

            {
                var il = writeSyncObjPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Impl.FindImplMethod);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.WriteSyncObjectGetMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IConfigStore.WriteTo
            var writeTo = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.WriteTo)}", virtualMemberMethod, null, new[] { typeof(IConfigProvider) });
            typeBuilder.DefineMethodOverride(writeTo, IConfigStore_WriteTo);

            {
                var il = writeTo.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Impl.FindImplMethod);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.WriteToMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IConfigStore.ReadFrom
            var readFrom = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.ReadFrom)}", virtualMemberMethod, null, new[] { typeof(IConfigProvider) });
            typeBuilder.DefineMethodOverride(readFrom, IConfigStore_ReadFrom);

            {
                var il = writeTo.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Impl.FindImplMethod);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ReadFromMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #endregion

            #region Changed
            var coreChanged = typeBuilder.DefineMethod(
                "<>Changed",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                null, Type.EmptyTypes);

            {
                var il = coreChanged.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, Impl.ImplSignalChangedMethod);
                il.Emit(OpCodes.Ret); // simply call our impl's SignalChanged method and return
            }

            if (baseChanged != null) {
                var changedMethod = typeBuilder.DefineMethod( // copy to override baseChanged
                    baseChanged.Name, 
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig, 
                    null, Type.EmptyTypes);
                typeBuilder.DefineMethodOverride(changedMethod, baseChanged);

                {
                    var il = changedMethod.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, baseChanged); // call base

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Tailcall);
                    il.Emit(OpCodes.Call, coreChanged); // call back to the core change method

                    il.Emit(OpCodes.Ret);
                }

                coreChanged = changedMethod; // switch to calling this version instead of just the default
            }
            #endregion

            // TODO: generate overrides for all the virtual properties

            var genType = typeBuilder.CreateType();

            var parentParam = Expression.Parameter(typeof(IGeneratedStore), "parent");
            var creatorDel = Expression.Lambda<Func<IGeneratedStore, IConfigStore>>(
                Expression.New(ctor, parentParam), parentParam
            ).Compile();

            { // register a member map
                var dict = new Dictionary<string, Type>();
                foreach (var kvp in structure)
                    dict.Add(kvp.Key, kvp.Value.Type);
                memberMaps.Add(type, dict);
            }

            return creatorDel;
        }

        // expects the this param to be on the stack
        private static void EmitMemberFix(ILGenerator il, SerializedMemberInfo member)
        {

        }

    }
}
