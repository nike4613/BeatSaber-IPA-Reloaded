using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Config
{
    /// <summary>
    /// A storage for a config structure.
    /// </summary>
    public interface IConfigStore
    {
        /// <summary>
        /// A synchronization object for the save thread to wait on for changes. 
        /// It should be signaled whenever the internal state of the object is changed.
        /// The writer will never signal this handle. 
        /// </summary>
        WaitHandle SyncObject { get; }

        /// <summary>
        /// A synchronization object for the load thread and accessors to maintain safe synchronization.
        /// Any readers should take a read lock with <see cref="ReaderWriterLockSlim.EnterReadLock()"/> or
        /// <see cref="ReaderWriterLockSlim.EnterUpgradeableReadLock()"/>, and any writers should take a 
        /// write lock with <see cref="ReaderWriterLockSlim.EnterWriteLock()"/>.
        /// </summary>
        /// <remarks>
        /// Read and write are read and write to *this object*, not to the file on disk.
        /// </remarks>
        ReaderWriterLockSlim WriteSyncObject { get; }

        /// <summary>
        /// Writes the config structure stored by the current <see cref="IConfigStore"/> to the given
        /// <see cref="IConfigProvider"/>.
        /// </summary>
        /// <remarks>
        /// The calling code will have entered a read lock on <see cref="WriteSyncObject"/> when
        /// this is called.
        /// </remarks>
        /// <param name="provider">the provider to write to</param>
        void WriteTo(ConfigProvider provider);

        /// <summary>
        /// Reads the config structure from the given <see cref="IConfigProvider"/> into the current 
        /// <see cref="IConfigStore"/>.
        /// </summary>
        /// <remarks>
        /// The calling code will have entered a write lock on <see cref="WriteSyncObject"/> when
        /// this is called.
        /// </remarks>
        /// <param name="provider">the provider to read from</param>
        void ReadFrom(ConfigProvider provider);

    }
}
