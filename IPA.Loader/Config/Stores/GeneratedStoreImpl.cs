using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
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
using IPA.Utilities;
using System.ComponentModel;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    /// <summary>
    /// A class providing an extension for <see cref="Config"/> to make it easy to use generated
    /// config stores.
    /// </summary>
    public static class GeneratedStore
    {
        /// <summary>
        /// The name of the assembly that internals must be visible to to allow internal protection.
        /// </summary>
        public const string AssemblyVisibilityTarget = GeneratedStoreImpl.GeneratedAssemblyName;

        /// <summary>
        /// Creates a generated <see cref="IConfigStore"/> of type <typeparamref name="T"/>, registers it to
        /// the <see cref="Config"/> object, and returns it. This also forces a synchronous config load via
        /// <see cref="Config.LoadSync"/> if <paramref name="loadSync"/> is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <typeparamref name="T"/> must be a public non-<see langword="sealed"/> class.
        /// It can also be internal, but in that case, then your assembly must have the following attribute
        /// to allow the generated code to reference it.
        /// <code lang="csharp">
        /// [assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]
        /// </code>
        /// </para>
        /// <para>
        /// Only fields and properties that are public or protected will be considered, and only properties
        /// where both the getter and setter are public or protected are considered. Any fields or properties
        /// with an <see cref="IgnoreAttribute"/> applied to them are also ignored. Having properties be <see langword="virtual"/> is not strictly
        /// necessary, however it allows the generated type to keep track of changes and lock around them so that the config will auto-save.
        /// </para>
        /// <para>
        /// All of the attributes in the <see cref="Attributes"/> namespace are handled as described by them.
        /// </para>
        /// <para>
        /// If the <typeparamref name="T"/> declares a public or protected, <see langword="virtual"/>
        /// method <c>Changed()</c>, then that method may be called to artificially signal to the runtime that the content of the object 
        /// has changed. That method will also be called after the write locks are released when a property is set anywhere in the owning
        /// tree. This will only be called on the outermost generated object of the config structure, even if the change being signaled
        /// is somewhere deep into the tree.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>OnReload()</c>, which will be called on the filesystem reader thread after the object has been repopulated with new data 
        /// values. It will be called <i>after</i> the write lock for this object is released. This will only be called on the outermost generated
        /// object of the config structure.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>CopyFrom(ConfigType)</c> (the first parameter is the type it is defined on), which may be called to copy the properties from
        /// another object of its type easily, and more importantly, as only one change. Its body will be executed after the values have been copied.
        /// </para>
        /// <para>
        /// Similarly, <typeparamref name="T"/> can declare a public or protected, <see langword="virtual"/> 
        /// method <c>ChangeTransaction()</c> returning <see cref="IDisposable"/>, which may be called to get an object representing a transactional
        /// change. This may be used to change a lot of properties at once without triggering a save multiple times. Ideally, this is used in a
        /// <see langword="using"/> block or declaration. The <see cref="IDisposable"/> returned from your implementation will have its
        /// <see cref="IDisposable.Dispose"/> called <i>after</i> <c>Changed()</c> is called, but <i>before</i> the write lock is released.
        /// Unless you have a very good reason to use the nested <see cref="IDisposable"/>, avoid it.
        /// </para>
        /// <para>
        /// If <typeparamref name="T"/> is marked with <see cref="NotifyPropertyChangesAttribute"/>, the resulting object will implement
        /// <see cref="INotifyPropertyChanged"/>. Similarly, if <typeparamref name="T"/> implements <see cref="INotifyPropertyChanged"/>,
        /// the resulting object will implement it and notify it too.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <param name="cfg">the <see cref="Config"/> to register to</param>
        /// <param name="loadSync">whether to synchronously load the content, or trigger an async load</param>
        /// <returns>a generated instance of <typeparamref name="T"/> as a special <see cref="IConfigStore"/></returns>
        public static T Generated<T>(this Config cfg, bool loadSync = true) where T : class
        {
            var ret = GeneratedStoreImpl.Create<T>();
            cfg.SetStore(ret as IConfigStore);
            if (loadSync)
                cfg.LoadSync();
            else
                cfg.LoadAsync();

            return ret;
        }

        /// <summary>
        /// Creates a generated store outside of the context of the config system.
        /// </summary>
        /// <remarks>
        /// See <see cref="Generated{T}(Config, bool)"/> for more information about how it behaves.
        /// </remarks>
        /// <typeparam name="T">the type to wrap</typeparam>
        /// <returns>a generated instance of <typeparamref name="T"/> implementing functionality described by <see cref="Generated{T}(Config, bool)"/></returns>
        /// <seealso cref="Generated{T}(Config, bool)"/>
        public static T Create<T>() where T : class
            => GeneratedStoreImpl.Create<T>();
    }

    internal static class GeneratedStoreImpl
    {
        internal interface IGeneratedStore
        {
            Type Type { get; }
            IGeneratedStore Parent { get; }
            Impl Impl { get; }
            void OnReload();

            void Changed();
            IDisposable ChangeTransaction();

            Value Serialize();
            void Deserialize(Value val);
        }
        internal interface IGeneratedStore<T> : IGeneratedStore where T : class
        {
            void CopyFrom(T source, bool useLock);
        }
        internal interface IGeneratedPropertyChanged : INotifyPropertyChanged
        { 
            PropertyChangedEventHandler PropertyChangedEvent { get; }
        }

        internal class Impl : IConfigStore
        {
            private readonly IGeneratedStore generated;
            private bool inChangeTransaction = false;
            //private bool changedInTransaction = false;

            internal static ConstructorInfo Ctor = typeof(Impl).GetConstructor(new[] { typeof(IGeneratedStore) });
            public Impl(IGeneratedStore store) => generated = store;

            private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
            public WaitHandle SyncObject => resetEvent;
            public static WaitHandle ImplGetSyncObject(IGeneratedStore s) => FindImpl(s).SyncObject;
            internal static MethodInfo ImplGetSyncObjectMethod = typeof(Impl).GetMethod(nameof(ImplGetSyncObject));

            public ReaderWriterLockSlim WriteSyncObject { get; } = new ReaderWriterLockSlim();
            public static ReaderWriterLockSlim ImplGetWriteSyncObject(IGeneratedStore s) => FindImpl(s)?.WriteSyncObject;
            internal static MethodInfo ImplGetWriteSyncObjectMethod = typeof(Impl).GetMethod(nameof(ImplGetWriteSyncObject));

            internal static MethodInfo ImplSignalChangedMethod = typeof(Impl).GetMethod(nameof(ImplSignalChanged));
            public static void ImplSignalChanged(IGeneratedStore s) => FindImpl(s).SignalChanged();
            public void SignalChanged() => resetEvent.Set();

            internal static MethodInfo ImplInvokeChangedMethod = typeof(Impl).GetMethod(nameof(ImplInvokeChanged));
            public static void ImplInvokeChanged(IGeneratedStore s) => FindImpl(s).InvokeChanged();
            public void InvokeChanged() => generated.Changed();

            internal static MethodInfo ImplTakeReadMethod = typeof(Impl).GetMethod(nameof(ImplTakeRead));
            public static void ImplTakeRead(IGeneratedStore s) => FindImpl(s).TakeRead();
            public void TakeRead()
            {
                if (!WriteSyncObject.IsWriteLockHeld)
                    WriteSyncObject.EnterReadLock();
            }

            internal static MethodInfo ImplReleaseReadMethod = typeof(Impl).GetMethod(nameof(ImplReleaseRead));
            public static void ImplReleaseRead(IGeneratedStore s) => FindImpl(s).ReleaseRead();
            public void ReleaseRead()
            {
                if (!WriteSyncObject.IsWriteLockHeld)
                    WriteSyncObject.ExitReadLock();
            }

            internal static MethodInfo ImplTakeWriteMethod = typeof(Impl).GetMethod(nameof(ImplTakeWrite));
            public static void ImplTakeWrite(IGeneratedStore s) => FindImpl(s).TakeWrite();
            public void TakeWrite() => WriteSyncObject.EnterWriteLock();

            internal static MethodInfo ImplReleaseWriteMethod = typeof(Impl).GetMethod(nameof(ImplReleaseWrite));
            public static void ImplReleaseWrite(IGeneratedStore s) => FindImpl(s).ReleaseWrite();
            public void ReleaseWrite() => WriteSyncObject.ExitWriteLock();

            internal static MethodInfo ImplChangeTransactionMethod = typeof(Impl).GetMethod(nameof(ImplChangeTransaction));
            public static IDisposable ImplChangeTransaction(IGeneratedStore s, IDisposable nest) => FindImpl(s).ChangeTransaction(nest);
            // TODO: improve trasactionals so they don't always save in every case
            public IDisposable ChangeTransaction(IDisposable nest, bool takeWrite = true)
                => GetFreeTransaction().InitWith(this, !inChangeTransaction, nest, takeWrite && !WriteSyncObject.IsWriteLockHeld);

            private ChangeTransactionObj GetFreeTransaction()
                => freeTransactionObjs.Count > 0 ? freeTransactionObjs.Pop()
                                                 : new ChangeTransactionObj();
            // TODO: maybe sometimes clean this?
            private static readonly Stack<ChangeTransactionObj> freeTransactionObjs = new Stack<ChangeTransactionObj>();

            private sealed class ChangeTransactionObj : IDisposable
            {
                private struct Data
                {
                    public readonly Impl impl;
                    public readonly bool owns;
                    public readonly bool ownsWrite;
                    public readonly IDisposable nested;

                    public Data(Impl impl, bool owning, bool takeWrite, IDisposable nest)
                    {
                        this.impl = impl; owns = owning; ownsWrite = takeWrite; nested = nest;
                    }
                }
                private Data data;

                public ChangeTransactionObj InitWith(Impl impl, bool owning, IDisposable nest, bool takeWrite)
                {
                    data = new Data(impl, owning, takeWrite, nest);

                    if (data.owns)
                        impl.inChangeTransaction = true;
                    if (data.ownsWrite)
                        impl.TakeWrite();

                    return this;
                }

                public void Dispose() => Dispose(true);
                private void Dispose(bool addToStore)
                {
                    if (data.owns)
                    {
                        data.impl.inChangeTransaction = false;
                        data.impl.InvokeChanged();
                    }
                    data.nested?.Dispose();
                    if (data.ownsWrite)
                        data.impl.ReleaseWrite();

                    if (addToStore)
                        freeTransactionObjs.Push(this);
                }

                ~ChangeTransactionObj() => Dispose(false);
            }

            public static Impl FindImpl(IGeneratedStore store)
            {
                while (store?.Parent != null) store = store.Parent; // walk to the top of the tree
                return store?.Impl;
            }


            internal static MethodInfo ImplReadFromMethod = typeof(Impl).GetMethod(nameof(ImplReadFrom));
            public static void ImplReadFrom(IGeneratedStore s, ConfigProvider provider) => FindImpl(s).ReadFrom(provider);
            public void ReadFrom(ConfigProvider provider)
            {
                var values = provider.Load();
                Logger.config.Debug("Generated impl ReadFrom");
                Logger.config.Debug($"Read {values}");
                generated.Deserialize(values);

                using var transaction = generated.ChangeTransaction();
                generated.OnReload();
            }

            internal static MethodInfo ImplWriteToMethod = typeof(Impl).GetMethod(nameof(ImplWriteTo));
            public static void ImplWriteTo(IGeneratedStore s, ConfigProvider provider) => FindImpl(s).WriteTo(provider);
            public void WriteTo(ConfigProvider provider)
            {
                var values = generated.Serialize();
                Logger.config.Debug("Generated impl WriteTo");
                Logger.config.Debug($"Serialized {values}");
                provider.Store(values);
            }
        }

        private static readonly Dictionary<Type, (GeneratedStoreCreator ctor, Type type)> generatedCreators = new Dictionary<Type, (GeneratedStoreCreator ctor, Type type)>();

        public static T Create<T>() where T : class => (T)Create(typeof(T));

        public static IConfigStore Create(Type type) => Create(type, null);

        private static readonly MethodInfo CreateGParent = 
            typeof(GeneratedStoreImpl).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static, null, 
                                             CallingConventions.Any, new[] { typeof(IGeneratedStore) }, Array.Empty<ParameterModifier>());
        internal static T Create<T>(IGeneratedStore parent) where T : class => (T)Create(typeof(T), parent);

        private static IConfigStore Create(Type type, IGeneratedStore parent)
            => GetCreator(type)(parent);

        internal static GeneratedStoreCreator GetCreator(Type t)
        {
            if (generatedCreators.TryGetValue(t, out var gen))
                return gen.ctor;
            else
            {
                gen = MakeCreator(t);
                generatedCreators.Add(t, gen);
                return gen.ctor;
            }
        }

        internal static Type GetGeneratedType(Type t)
        {
            if (generatedCreators.TryGetValue(t, out var gen))
                return gen.type;
            else
            {
                gen = MakeCreator(t);
                generatedCreators.Add(t, gen);
                return gen.type;
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

        private class SerializedMemberInfo
        {
            public string Name;
            public MemberInfo Member;
            public Type Type;
            public bool AllowNull;
            public bool IsVirtual;
            public bool IsField;
            public bool IsNullable; // signifies whether this is a Nullable<T>

            public bool HasConverter;
            public bool IsGenericConverter; // used so we can call directly to the generic version if it is
            public Type Converter;
            public Type ConverterBase;
            public Type ConverterTarget;
            public FieldInfo ConverterField;

            // invalid for objects with IsNullabe false
            public Type NullableWrappedType => Nullable.GetUnderlyingType(Type);
            // invalid for objects with IsNullabe false
            public PropertyInfo Nullable_HasValue => Type.GetProperty(nameof(Nullable<int>.HasValue));
            // invalid for objects with IsNullabe false
            public PropertyInfo Nullable_Value => Type.GetProperty(nameof(Nullable<int>.Value));
            // invalid for objects with IsNullabe false
            public ConstructorInfo Nullable_Construct => Type.GetConstructor(new[] { NullableWrappedType });
        }

        internal delegate IConfigStore GeneratedStoreCreator(IGeneratedStore parent);

        private static bool IsMethodInvalid(MethodInfo m, Type ret) => !m.IsVirtual || m.ReturnType != ret;
        private static (GeneratedStoreCreator ctor, Type type) MakeCreator(Type type)
        { // note that this does not and should not use converters by default for everything
            if (!type.IsClass) throw new ArgumentException("Config type is not a class");

            var baseCtor = type.GetConstructor(Type.EmptyTypes); // get a default constructor
            if (baseCtor == null)
                throw new ArgumentException("Config type does not have a public parameterless constructor");

            #region Parse base object structure
            const BindingFlags overrideMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var baseChanged = type.GetMethod("Changed", overrideMemberFlags, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChanged != null && IsMethodInvalid(baseChanged, typeof(void))) baseChanged = null;

            var baseOnReload = type.GetMethod("OnReload", overrideMemberFlags, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseOnReload != null && IsMethodInvalid(baseOnReload, typeof(void))) baseOnReload = null;

            var baseCopyFrom = type.GetMethod("CopyFrom", overrideMemberFlags, null, new[] { type }, Array.Empty<ParameterModifier>());
            if (baseCopyFrom != null && IsMethodInvalid(baseCopyFrom, typeof(void))) baseCopyFrom = null;

            var baseChangeTransaction = type.GetMethod("ChangeTransaction", overrideMemberFlags, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChangeTransaction != null && IsMethodInvalid(baseChangeTransaction, typeof(IDisposable))) baseChangeTransaction = null;

            var isINotifyPropertyChanged = type.FindInterfaces((i, t) => i == (Type)t, typeof(INotifyPropertyChanged)).Length != 0;
            var hasNotifyAttribute = type.GetCustomAttribute<NotifyPropertyChangesAttribute>() != null;

            var structure = new List<SerializedMemberInfo>();

            bool ProcessAttributesFor(ref SerializedMemberInfo member)
            {
                var attrs = member.Member.GetCustomAttributes(true);
                var ignores = attrs.Select(o => o as IgnoreAttribute).NonNull();
                if (ignores.Any() || typeof(Delegate).IsAssignableFrom(member.Type))
                { // we ignore delegates completely because there fundamentally is not a good way to serialize them
                    return false;
                }

                var nonNullables = attrs.Select(o => o as NonNullableAttribute).NonNull();

                member.Name = member.Member.Name;
                member.IsNullable = member.Type.IsGenericType
                          && member.Type.GetGenericTypeDefinition() == typeof(Nullable<>);
                member.AllowNull = !nonNullables.Any() && (!member.Type.IsValueType || member.IsNullable);

                var nameAttr = attrs.Select(o => o as SerializedNameAttribute).NonNull().FirstOrDefault();
                if (nameAttr != null)
                    member.Name = nameAttr.Name;

                member.HasConverter = false;
                var converterAttr = attrs.Select(o => o as UseConverterAttribute).NonNull().FirstOrDefault();
                if (converterAttr != null)
                {
                    member.Converter = converterAttr.ConverterType;
                    member.IsGenericConverter = converterAttr.IsGenericConverter;

                    if (member.Converter.GetConstructor(Type.EmptyTypes) == null)
                    {
                        Logger.config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not default-constructible");
                        goto endConverterAttr; // is there a better control flow structure to do this?
                    }

                    if (member.Converter.ContainsGenericParameters)
                    {
                        Logger.config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that has unfilled type parameters");
                        goto endConverterAttr;
                    }

                    if (member.Converter.IsInterface || member.Converter.IsAbstract)
                    {
                        Logger.config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not constructible");
                        goto endConverterAttr;
                    }

                    var targetType = converterAttr.ConverterTargetType;
                    if (!member.IsGenericConverter)
                    {
                        try
                        {
                            var conv = Activator.CreateInstance(converterAttr.ConverterType) as IValueConverter;
                            targetType = conv.Type;
                        }
                        catch
                        {
                            Logger.config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter who's target type could not be determined");
                            goto endConverterAttr;
                        }
                    }
                    if (targetType != member.Type)
                    {
                        Logger.config.Warn($"{type.FullName}'s member {member.Member.Name} requests a converter that is not of the member's type");
                        goto endConverterAttr;
                    }

                    member.ConverterTarget = targetType;
                    if (member.IsGenericConverter)
                        member.ConverterBase = typeof(ValueConverter<>).MakeGenericType(targetType);
                    else
                        member.ConverterBase = typeof(IValueConverter);

                    member.HasConverter = true;
                }
            endConverterAttr:

                return true;
            }

            // only looks at public/protected properties
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.GetSetMethod(true)?.IsPrivate ?? true)
                { // we enter this block if the setter is inacessible or doesn't exist
                    continue; // ignore props without setter
                }
                if (prop.GetGetMethod(true)?.IsPrivate ?? true)
                { // we enter this block if the getter is inacessible or doesn't exist
                    continue; // ignore props without getter
                }

                var smi = new SerializedMemberInfo
                {
                    Member = prop,
                    IsVirtual = (prop.GetGetMethod(true)?.IsVirtual ?? false) ||
                                (prop.GetSetMethod(true)?.IsVirtual ?? false),
                    IsField = false,
                    Type = prop.PropertyType
                };

                if (!ProcessAttributesFor(ref smi)) continue;

                structure.Add(smi);
            }

            // only look at public/protected fields
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsPrivate) continue;

                var smi = new SerializedMemberInfo
                {
                    Member = field,
                    IsVirtual = false,
                    IsField = true,
                    Type = field.FieldType
                };

                if (!ProcessAttributesFor(ref smi)) continue;

                structure.Add(smi);
            }
            #endregion

            var typeBuilder = Module.DefineType($"{type.FullName}<Generated>", 
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);

            #region Converter fields
            var uniqueConverterTypes = structure.Where(m => m.HasConverter).Select(m => m.Converter).Distinct().ToArray();
            var converterFields = new Dictionary<Type, FieldInfo>(uniqueConverterTypes.Length);

            foreach (var convType in uniqueConverterTypes)
            {
                var field = typeBuilder.DefineField($"<converter>_{convType}", convType, 
                    FieldAttributes.Private | FieldAttributes.InitOnly | FieldAttributes.Static);
                converterFields.Add(convType, field);

                foreach (var member in structure.Where(m => m.HasConverter && m.Converter == convType))
                    member.ConverterField = field;
            }
            #endregion

            #region Static constructor
            var cctor = typeBuilder.DefineConstructor(MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
            {
                var il = cctor.GetILGenerator();
               
                foreach (var kvp in converterFields)
                {
                    var typeCtor = kvp.Key.GetConstructor(Type.EmptyTypes);
                    il.Emit(OpCodes.Newobj, typeCtor);
                    il.Emit(OpCodes.Stsfld, kvp.Value);
                }

                il.Emit(OpCodes.Ret);
            }
            #endregion

            #region Constructor
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
                EmitTypeof(il, type);
                il.Emit(OpCodes.Stfld, typeField);

                var noImplLabel = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue, noImplLabel);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Newobj, Impl.Ctor);
                il.Emit(OpCodes.Stfld, implField);
                il.MarkLabel(noImplLabel);

                var GetLocal = MakeGetLocal(il);

                foreach (var member in structure)
                {
                    EmitStore(il, member, il =>
                    {
                        EmitLoad(il, member); // load the member
                        EmitCorrectMember(il, member, false, true, GetLocal); // correct it
                    });
                }

                il.Emit(OpCodes.Pop);

                il.Emit(OpCodes.Ret);
                
            }
            #endregion

            const MethodAttributes propertyMethodAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            const MethodAttributes virtualPropertyMethodAttr = propertyMethodAttr | MethodAttributes.Virtual | MethodAttributes.Final;
            const MethodAttributes virtualMemberMethod = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final;

            #region INotifyPropertyChanged
            MethodBuilder notifyChanged = null;
            if (isINotifyPropertyChanged || hasNotifyAttribute)
            {
                var INotifyPropertyChanged_t = typeof(INotifyPropertyChanged);
                typeBuilder.AddInterfaceImplementation(INotifyPropertyChanged_t);

                var INotifyPropertyChanged_PropertyChanged =
                    INotifyPropertyChanged_t.GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

                var PropertyChangedEventHandler_t = typeof(PropertyChangedEventHandler);
                var PropertyChangedEventHander_Invoke = PropertyChangedEventHandler_t.GetMethod(nameof(PropertyChangedEventHandler.Invoke));

                var PropertyChangedEventArgs_t = typeof(PropertyChangedEventArgs);
                var PropertyChangedEventArgs_ctor = PropertyChangedEventArgs_t.GetConstructor(new[] { typeof(string) });

                var Delegate_t = typeof(Delegate);
                var Delegate_Combine = Delegate_t.GetMethod(nameof(Delegate.Combine), BindingFlags.Static | BindingFlags.Public, null,
                                                            new[] { Delegate_t, Delegate_t }, Array.Empty<ParameterModifier>());
                var Delegate_Remove = Delegate_t.GetMethod(nameof(Delegate.Remove), BindingFlags.Static | BindingFlags.Public, null,
                                                            new[] { Delegate_t, Delegate_t }, Array.Empty<ParameterModifier>());

                var CompareExchange = typeof(Interlocked).GetMethods()
                    .Where(m => m.Name == nameof(Interlocked.CompareExchange))
                    .Where(m => m.ContainsGenericParameters)
                    .Where(m => m.GetParameters().Length == 3).First()
                        .MakeGenericMethod(PropertyChangedEventHandler_t);

                var basePropChangedEvent = type.GetEvents()
                    .Where(e => e.AddMethod.GetBaseDefinition().DeclaringType == INotifyPropertyChanged_t)
                    .FirstOrDefault();
                var basePropChangedAdd = basePropChangedEvent?.AddMethod;
                var basePropChangedRemove = basePropChangedEvent?.RemoveMethod;

                var PropertyChanged_backing = typeBuilder.DefineField("<event>PropertyChanged", PropertyChangedEventHandler_t, FieldAttributes.Private);

                var add_PropertyChanged = typeBuilder.DefineMethod("<add>PropertyChanged",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual,
                    null, new[] { PropertyChangedEventHandler_t });
                typeBuilder.DefineMethodOverride(add_PropertyChanged, INotifyPropertyChanged_PropertyChanged.GetAddMethod());
                if (basePropChangedAdd != null)
                    typeBuilder.DefineMethodOverride(add_PropertyChanged, basePropChangedAdd);

                {
                    var il = add_PropertyChanged.GetILGenerator();

                    var loopLabel = il.DefineLabel();
                    var delTemp = il.DeclareLocal(PropertyChangedEventHandler_t);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, PropertyChanged_backing);

                    il.MarkLabel(loopLabel);
                    il.Emit(OpCodes.Stloc, delTemp);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, PropertyChanged_backing);

                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, Delegate_Combine);
                    il.Emit(OpCodes.Castclass, PropertyChangedEventHandler_t);

                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Call, CompareExchange);

                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Bne_Un_S, loopLabel);

                    il.Emit(OpCodes.Ret);
                }

                var remove_PropertyChanged = typeBuilder.DefineMethod("<remove>PropertyChanged",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual,
                    null, new[] { PropertyChangedEventHandler_t });
                typeBuilder.DefineMethodOverride(remove_PropertyChanged, INotifyPropertyChanged_PropertyChanged.GetRemoveMethod());
                if (basePropChangedRemove != null)
                    typeBuilder.DefineMethodOverride(remove_PropertyChanged, basePropChangedRemove);

                {
                    var il = remove_PropertyChanged.GetILGenerator();

                    var loopLabel = il.DefineLabel();
                    var delTemp = il.DeclareLocal(PropertyChangedEventHandler_t);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, PropertyChanged_backing);

                    il.MarkLabel(loopLabel);
                    il.Emit(OpCodes.Stloc, delTemp);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, PropertyChanged_backing);

                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, Delegate_Remove);
                    il.Emit(OpCodes.Castclass, PropertyChangedEventHandler_t);

                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Call, CompareExchange);

                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldloc, delTemp);
                    il.Emit(OpCodes.Bne_Un_S, loopLabel);

                    il.Emit(OpCodes.Ret);
                }

                var PropertyChanged_event = typeBuilder.DefineEvent(nameof(INotifyPropertyChanged.PropertyChanged), EventAttributes.None, PropertyChangedEventHandler_t);
                PropertyChanged_event.SetAddOnMethod(add_PropertyChanged);
                PropertyChanged_event.SetRemoveOnMethod(remove_PropertyChanged);

                notifyChanged = typeBuilder.DefineMethod("<>NotifyChanged",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final, null, new[] { typeof(string) });

                {
                    var il = notifyChanged.GetILGenerator();

                    var invokeNonNull = il.DefineLabel();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, PropertyChanged_backing);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brtrue, invokeNonNull);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ret);

                    il.MarkLabel(invokeNonNull);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Newobj, PropertyChangedEventArgs_ctor);
                    il.Emit(OpCodes.Call, PropertyChangedEventHander_Invoke);
                    il.Emit(OpCodes.Ret);
                }
            }
            #endregion

            #region IGeneratedStore
            typeBuilder.AddInterfaceImplementation(typeof(IGeneratedStore));

            var IGeneratedStore_t = typeof(IGeneratedStore);
            var IGeneratedStore_GetImpl = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Impl)).GetGetMethod();
            var IGeneratedStore_GetType = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Type)).GetGetMethod();
            var IGeneratedStore_GetParent = IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Parent)).GetGetMethod();
            var IGeneratedStore_Serialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Serialize));
            var IGeneratedStore_Deserialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Deserialize));
            var IGeneratedStore_OnReload = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.OnReload));
            var IGeneratedStore_Changed = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Changed));
            var IGeneratedStore_ChangeTransaction = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.ChangeTransaction));

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
            #region IGeneratedStore.Serialize
            var serializeGen = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.Serialize)}", virtualPropertyMethodAttr, IGeneratedStore_Serialize.ReturnType, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(serializeGen, IGeneratedStore_Serialize);

            { // this is non-locking because the only code that will call this will already own the correct lock
                var il = serializeGen.GetILGenerator();

                var Map_Add = typeof(Map).GetMethod(nameof(Map.Add));
                var mapLocal = il.DeclareLocal(typeof(Map));

                var GetLocal = MakeGetLocal(il);
                var valLocal = GetLocal(typeof(Value));

                il.Emit(OpCodes.Call, typeof(Value).GetMethod(nameof(Value.Map)));
                il.Emit(OpCodes.Stloc, mapLocal);

                foreach (var member in structure)
                {
                    EmitSerializeMember(il, member, GetLocal);
                    il.Emit(OpCodes.Stloc, valLocal);
                    il.Emit(OpCodes.Ldloc, mapLocal);
                    il.Emit(OpCodes.Ldstr, member.Name);
                    il.Emit(OpCodes.Ldloc, valLocal);
                    il.Emit(OpCodes.Call, Map_Add);
                }

                il.Emit(OpCodes.Ldloc, mapLocal);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Deserialize
            var deserializeGen = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.Deserialize)}", virtualPropertyMethodAttr, null,
                new[] { IGeneratedStore_Deserialize.GetParameters()[0].ParameterType });
            typeBuilder.DefineMethodOverride(deserializeGen, IGeneratedStore_Deserialize);

            { // this is non-locking because the only code that will call this will already own the correct lock
                var il = deserializeGen.GetILGenerator();

                var Map_t = typeof(Map);
                var Map_TryGetValue = Map_t.GetMethod(nameof(Map.TryGetValue));
                var Object_GetType = typeof(object).GetMethod(nameof(Object.GetType));

                var valueLocal = il.DeclareLocal(typeof(Value));
                var mapLocal = il.DeclareLocal(typeof(Map));

                var nonNull = il.DefineLabel();

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue, nonNull);

                EmitLogError(il, "Attempting to deserialize null", tailcall: true);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(nonNull);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Isinst, Map_t);
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Stloc, mapLocal);
                var notMapError = il.DefineLabel();
                il.Emit(OpCodes.Brtrue, notMapError);
                // handle error
                EmitLogError(il, $"Invalid root for deserializing {type.FullName}", tailcall: true,
                    expected: il => EmitTypeof(il, Map_t), found: il =>
                    {
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Callvirt, Object_GetType);
                    });
                il.Emit(OpCodes.Ret);

                var nextLabel = notMapError;

                var GetLocal = MakeGetLocal(il);

                // head of stack is Map instance
                foreach (var member in structure)
                {
                    il.MarkLabel(nextLabel);
                    nextLabel = il.DefineLabel();
                    var endErrorLabel = il.DefineLabel();

                    il.Emit(OpCodes.Ldloc, mapLocal);
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

                if (notifyChanged != null)
                {
                    foreach (var member in structure)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }
                }

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
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplGetSyncObjectMethod);
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
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplGetWriteSyncObjectMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IConfigStore.WriteTo
            var writeTo = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.WriteTo)}", virtualMemberMethod, null, new[] { typeof(ConfigProvider) });
            typeBuilder.DefineMethodOverride(writeTo, IConfigStore_WriteTo);

            {
                var il = writeTo.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplWriteToMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IConfigStore.ReadFrom
            var readFrom = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.ReadFrom)}", virtualMemberMethod, null, new[] { typeof(ConfigProvider) });
            typeBuilder.DefineMethodOverride(readFrom, IConfigStore_ReadFrom);

            {
                var il = readFrom.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplReadFromMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #endregion

            #region Changed
            var coreChanged = typeBuilder.DefineMethod(
                "<>Changed",
                virtualMemberMethod,
                null, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(coreChanged, IGeneratedStore_Changed);
            if (baseChanged != null)
                typeBuilder.DefineMethodOverride(coreChanged, baseChanged);

            {
                var il = coreChanged.GetILGenerator();

                if (baseChanged != null)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, baseChanged); // call base
                }

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplSignalChangedMethod);
                il.Emit(OpCodes.Ret); // simply call our impl's SignalChanged method and return
            }
            #endregion

            #region ChangeTransaction
            var coreChangeTransaction = typeBuilder.DefineMethod(
                "<>ChangeTransaction",
                virtualMemberMethod,
                typeof(IDisposable), Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(coreChangeTransaction, IGeneratedStore_ChangeTransaction);
            if (baseChangeTransaction != null)
                typeBuilder.DefineMethodOverride(coreChangeTransaction, baseChangeTransaction);

            {
                var il = coreChangeTransaction.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                if (baseChangeTransaction != null)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, baseChangeTransaction);
                }
                else
                    il.Emit(OpCodes.Ldnull);

                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplChangeTransactionMethod);
                il.Emit(OpCodes.Ret);
            }
            #endregion

            #region IGeneratedStore<T>
            var IGeneratedStore_T_t = typeof(IGeneratedStore<>).MakeGenericType(type);
            typeBuilder.AddInterfaceImplementation(IGeneratedStore_T_t);

            var IGeneratedStore_T_CopyFrom = IGeneratedStore_T_t.GetMethod(nameof(IGeneratedStore<Config>.CopyFrom));

            #region IGeneratedStore<T>.CopyFrom
            var copyFrom = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore<Config>.CopyFrom)}", virtualMemberMethod, null, new[] { type, typeof(bool) });
            typeBuilder.DefineMethodOverride(copyFrom, IGeneratedStore_T_CopyFrom);

            {
                var il = copyFrom.GetILGenerator();

                var transactionLocal = il.DeclareLocal(IDisposable_t);

                var startLock = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Brfalse, startLock);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, coreChangeTransaction); // take the write lock
                il.Emit(OpCodes.Stloc, transactionLocal);
                il.MarkLabel(startLock);

                var GetLocal = MakeGetLocal(il);

                foreach (var member in structure)
                {
                    il.BeginExceptionBlock();

                    EmitStore(il, member, il =>
                    {
                        EmitLoad(il, member, il => il.Emit(OpCodes.Ldarg_1));
                        EmitCorrectMember(il, member, false, false, GetLocal);
                    });

                    il.BeginCatchBlock(typeof(Exception));

                    EmitWarnException(il, $"Error while copying from member {member.Name}");

                    il.EndExceptionBlock();
                }

                if (notifyChanged != null)
                {
                    foreach (var member in structure)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }
                }

                var endLock = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Brfalse, endLock);
                il.Emit(OpCodes.Ldloc, transactionLocal);
                il.Emit(OpCodes.Callvirt, IDisposable_Dispose);
                il.MarkLabel(endLock);
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #endregion

            #region base.CopyFrom
            if (baseCopyFrom != null)
            {
                var pubCopyFrom = typeBuilder.DefineMethod(
                    baseCopyFrom.Name,
                    virtualMemberMethod,
                    null, new[] { type });
                typeBuilder.DefineMethodOverride(pubCopyFrom, baseCopyFrom);

                {
                    var il = pubCopyFrom.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, coreChangeTransaction);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, copyFrom); // call internal

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, baseCopyFrom); // call base

                    il.Emit(OpCodes.Tailcall);
                    il.Emit(OpCodes.Callvirt, IDisposable_Dispose); // dispose transaction (which calls changed)
                    il.Emit(OpCodes.Ret);
                }
            }
            #endregion

            #region Members
            foreach (var member in structure.Where(m => m.IsVirtual))
            { // IsVirtual implies !IsField
                var prop = member.Member as PropertyInfo;
                var get = prop.GetGetMethod(true);
                var set = prop.GetSetMethod(true);

                var propBuilder = typeBuilder.DefineProperty($"{member.Name}#", PropertyAttributes.None, member.Type, null);
                var propGet = typeBuilder.DefineMethod($"<g>{propBuilder.Name}", virtualPropertyMethodAttr, member.Type, Type.EmptyTypes);
                propBuilder.SetGetMethod(propGet);
                typeBuilder.DefineMethodOverride(propGet, get);

                {
                    var il = propGet.GetILGenerator();

                    var local = il.DeclareLocal(member.Type);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, Impl.ImplTakeReadMethod); // take the read lock

                    il.BeginExceptionBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, get); // call base getter
                    il.Emit(OpCodes.Stloc, local);

                    il.BeginFinallyBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, Impl.ImplReleaseReadMethod); // release the read lock

                    il.EndExceptionBlock();

                    il.Emit(OpCodes.Ldloc, local);
                    il.Emit(OpCodes.Ret);
                }

                var propSet = typeBuilder.DefineMethod($"<s>{propBuilder.Name}", virtualPropertyMethodAttr, null, new[] { member.Type });
                propBuilder.SetSetMethod(propSet);
                typeBuilder.DefineMethodOverride(propSet, set);

                { // TODO: decide if i want to correct the value before or after i take the write lock
                    var il = propSet.GetILGenerator();

                    var transactionLocal = il.DeclareLocal(IDisposable_t);
                    var GetLocal = MakeGetLocal(il);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, coreChangeTransaction); // take the write lock
                    il.Emit(OpCodes.Stloc, transactionLocal);

                    il.BeginExceptionBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    EmitCorrectMember(il, member, false, false, GetLocal);
                    il.Emit(OpCodes.Call, set);

                    il.BeginFinallyBlock();

                    il.Emit(OpCodes.Ldloc, transactionLocal);
                    il.Emit(OpCodes.Callvirt, IDisposable_Dispose);

                    il.EndExceptionBlock();

                    if (notifyChanged != null)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }
                    il.Emit(OpCodes.Ret);
                }

            }
            #endregion

            var genType = typeBuilder.CreateType();

            var parentParam = Expression.Parameter(typeof(IGeneratedStore), "parent");
            var creatorDel = Expression.Lambda<GeneratedStoreCreator>(
                Expression.New(ctor, parentParam), parentParam
            ).Compile();

            return (creatorDel, genType);
        }

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

        #region Correction
        private static bool NeedsCorrection(SerializedMemberInfo member)
        {
            if (member.HasConverter) return false;
            var expectType = GetExpectedValueTypeForType(member.IsNullable ? member.NullableWrappedType : member.Type);

            if (expectType == typeof(Map)) // TODO: make this slightly saner
                return true;
            return false;
        }

        // expects start value on stack, exits with final value on stack
        private static void EmitCorrectMember(ILGenerator il, SerializedMemberInfo member, bool shouldLock, bool alwaysNew, GetLocal GetLocal)
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

            // TODO: impl the rest of this

            // currently the only thing for this is where expect == Map, so do generate shit
            var copyFrom = typeof(IGeneratedStore<>).MakeGenericType(member.Type).GetMethod(nameof(IGeneratedStore<Config>.CopyFrom));
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
                EmitLoad(il, member, il => il.Emit(OpCodes.Ldarg_0));
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
                il.Emit(OpCodes.Brtrue_S, noCreate);
                il.Emit(OpCodes.Pop);
            }
            EmitCreateChildGenerated(il, member.Type);
            il.MarkLabel(noCreate);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, valLocal);
            il.Emit(shouldLock ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, copyFrom);

            // TODO: impl the rest of this

            if (member.IsNullable)
                il.Emit(OpCodes.Newobj, member.Nullable_Construct);

            il.MarkLabel(endLabel);
        }
        #endregion

        #region Utility

        private delegate LocalBuilder GetLocal(Type type, int idx = 0);

        private static GetLocal MakeGetLocal(ILGenerator il)
        { // TODO: improve this shit a bit so that i can release a hold of a variable and do more auto managing
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

            return GetLocal;
        }

        private static void EmitLoad(ILGenerator il, SerializedMemberInfo member, Action<ILGenerator> thisarg = null)
        {
            if (thisarg == null)
                thisarg = il => il.Emit(OpCodes.Ldarg_0);

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

        private static void EmitStore(ILGenerator il, SerializedMemberInfo member, Action<ILGenerator> value)
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

        private static void EmitCreateChildGenerated(ILGenerator il, Type childType)
        {
            var method = CreateGParent.MakeGenericMethod(childType);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, method);
        }
        #endregion

        #region Serialize

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

            var targetType = GetExpectedValueTypeForType(member.Type);
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

                // for now, we assume that its a generated type implementing IGeneratedStore
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

            il.MarkLabel(endSerialize);
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
        #endregion
    }
}
