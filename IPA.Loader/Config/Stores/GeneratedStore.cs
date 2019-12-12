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
using System.Runtime.CompilerServices;
using System.IO;
using Boolean = IPA.Config.Data.Boolean;
using System.Collections;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.GeneratedAssemblyName)]

namespace IPA.Config.Stores
{
    /// <summary>
    /// A class providing an extension for <see cref="Config"/> to make it easy to use generated
    /// config stores.
    /// </summary>
    public static class GeneratedExtension
    {
        /// <summary>
        /// The name of the assembly that internals must be visible to to allow internal protection.
        /// </summary>
        public const string AssemblyVisibilityTarget = GeneratedStore.GeneratedAssemblyName;

        /// <summary>
        /// Creates a generated <see cref="IConfigStore"/> of type <typeparamref name="T"/>, registers it to
        /// the <see cref="Config"/> object, and returns it. This also forces a synchronous config load via
        /// <see cref="Config.LoadSync"/> if <paramref name="loadSync"/> is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <typeparamref name="T"/> must be a public non-<see langword="sealed"/> <see langword="class"/>.
        /// It can also be internal, but in that case, then your assembly must have the following attribute
        /// to allow the generated code to reference it.
        /// <code>
        /// [assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedExtension.AssemblyVisibilityTarget)]
        /// </code>
        /// </para>
        /// <para>
        /// If the <typeparamref name="T"/> declares a <see langword="public"/> or <see langword="protected"/>, <see langword="virtual"/>
        /// method <c>Changed()</c>, then that method may be called to artificially signal to the runtime that the content of the object 
        /// has changed. That method will also be called after the write locks are released when a property is set anywhere in the owning
        /// tree. This will only be called on the outermost generated object of the config structure, even if the change being signaled
        /// is somewhere deep into the tree.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a <see langword="public"/> or <see langword="protected"/>, <see langword="virtual"/> 
        /// method <c>OnReload()</c>, which will be called on the filesystem reader thread after the object has been repopulated with new data 
        /// values. It will be called <i>after</i> the write lock for this object is released. This will only be called on the outermost generated
        /// object of the config structure.
        /// </para>
        /// <para>
        /// TODO: describe details of generated stores
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <param name="cfg">the <see cref="Config"/> to register to</param>
        /// <param name="loadSync">whether to synchronously load the content, or trigger an async load</param>
        /// <returns>a generated instance of <typeparamref name="T"/> as a special <see cref="IConfigStore"/></returns>
        public static T Generated<T>(this Config cfg, bool loadSync = true) where T : class
        {
            var ret = GeneratedStore.Create<T>();
            cfg.SetStore(ret as IConfigStore);
            if (loadSync)
                cfg.LoadSync();
            else
                cfg.LoadAsync();

            return ret;
        }
    }

    internal static class GeneratedStore
    {
        internal interface IGeneratedStore
        {
            /// <summary>
            /// serializes/deserializes to Value
            /// </summary>
            Value Values { get; set; }
            Type Type { get; }
            IGeneratedStore Parent { get; }
            Impl Impl { get; }
            void OnReload();
        }

        internal class Impl : IConfigStore
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
            public static void ImplSignalChanged(IGeneratedStore s) => FindImpl(s).SignalChanged();
            public void SignalChanged() => resetEvent.Set(); 

            internal static MethodInfo ImplTakeReadMethod = typeof(Impl).GetMethod(nameof(ImplTakeRead));
            public static void ImplTakeRead(IGeneratedStore s) => FindImpl(s).TakeRead();
            public void TakeRead() => WriteSyncObject.EnterReadLock();

            internal static MethodInfo ImplReleaseReadMethod = typeof(Impl).GetMethod(nameof(ImplReleaseRead));
            public static void ImplReleaseRead(IGeneratedStore s) => FindImpl(s).ReleaseRead();
            public void ReleaseRead() => WriteSyncObject.ExitReadLock();

            internal static MethodInfo ImplTakeWriteMethod = typeof(Impl).GetMethod(nameof(ImplTakeWrite));
            public static void ImplTakeWrite(IGeneratedStore s) => FindImpl(s).TakeWrite();
            public void TakeWrite() => WriteSyncObject.EnterWriteLock();

            internal static MethodInfo ImplReleaseWriteMethod = typeof(Impl).GetMethod(nameof(ImplReleaseWrite));
            public static void ImplReleaseWrite(IGeneratedStore s) => FindImpl(s).ReleaseWrite();
            public void ReleaseWrite() => WriteSyncObject.ExitWriteLock();

            internal static MethodInfo FindImplMethod = typeof(Impl).GetMethod(nameof(FindImpl));
            public static Impl FindImpl(IGeneratedStore store)
            {
                while (store?.Parent != null) store = store.Parent; // walk to the top of the tree
                return store?.Impl;
            }



            internal static MethodInfo ReadFromMethod = typeof(Impl).GetMethod(nameof(ReadFrom));
            public void ReadFrom(IConfigProvider provider)
            {
                var values = provider.Load();
                Logger.config.Debug("Generated impl ReadFrom");
                Logger.config.Debug($"Read {values}");
                generated.Values = values;

                ReleaseWrite();
                generated.OnReload();
                TakeWrite(); // must take again for runtime to be happy (which is unfortunate)
            }

            internal static MethodInfo WriteToMethod = typeof(Impl).GetMethod(nameof(WriteTo));
            public void WriteTo(IConfigProvider provider)
            {
                var values = generated.Values;
                Logger.config.Debug("Generated impl WriteTo");
                Logger.config.Debug($"Serialized {values}");
                provider.Store(values);
            }
        }

        private static Dictionary<Type, Func<IGeneratedStore, IConfigStore>> generatedCreators = new Dictionary<Type, Func<IGeneratedStore, IConfigStore>>();
        private static Dictionary<Type, Dictionary<string, Type>> memberMaps = new Dictionary<Type, Dictionary<string, Type>>();

        public static T Create<T>() where T : class => (T)Create(typeof(T));

        public static IConfigStore Create(Type type) => Create(type, null);

        private static readonly MethodInfo CreateGParent = 
            typeof(GeneratedStore).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static, null, 
                                             CallingConventions.Any, new[] { typeof(IGeneratedStore) }, Array.Empty<ParameterModifier>());
        internal static T Create<T>(IGeneratedStore parent) where T : class => (T)Create(typeof(T), parent);

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

        internal const string GeneratedAssemblyName = "IPA.Config.Generated";

        private static AssemblyBuilder assembly = null;
        private static AssemblyBuilder Assembly
        {
            get
            {
                if (assembly == null)
                {
                    var name = new AssemblyName(GeneratedAssemblyName);
                    assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
                }

                return assembly;
            }
        }

        internal static void DebugSaveAssembly(string file)
        {
            Assembly.Save(file);
        }

        private static ModuleBuilder module = null;
        private static ModuleBuilder Module
        {
            get
            {
                if (module == null)
                    module = Assembly.DefineDynamicModule(Assembly.GetName().Name, Assembly.GetName().Name + ".dll");

                return module;
            }
        }

        private struct SerializedMemberInfo
        {
            public string Name;
            public MemberInfo Member;
            public bool IsVirtual;
            public bool IsField;
            public Type Type;
        }

        private static Func<IGeneratedStore, IConfigStore> MakeCreator(Type type)
        {
            var baseCtor = type.GetConstructor(Type.EmptyTypes); // get a default constructor
            if (baseCtor == null)
                throw new ArgumentException("Config type does not have a public parameterless constructor");

            var typeBuilder = Module.DefineType($"{type.FullName}<Generated>", 
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);


            // none of this can be Expressions because CompileToMethod requires a static target method for some dumbass reason

            #region Parse base object structure
            var baseChanged = type.GetMethod("Changed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChanged != null && !baseChanged.IsVirtual) baseChanged = null; // limit this to just the one thing
            var baseOnReload = type.GetMethod("OnReload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseOnReload != null && !baseOnReload.IsVirtual) baseOnReload = null; // limit this to just the one thing

            var structure = new List<SerializedMemberInfo>();

            // TODO: incorporate attributes/base types
            // TODO: ignore probs without setter
            
            // only looks at public properties
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var smi = new SerializedMemberInfo
                {
                    Name = prop.Name,
                    Member = prop,
                    IsVirtual = (prop.GetGetMethod(true)?.IsVirtual ?? false) ||
                                (prop.GetSetMethod(true)?.IsVirtual ?? false),
                    IsField = false,
                    Type = prop.PropertyType
                };

                structure.Add(smi);
            }

            // only look at public fields
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var smi = new SerializedMemberInfo
                {
                    Name = field.Name,
                    Member = field,
                    IsVirtual = false,
                    IsField = true,
                    Type = field.FieldType
                };

                structure.Add(smi);
            }
            #endregion

            #region Constructor
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(IGeneratedStore) });
            { // because this is a constructor, it has to be raw IL
                var il = ctor.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // keep this at bottom of stack

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, baseCtor);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldarg_1); // load parent
                il.Emit(OpCodes.Stfld, parentField);

                il.Emit(OpCodes.Dup);
                EmitTypeof(il, type);
                il.Emit(OpCodes.Stfld, typeField);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Newobj, Impl.Ctor);
                il.Emit(OpCodes.Stfld, implField);

                foreach (var member in structure)
                    EmitMemberFix(il, member);

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
            var IGeneratedStore_OnReload = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.OnReload));

            #region IGeneratedStore.OnReload
            var onReload = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.OnReload)}", virtualMemberMethod, null, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(onReload, IGeneratedStore_OnReload);
            if (baseOnReload != null) typeBuilder.DefineMethodOverride(onReload, baseOnReload);

            {
                var il = onReload.GetILGenerator();

                if (baseOnReload != null)
                {
                    il.Emit(OpCodes.Ldarg_0); // load this
                    il.Emit(OpCodes.Tailcall);
                    il.Emit(OpCodes.Call, baseOnReload); // load impl field
                }
                il.Emit(OpCodes.Ret);
            }
            #endregion
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

                var Map_Add = typeof(Map).GetMethod(nameof(Map.Add));

                il.Emit(OpCodes.Call, typeof(Value).GetMethod(nameof(Value.Map)));
                // the map is now at the top of the stack

                var locals = new List<LocalBuilder>();

                LocalBuilder GetLocal(Type ty, int i = 0)
                {
                    var builder = locals.Where(b => b.LocalType == ty).Skip(i).FirstOrDefault();
                    if (builder == null)
                    {
                        builder = il.DeclareLocal(ty);
                        locals.Add(builder);
                    }
                    return builder;
                }

                foreach (var member in structure)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldstr, member.Name); // TODO: make this behave with annotations
                    EmitSerializeMember(il, member, GetLocal);
                    il.Emit(OpCodes.Call, Map_Add);
                }

                // the map is still at the top of the stack, return it

                il.Emit(OpCodes.Ret);
            }

            { // this is non-locking because the only code that will call this will already own the correct lock
                var il = valuesPropSet.GetILGenerator();

                var Map_t = typeof(Map);
                var Map_TryGetValue = Map_t.GetMethod(nameof(Map.TryGetValue));
                var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

                var valueLocal = il.DeclareLocal(typeof(Value));

                var nonNull = il.DefineLabel();

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue, nonNull);

                EmitLogError(il, "Attempting to deserialize null", tailcall: true);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(nonNull);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Isinst, Map_t);
                il.Emit(OpCodes.Dup); // duplicate cloned value
                var notMapError = il.DefineLabel();
                il.Emit(OpCodes.Brtrue, notMapError);
                // handle error
                il.Emit(OpCodes.Pop); // removes the duplicate value
                EmitLogError(il, $"Invalid root for deserializing {type.FullName}", tailcall: true,
                    expected: il => EmitTypeof(il, Map_t), found: il =>
                    {
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Callvirt, Object_GetType);
                    });
                il.Emit(OpCodes.Ret);

                var nextLabel = notMapError;

                var locals = new List<LocalBuilder>();

                LocalBuilder GetLocal(Type ty, int i = 0)
                {
                    var builder = locals.Where(b => b.LocalType == ty).Skip(i).FirstOrDefault();
                    if (builder == null)
                    {
                        builder = il.DeclareLocal(ty);
                        locals.Add(builder);
                    }
                    return builder;
                }

                // head of stack is Map instance
                foreach (var member in structure)
                {
                    il.MarkLabel(nextLabel);
                    nextLabel = il.DefineLabel();
                    var endErrorLabel = il.DefineLabel();

                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldstr, member.Name);
                    il.Emit(OpCodes.Ldloca_S, valueLocal);
                    il.Emit(OpCodes.Call, Map_TryGetValue);
                    il.Emit(OpCodes.Brtrue_S, endErrorLabel);

                    EmitLogError(il, $"Missing key {member.Name}", tailcall: false);
                    il.Emit(OpCodes.Br, nextLabel);

                    il.MarkLabel(endErrorLabel);

                    il.Emit(OpCodes.Ldloc_S, valueLocal);
                    EmitDeserializeMember(il, member, nextLabel, il => il.Emit(OpCodes.Ldloc_S, valueLocal), GetLocal);
                }

                il.MarkLabel(nextLabel);

                il.Emit(OpCodes.Pop); // removes the duplicate value
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
                var il = readFrom.GetILGenerator();

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
                foreach (var member in structure)
                    dict.Add(member.Name, member.Type);
                memberMaps.Add(type, dict);
            }

            return creatorDel;
        }

        #region Utility
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

        private static Type Decimal_t = typeof(decimal);
        private static ConstructorInfo Decimal_FromFloat = Decimal_t.GetConstructor(new[] { typeof(float) });
        private static ConstructorInfo Decimal_FromDouble = Decimal_t.GetConstructor(new[] { typeof(double) });
        private static ConstructorInfo Decimal_FromInt = Decimal_t.GetConstructor(new[] { typeof(int) });
        private static ConstructorInfo Decimal_FromUInt = Decimal_t.GetConstructor(new[] { typeof(uint) });
        private static ConstructorInfo Decimal_FromLong = Decimal_t.GetConstructor(new[] { typeof(long) });
        private static ConstructorInfo Decimal_FromULong = Decimal_t.GetConstructor(new[] { typeof(ulong) });
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

        private static void EmitCreateChildGenerated(ILGenerator il, Type childType)
        {
            var method = CreateGParent.MakeGenericMethod(childType);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, method);
        }
        #endregion


        private static readonly MethodInfo LogErrorMethod = typeof(GeneratedStore).GetMethod(nameof(LogError), BindingFlags.NonPublic | BindingFlags.Static);
        internal static void LogError(Type expected, Type found, string message)
        {
            Logger.config.Notice($"{message}{(expected == null ? "" : $" (expected {expected}, found {found?.ToString() ?? "null"})")}");
        }

        // expects the this param to be on the stack
        private static void EmitMemberFix(ILGenerator il, SerializedMemberInfo member)
        {
            // TODO: impl
        }

        #region Serialize

        // emit takes no args, leaves Value at top of stack
        private static void EmitSerializeMember(ILGenerator il, SerializedMemberInfo member, Func<Type, int, LocalBuilder> GetLocal)
        {
            void EmitLoad()
            {
                il.Emit(OpCodes.Ldarg_0); // load this

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

            // TODO: implement Nullable<T>

            EmitLoad();

            var endSerialize = il.DefineLabel();

            if (!member.Type.IsValueType)
            {
                var passedNull = il.DefineLabel();
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue, passedNull);

                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Br, endSerialize);

                il.MarkLabel(passedNull);
            }

            var targetType = GetExpectedValueTypeForType(member.Type);
            if (targetType == typeof(Text))
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
                // TODO: impl this
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
            else if (targetType == typeof(Map))
            {
                // TODO: support other aggregate types

                // for now, we assume that its a generated type implementing IGeneratedStore
                var IGeneratedStore_ValueGet = typeof(IGeneratedStore).GetProperty(nameof(IGeneratedStore.Values)).GetGetMethod();
                il.Emit(OpCodes.Callvirt, IGeneratedStore_ValueGet);
            }

            il.MarkLabel(endSerialize);

            // TODO: implement converters
        }
        #endregion

        #region Deserialize

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
             || valT == typeof(ulong)) return typeof(Integer);
            if (valT == typeof(float)
             || valT == typeof(double)
             || valT == typeof(decimal)) return typeof(FloatingPoint);
            if (typeof(IEnumerable).IsAssignableFrom(valT)) return typeof(List);

            // TODO: fill this out the rest of the way
            // TODO: support converters

            return typeof(Map); // default for various objects
        }

        private static void EmitDeserializeGeneratedValue(ILGenerator il, Type targetType, Type srcType, Func<Type, int, LocalBuilder> GetLocal)
        {
            var IGeneratedStore_ValueSet = typeof(IGeneratedStore).GetProperty(nameof(IGeneratedStore.Values)).GetSetMethod();

            var valuel = GetLocal(srcType, 0);
            il.Emit(OpCodes.Stloc, valuel);
            EmitCreateChildGenerated(il, targetType);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, valuel);
            il.Emit(OpCodes.Callvirt, IGeneratedStore_ValueSet);
        }

        // top of stack is the Value to deserialize; the type will be as returned from GetExpectedValueTypeForType
        // after, top of stack will be thing to write to field
        private static void EmitDeserializeValue(ILGenerator il, Type targetType, Type expected, Func<Type, int, LocalBuilder> GetLocal)
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
                EmitDeserializeGeneratedValue(il, targetType, expected, GetLocal);
            }
            else // TODO: support converters
            {
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
        }

        // emit takes the value being deserialized, logs on error, leaves nothing on stack
        private static void EmitDeserializeMember(ILGenerator il, SerializedMemberInfo member, Label nextLabel, Action<ILGenerator> getValue, Func<Type, int, LocalBuilder> GetLocal)
        {
            var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

            var implLabel = il.DefineLabel();
            var passedTypeCheck = il.DefineLabel();
            var expectType = GetExpectedValueTypeForType(member.Type);

            void EmitStore(Action<ILGenerator> value)
            {
                il.Emit(OpCodes.Ldarg_0); // load this
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

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brtrue_S, implLabel); // null check

            // TODO: support Nullable<T>

            if (member.Type.IsValueType)
            {
                il.Emit(OpCodes.Pop);
                EmitLogError(il, $"Member {member.Name} ({member.Type}) not nullable", tailcall: false,
                    expected: il => EmitTypeof(il, expectType));
                il.Emit(OpCodes.Br, nextLabel);
            }
            else
            {
                il.Emit(OpCodes.Pop);
                EmitStore(il => il.Emit(OpCodes.Ldnull));
                il.Emit(OpCodes.Br, nextLabel);
            }

            il.MarkLabel(implLabel);

            il.Emit(OpCodes.Isinst, expectType); //replaces on stack
            il.Emit(OpCodes.Dup); // duplicate cloned value
            il.Emit(OpCodes.Brtrue, passedTypeCheck); // null check

            var errorHandle = il.DefineLabel();

            // special cases to handle coersion between Float and Int
            if (expectType == typeof(FloatingPoint))
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

            il.MarkLabel(errorHandle);
            il.Emit(OpCodes.Pop);
            EmitLogError(il, $"Unexpected type deserializing {member.Name}", tailcall: false,
                expected: il => EmitTypeof(il, expectType), found: il =>
                {
                    getValue(il);
                    il.Emit(OpCodes.Callvirt, Object_GetType);
                });
            il.Emit(OpCodes.Br, nextLabel);

            il.MarkLabel(passedTypeCheck);

            var local = GetLocal(member.Type, 0);
            EmitDeserializeValue(il, member.Type, expectType, GetLocal);
            il.Emit(OpCodes.Stloc, local);
            EmitStore(il => il.Emit(OpCodes.Ldloc, local));
        }
        #endregion
    }
}
