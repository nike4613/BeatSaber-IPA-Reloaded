using IPA.Config.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Config.Stores
{
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

            public ReaderWriterLockSlim WriteSyncObject { get; } = new ReaderWriterLockSlim();

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
            }

            internal static MethodInfo WriteToMethod = typeof(Impl).GetMethod(nameof(WriteTo));
            public void WriteTo(IConfigProvider provider)
            {
                var values = generated.Values;
                // TODO: implement
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
                    assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
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

        private static Func<IGeneratedStore, IConfigStore> MakeCreator(Type type)
        {
            var typeBuilder = Module.DefineType($"{type.FullName}.Generated", 
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);

            const MethodAttributes propertyMethodAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            #region IGeneratedStore
            typeBuilder.AddInterfaceImplementation(typeof(IGeneratedStore));

            #region IGeneratedStore.Impl
            var implProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Impl), PropertyAttributes.None, typeof(Impl), null);
            var implPropGet = typeBuilder.DefineMethod($"get_{nameof(IGeneratedStore.Impl)}", propertyMethodAttr, implProp.PropertyType, Type.EmptyTypes);
            implProp.SetGetMethod(implPropGet);

            {
                var il = implPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, implField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Type
            var typeProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Type), PropertyAttributes.None, typeof(Type), null);
            var typePropGet = typeBuilder.DefineMethod($"get_{nameof(IGeneratedStore.Type)}", propertyMethodAttr, typeProp.PropertyType, Type.EmptyTypes);
            typeProp.SetGetMethod(typePropGet);

            {
                var il = typePropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, typeField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Parent
            var parentProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Parent), PropertyAttributes.None, typeof(IGeneratedStore), null);
            var parentPropGet = typeBuilder.DefineMethod($"get_{nameof(IGeneratedStore.Parent)}", propertyMethodAttr, parentProp.PropertyType, Type.EmptyTypes);
            parentProp.SetGetMethod(parentPropGet);

            {
                var il = parentPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, parentField); // load impl field
                il.Emit(OpCodes.Ret);
            }
            #endregion
            #region IGeneratedStore.Values
            var valuesProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Values), PropertyAttributes.None, typeof(Value), null);
            var valuesPropGet = typeBuilder.DefineMethod($"get_{nameof(IGeneratedStore.Values)}", propertyMethodAttr, valuesProp.PropertyType, Type.EmptyTypes);
            var valuesPropSet = typeBuilder.DefineMethod($"set_{nameof(IGeneratedStore.Values)}", propertyMethodAttr, null, new[] { valuesProp.PropertyType });
            valuesProp.SetGetMethod(valuesPropGet);
            valuesProp.SetSetMethod(valuesPropSet);

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

            return null;
        }

    }
}
