using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Utilities.Async
{
    /// <summary>
    /// Utilities for inter-thread synchronization. All Locker method acquire their object immediately,
    /// and should only be used with <see langword="using"/> to automatically release them.
    /// </summary>
    /// <example>
    /// <para>
    /// The canonical usage of *all* of the member functions is as follows, substituting <see cref="Lock(Mutex)"/>
    /// with whichever member you want to use, according to your lock type.
    /// </para>
    /// <code>
    /// using var _locker = Synchronization.Lock(mutex);
    /// </code>
    /// </example>
    public static class Synchronization
    {
#region Locker structs
        /// <summary>
        /// A locker for a <see cref="Mutex"/> that automatically releases when it is disposed.
        /// Create this with <see cref="Lock(Mutex)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="Lock(Mutex)"/>
        public struct MutexLocker : IDisposable
        {
            private readonly Mutex mutex;

            internal MutexLocker(Mutex mutex)
            {
                this.mutex = mutex;
                mutex.WaitOne(); // wait and acquire mutex
            }

            void IDisposable.Dispose() => mutex.ReleaseMutex(); // release mutex
        }

        /// <summary>
        /// A locker for a <see cref="Semaphore"/> that automatically releases when it is disposed.
        /// Create this with <see cref="Lock(Semaphore)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="Lock(Semaphore)"/>
        public struct SemaphoreLocker : IDisposable
        {
            private readonly Semaphore sem;

            internal SemaphoreLocker(Semaphore sem)
            {
                this.sem = sem;
                sem.WaitOne();
            }

            void IDisposable.Dispose() => sem.Release();
        }

        /// <summary>
        /// A locker for a <see cref="SemaphoreSlim"/> that automatically releases when it is disposed.
        /// Create this with <see cref="Lock(SemaphoreSlim)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="Lock(SemaphoreSlim)"/>
        public struct SemaphoreSlimLocker : IDisposable
        {
            private readonly SemaphoreSlim sem;

            internal SemaphoreSlimLocker(SemaphoreSlim sem)
            {
                this.sem = sem;
                sem.Wait();
            }

            void IDisposable.Dispose() => sem.Release();
        }

#if NET4
        /// <summary>
        /// A locker for a <see cref="SemaphoreSlim"/> that was created asynchronously and automatically releases
        /// when it is disposed. Create this with <see cref="LockAsync(SemaphoreSlim)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="LockAsync(SemaphoreSlim)"/>
        public struct SemaphoreSlimAsyncLocker : IDisposable
        {
            private readonly SemaphoreSlim sem;

            internal SemaphoreSlimAsyncLocker(SemaphoreSlim sem) => this.sem = sem;
            internal Task Lock() => sem.WaitAsync();

            void IDisposable.Dispose() => sem.Release();
        }
#endif

        /// <summary>
        /// A locker for a write lock on a <see cref="ReaderWriterLockSlim"/> that automatically releases when
        /// it is disposed. Create this with <see cref="LockWrite(ReaderWriterLockSlim)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="LockWrite(ReaderWriterLockSlim)"/>
        public struct ReaderWriterLockSlimWriteLocker : IDisposable
        {
            private readonly ReaderWriterLockSlim rwl;

            internal ReaderWriterLockSlimWriteLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterWriteLock();
            }

            void IDisposable.Dispose() => rwl.ExitWriteLock();
        }

        /// <summary>
        /// A locker for a read lock on a <see cref="ReaderWriterLockSlim"/> that automatically releases when
        /// it is disposed. Create this with <see cref="LockRead(ReaderWriterLockSlim)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="LockRead(ReaderWriterLockSlim)"/>
        public struct ReaderWriterLockSlimReadLocker : IDisposable
        {
            private readonly ReaderWriterLockSlim rwl;

            internal ReaderWriterLockSlimReadLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterReadLock();
            }

            void IDisposable.Dispose() => rwl.ExitReadLock();
        }

        /// <summary>
        /// A locker for an upgradable read lock on a <see cref="ReaderWriterLockSlim"/> that automatically releases
        /// when it is disposed. Create this with <see cref="LockReadUpgradable(ReaderWriterLockSlim)"/>.
        /// </summary>
        /// <seealso cref="Synchronization"/>
        /// <seealso cref="LockReadUpgradable(ReaderWriterLockSlim)"/>
        public struct ReaderWriterLockSlimUpgradableReadLocker : IDisposable
        {
            private readonly ReaderWriterLockSlim rwl;

            internal ReaderWriterLockSlimUpgradableReadLocker(ReaderWriterLockSlim lck)
            {
                rwl = lck;
                rwl.EnterUpgradeableReadLock();
            }

            /// <summary>
            /// Creates a locker for a write lock on the <see cref="ReaderWriterLockSlim"/> associated with this locker,
            /// upgrading the current thread's lock.
            /// </summary>
            /// <returns>a locker for the new write lock</returns>
            /// <seealso cref="Synchronization"/>
            public ReaderWriterLockSlimWriteLocker Upgrade() => new ReaderWriterLockSlimWriteLocker(rwl);

            void IDisposable.Dispose() => rwl.ExitUpgradeableReadLock();
        }
#endregion

#region Accessors
        /// <summary>
        /// Creates a locker for a mutex.
        /// </summary>
        /// <param name="mut">the mutex to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static MutexLocker Lock(Mutex mut) => new MutexLocker(mut);

        /// <summary>
        /// Creates a locker for a semaphore.
        /// </summary>
        /// <param name="sem">the semaphore to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static SemaphoreLocker Lock(Semaphore sem) => new SemaphoreLocker(sem);

        /// <summary>
        /// Creates a locker for a slim semaphore.
        /// </summary>
        /// <param name="sem">the slim semaphore to acquire</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static SemaphoreSlimLocker Lock(SemaphoreSlim sem) => new SemaphoreSlimLocker(sem);

#if NET4 // TODO: make this work on NET3 too
        /// <summary>
        /// Creates a locker for a slim semaphore asynchronously.
        /// </summary>
        /// <param name="sem">the slim semaphore to acquire async</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static async Task<SemaphoreSlimAsyncLocker> LockAsync(SemaphoreSlim sem)
        {
            var locker = new SemaphoreSlimAsyncLocker(sem);
            await locker.Lock();
            return locker;
        }
#endif

        /// <summary>
        /// Creates a locker for a write lock <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in write mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ReaderWriterLockSlimWriteLocker LockWrite(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimWriteLocker(rwl);

        /// <summary>
        /// Creates a locker for a read lock on a <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in read mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ReaderWriterLockSlimReadLocker LockRead(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimReadLocker(rwl);

        /// <summary>
        /// Creates a locker for an upgradable read lock on a <see cref="ReaderWriterLockSlim"/>.
        /// </summary>
        /// <param name="rwl">the lock to acquire in upgradable read mode</param>
        /// <returns>the locker to use with <see langword="using"/></returns>
        public static ReaderWriterLockSlimUpgradableReadLocker LockReadUpgradable(ReaderWriterLockSlim rwl) => new ReaderWriterLockSlimUpgradableReadLocker(rwl);
#endregion
    }
}
