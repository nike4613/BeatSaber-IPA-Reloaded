using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using IPA.Config.Data;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(IPA.Config.Stores.GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
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
            public void SignalChanged()
            {
                try
                {
                    resetEvent.Set();
                }
                catch (ObjectDisposedException e)
                {
                    Logger.config.Error($"ObjectDisposedException while signalling a change for generated store {generated?.GetType()}");
                    Logger.config.Error(e);
                }
            }

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
                    try
                    {
                        if (data.ownsWrite)
                            data.impl.ReleaseWrite();
                    }
                    catch
                    {
                    }
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
                Logger.config.Debug($"Generated impl ReadFrom {generated.GetType()}");
                var values = provider.Load();
                //Logger.config.Debug($"Read {values}");
                generated.Deserialize(values);

                using var transaction = generated.ChangeTransaction();
                generated.OnReload();
            }

            internal static MethodInfo ImplWriteToMethod = typeof(Impl).GetMethod(nameof(ImplWriteTo));
            public static void ImplWriteTo(IGeneratedStore s, ConfigProvider provider) => FindImpl(s).WriteTo(provider);
            public void WriteTo(ConfigProvider provider)
            {
                Logger.config.Debug($"Generated impl WriteTo {generated.GetType()}");
                var values = generated.Serialize();
                //Logger.config.Debug($"Serialized {values}");
                provider.Store(values);
            }
        }
    }
}
