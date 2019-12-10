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
    public static class GeneratedStoreExtensions
    {
        /// <summary>
        /// Creates a generated <see cref="IConfigStore"/> of type <typeparamref name="T"/>, registers it to
        /// the <see cref="Config"/> object, and returns it. This also forces a synchronous config load via
        /// <see cref="Config.LoadSync"/> if <paramref name="loadSync"/> is <see langword="true"/>.
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
            public void ReleaseRead() => WriteSyncObject.ExitWriteLock();

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

            var typeBuilder = Module.DefineType($"{type.FullName}.Generated", 
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);


            // none of this can be Expressions because CompileToMethod requires a static target method for some dumbass reason

            #region Parse base object structure
            var baseChanged = type.GetMethod("Changed", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChanged != null && !baseChanged.IsVirtual) baseChanged = null; // limit this to just the one thing

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

                foreach (var member in structure)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldstr, member.Name); // TODO: make this behave with annotations
                    EmitSerializeMember(il, member);
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
                    EmitDeserializeMember(il, member, nextLabel, il => il.Emit(OpCodes.Ldloc_S, valueLocal), locals);
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

        private static void EmitNumberConvertTo(ILGenerator il, Type to, Type from)
        { // WARNING: THIS USES THE NO-OVERFLOW OPCODES
            if (to == typeof(IntPtr)) il.Emit(OpCodes.Conv_I);
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
        private static void EmitSerializeMember(ILGenerator il, SerializedMemberInfo member)
        {
            // TODO: impl
            il.Emit(OpCodes.Ldnull);
        }
        #endregion

        #region Deserialize

        private static Type GetExpectedValueTypeForType(Type valT)
        {
            if (typeof(Value).IsAssignableFrom(valT)) // this is a Value subtype
                return valT;
            if (valT == typeof(string)) return typeof(Text);
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

        // top of stack is the Value to deserialize; the type will be as returned from GetExpectedValueTypeForType
        // after, top of stack will be thing to write to field
        private static void EmitDeserializeValue(ILGenerator il, Type targetType, Label nextLabel)
        {
            if (typeof(Value).IsAssignableFrom(targetType)) return; // do nothing

            var expected = GetExpectedValueTypeForType(targetType);
            if (expected == typeof(Text))
            {
                var getter = expected.GetProperty(nameof(Text.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
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
            else // TODO: support converters
            {
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
        }

        // emit takes the value being deserialized, logs on error, leaves nothing on stack
        private static void EmitDeserializeMember(ILGenerator il, SerializedMemberInfo member, Label nextLabel, Action<ILGenerator> getValue, List<LocalBuilder> locals)
        {
            var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

            var implLabel = il.DefineLabel();
            var passedTypeCheck = il.DefineLabel();
            var expectType = GetExpectedValueTypeForType(member.Type);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brtrue_S, implLabel); // null check

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

            il.Emit(OpCodes.Pop);
            EmitLogError(il, $"Unexpected type deserializing {member.Name}", tailcall: false,
                expected: il => EmitTypeof(il, expectType), found: il =>
                {
                    getValue(il);
                    il.Emit(OpCodes.Callvirt, Object_GetType);
                });
            il.Emit(OpCodes.Br, nextLabel);

            il.MarkLabel(passedTypeCheck);

            var local = GetLocal(member.Type);
            EmitDeserializeValue(il, member.Type, nextLabel);
            il.Emit(OpCodes.Stloc, local);
            EmitStore(il => il.Emit(OpCodes.Ldloc, local));
        }
        #endregion
    }
}
