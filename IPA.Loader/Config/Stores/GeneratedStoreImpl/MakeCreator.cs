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

        internal delegate IConfigStore GeneratedStoreCreator(IGeneratedStore parent);
        private static void GetMethodThis(ILGenerator il) => il.Emit(OpCodes.Ldarg_0);

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

            var structure = ReadObjectMembers(type);
            if (!structure.Any())
                Logger.config.Warn($"Custom type {type.FullName} has no accessible members");
            #endregion

            var typeBuilder = Module.DefineType($"{type.FullName}<Generated>",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            var typeField = typeBuilder.DefineField("<>_type", typeof(Type), FieldAttributes.Private | FieldAttributes.InitOnly);
            var implField = typeBuilder.DefineField("<>_impl", typeof(Impl), FieldAttributes.Private | FieldAttributes.InitOnly);
            var parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore), FieldAttributes.Private | FieldAttributes.InitOnly);

            /*#region Converter fields
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
            #endregion*/

            //CreateAndInitializeConvertersFor(type, structure);

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

                var GetLocal = MakeLocalAllocator(il);

                foreach (var member in structure)
                {
                    if (NeedsCorrection(member))
                        EmitLoadCorrectStore(il, member, false, true, GetLocal, GetMethodThis, GetMethodThis, GetMethodThis);
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
                    .Where(e => e.GetAddMethod().GetBaseDefinition().DeclaringType == INotifyPropertyChanged_t)
                    .FirstOrDefault();
                var basePropChangedAdd = basePropChangedEvent?.GetAddMethod();
                var basePropChangedRemove = basePropChangedEvent?.GetRemoveMethod();

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

                var GetLocal = MakeLocalAllocator(il);

                EmitSerializeStructure(il, structure, GetLocal, GetMethodThis, GetMethodThis);

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

                il.MarkLabel(notMapError);

                var GetLocal = MakeLocalAllocator(il);

                // head of stack is Map instance
                EmitDeserializeStructure(il, structure, mapLocal, valueLocal, GetLocal, GetMethodThis, GetMethodThis);

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

                var GetLocal = MakeLocalAllocator(il);

                foreach (var member in structure)
                {
                    il.BeginExceptionBlock();

                    EmitLoadCorrectStore(il, member, false, false, GetLocal, il => il.Emit(OpCodes.Ldarg_1), GetMethodThis, GetMethodThis);

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

                {
                    var il = propSet.GetILGenerator();

                    var transactionLocal = il.DeclareLocal(IDisposable_t);
                    var GetLocal = MakeLocalAllocator(il);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, coreChangeTransaction); // take the write lock
                    il.Emit(OpCodes.Stloc, transactionLocal);

                    il.BeginExceptionBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    EmitCorrectMember(il, member, false, false, GetLocal, GetMethodThis, GetMethodThis);
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
    }
}
