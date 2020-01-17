using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Utilities.Async
{
    /// <summary>
    /// A task scheduler that runs tasks on the Unity main thread via coroutines.
    /// </summary>
    public class UnityMainThreadTaskScheduler : TaskScheduler, IDisposable
    {
        /// <summary>
        /// Gets the default main thread scheduler that is managed by BSIPA.
        /// </summary>
        /// <value>a scheduler that is managed by BSIPA</value>
        public static new TaskScheduler Default { get; } = new UnityMainThreadTaskScheduler();
        /// <summary>
        /// Gets a factory for creating tasks on <see cref="Default"/>.
        /// </summary>
        /// <value>a factory for creating tasks on the default scheduler</value>
        public static TaskFactory Factory { get; } = new TaskFactory(Default);

        private readonly ConcurrentDictionary<QueueItem, Task> tasks = new ConcurrentDictionary<QueueItem, Task>();
        private int queueEndPosition = 0;
        private int queuePosition = 0;

        private struct QueueItem : IEquatable<int>, IEquatable<Task>, IEquatable<QueueItem>
        {
            public int Index;
            public Task Task;

            public QueueItem(int index, Task task) : this()
            {
                Index = index;
                Task = task;
            }

            public bool Equals(int other) => Index.Equals(other);
            public bool Equals(Task other) => Task.Equals(other);
            public bool Equals(QueueItem other) => other.Index == Index || other.Task == Task;
        }

        /// <summary>
        /// Gets whether or not this scheduler is currently executing tasks.
        /// </summary>
        /// <value><see langword="true"/> if the scheduler is running, <see langword="false"/> otherwise</value>
        public bool IsRunning { get; private set; } = false;

        /// <summary>
        /// Gets whether or not this scheduler is in the process of shutting down.
        /// </summary>
        /// <value><see langword="true"/> if the scheduler is shutting down, <see langword="false"/> otherwise</value>
        public bool Cancelling { get; private set; } = false;

        private int yieldAfterTasks = 64;
        /// <summary>
        /// Gets or sets the number of tasks to execute before yielding back to Unity.
        /// </summary>
        /// <value>the number of tasks to execute per resume</value>
        public int YieldAfterTasks
        {
            get => yieldAfterTasks;
            set
            {
                ThrowIfDisposed();
                if (value < 1) 
                    throw new ArgumentException("Value cannot be less than 1", nameof(value));
                yieldAfterTasks = value;
            }
        }

        private TimeSpan yieldAfterTime = TimeSpan.FromMilliseconds(.5); // auto-yield if more than half a millis has passed by default
        /// <summary>
        /// Gets or sets the amount of time to execute tasks for before yielding back to Unity. Default is 0.5ms.
        /// </summary>
        /// <value>the amount of time to execute tasks for before yielding back to Unity</value>
        public TimeSpan YieldAfterTime
        {
            get => yieldAfterTime;
            set
            {
                ThrowIfDisposed();
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Value must be greater than zero", nameof(value));
                yieldAfterTime = value;
            }
        }

        /// <summary>
        /// When used as a Unity coroutine, runs the scheduler. Otherwise, this is an invalid call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Do not ever call <see cref="UnityEngine.MonoBehaviour.StopCoroutine(IEnumerator)"/> on this
        /// coroutine, nor <see cref="UnityEngine.MonoBehaviour.StopAllCoroutines"/> on the behaviour hosting
        /// this coroutine. This has no way to detect this, and this object will become invalid.
        /// </para>
        /// <para>
        /// If you need to stop this coroutine, first call <see cref="Cancel"/>, then wait for it to
        /// exit on its own.
        /// </para>
        /// </remarks>
        /// <returns>a Unity coroutine</returns>
        /// <exception cref="ObjectDisposedException">if this scheduler is disposed</exception>
        /// <exception cref="InvalidOperationException">if the scheduler is already running</exception>
        public IEnumerator Coroutine()
        {
            ThrowIfDisposed();

            if (IsRunning)
                throw new InvalidOperationException("Scheduler already running");

            Cancelling = false;
            IsRunning = true;
            yield return null; // yield immediately

            var sw = new Stopwatch();

            try
            {
                while (!Cancelling)
                {
                    if (queuePosition < queueEndPosition)
                    {
                        var yieldAfter = YieldAfterTasks;
                        sw.Start();
                        for (int i = 0; i < yieldAfter && queuePosition < queueEndPosition 
                                                       && sw.Elapsed < YieldAfterTime; i++)
                        {
                            if (tasks.TryRemove(new QueueItem { Index = Interlocked.Increment(ref queuePosition) }, out var task))
                                TryExecuteTask(task); // we succesfully removed the task
                            else
                                i++; // we didn't
                        }
                        sw.Reset();
                    }
                    yield return null;
                }
            }
            finally
            {
                sw.Reset();
                IsRunning = false;
            }
        }

        /// <summary>
        /// Cancels the scheduler. If the scheduler is currently executing tasks, that batch will finish first.
        /// All remaining tasks will be left in the queue.
        /// </summary>
        /// <exception cref="ObjectDisposedException">if this scheduler is disposed</exception>
        /// <exception cref="InvalidOperationException">if the scheduler is not running</exception>
        public void Cancel()
        {
            ThrowIfDisposed();

            if (!IsRunning) throw new InvalidOperationException("The scheduler is not running");
            Cancelling = true;
        }

        /// <summary>
        /// Throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>nothing</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // this is only for debuggers which we can't use sooooo
            throw new NotSupportedException();
        }

        /// <summary>
        /// Queues a given <see cref="Task"/> to this scheduler. The <see cref="Task"/> <i>must></i> be
        /// scheduled for this <see cref="TaskScheduler"/> by the runtime.
        /// </summary>
        /// <param name="task">the <see cref="Task"/> to queue</param>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        protected override void QueueTask(Task task)
        {
            ThrowIfDisposed();

            tasks.TryAdd(new QueueItem(Interlocked.Increment(ref queueEndPosition), task), task);
        }

        /// <summary>
        /// Rejects any attempts to execute a task inline.
        /// </summary>
        /// <remarks>
        /// This task scheduler <i>always</i> runs its tasks on the thread that it manages, therefore it doesn't
        /// make sense to run it inline.
        /// </remarks>
        /// <param name="task">the task to attempt to execute</param>
        /// <param name="taskWasPreviouslyQueued">whether the task was previously queued to this scheduler</param>
        /// <returns><see langword="false"/></returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            ThrowIfDisposed();

            if (!UnityGame.OnMainThread) return false;

            if (taskWasPreviouslyQueued)
                if (!tasks.TryRemove(new QueueItem { Task = task }, out var _))
                    return false; // if we couldn't remove it, its not in our queue, so it already ran

            return TryExecuteTask(task);
        }

        private void ThrowIfDisposed()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SingleThreadTaskScheduler));
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Disposes this object.
        /// </summary>
        /// <param name="disposing">whether or not to dispose managed objects</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes this object. This puts the object into an unusable state.
        /// </summary>
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
