using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Utilities
{
    /// <summary>
    /// Utilities for inter-thread synchronization. All Locker method acquire their object immediately.
    /// </summary>
    public static class Synchronization
    {
        #region Public types
        /// <summary>
        /// A synchronization state locker that releases its state when its <see cref="IDisposable.Dispose"/>
        /// method is called. ALWAYS use with a <see langword="using"/> block or statement. Otherwise, the locker
        /// may not release the object be released properly.
        /// </summary>
        public interface ISyncStateLocker : IDisposable { }

        /// <summary>
        /// A locker type for <see cref="ReaderWriterLockSlim"/> locks. 
        /// ALWAYS use with a <see langword="using"/> block or statement. Otherwise, the locker
        /// may not release the object be released properly.
        /// </summary>
        public interface IReadWriteLocker : ISyncStateLocker { }

        /// <summary>
        /// An upgradable locker type for <see cref="ReaderWriterLockSlim"/>. 
        /// ALWAYS use with a <see langword="using"/> block or statement. Otherwise, the locker
        /// may not release the object be released properly.
        /// </summary>
        public interface IUpgradableLocker : IReadWriteLocker
        {
            /// <summary>
            /// Upgrades the locker and gives a new locker to manage the upgraded lock.
            /// </summary>
            /// <returns>the locker to use with <see langword="using"/> to manage the write lock</returns>
            IReadWriteLocker Upgrade();
        }
        #endregion

        #region Implementations
        private sealed class MutexLocker : ISyncStateLocker
        {
            private readonly Mutex mutex;

            public MutexLocker(Mutex mutex)
            {
                this.mutex = mutex;
                mutex.WaitOne(); // wait and acquire mutex
            }

            public void Dispose() => mutex.ReleaseMutex(); // release mutex
        }

        private sealed class SemaphoreLocker : ISyncStateLocker
        {
            private readonly Semaphore sem;

            public SemaphoreLocker(Semaphore sem)
            {
                this.sem = sem;
                sem.WaitOne();
            }

            public void Dispose() => sem.Release();
        }

        private sealed class SemaphoreSlimLocker : ISyncStateLocker
        {
            private readonly SemaphoreSlim sem;

            public SemaphoreSlimLocker(SemaphoreSlim sem)
            {
                this.sem = sem;
                sem.Wait();
            }

            public void Dispose() => sem.Release();
        }

        private sealed class ReaderWriterLockSlimWriteLocker : IReadWriteLocker
        {
            private readonly ReaderWriterLockSlim rwl;

            public ReaderWriterLockSlimWriteLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterWriteLock();
            }

            public void Dispose() => rwl.ExitWriteLock();
        }

        private sealed class ReaderWriterLockSlimReadLocker : IReadWriteLocker
        {
            private readonly ReaderWriterLockSlim rwl;

            public ReaderWriterLockSlimReadLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterReadLock();
            }

            public void Dispose() => rwl.ExitReadLock();
        }

        private sealed class ReaderWriterLockSlimUpgradableReadLocker : IUpgradableLocker
        {
            private readonly ReaderWriterLockSlim rwl;

            public ReaderWriterLockSlimUpgradableReadLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterUpgradeableReadLock();
            }

            public IReadWriteLocker Upgrade() => new ReaderWriterLockSlimWriteLocker(rwl);

            public void Dispose() => rwl.ExitUpgradeableReadLock();
        }
        #endregion

        #region Accessors
        // TODO: add async fun stuff to this

        /// <summary>
        /// Creates an <see cref="ISyncStateLocker"/> for a mutex.
        /// </summary>
        /// <param name="mut">the mutex to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ISyncStateLocker Locker(Mutex mut) => new MutexLocker(mut);

        /// <summary>
        /// Creates an <see cref="ISyncStateLocker"/> for a semaphore.
        /// </summary>
        /// <param name="sem">the semaphore to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ISyncStateLocker Locker(Semaphore sem) => new SemaphoreLocker(sem);

        /// <summary>
        /// Creates an <see cref="ISyncStateLocker"/> for a slim semaphore.
        /// </summary>
        /// <param name="sem">the slim semaphore to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ISyncStateLocker Locker(SemaphoreSlim sem) => new SemaphoreSlimLocker(sem);

        /// <summary>
        /// Creates an <see cref="IReadWriteLocker"/> for a <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in write mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static IReadWriteLocker LockWrite(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimWriteLocker(rwl);

        /// <summary>
        /// Creates an <see cref="IReadWriteLocker"/> for a <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in read mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static IReadWriteLocker LockRead(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimReadLocker(rwl);

        /// <summary>
        /// Creates an <see cref="IUpgradableLocker"/> for a <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in upgradable read mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static IUpgradableLocker LockReadUpgradable(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimUpgradableReadLocker(rwl);
        #endregion
    }
}
