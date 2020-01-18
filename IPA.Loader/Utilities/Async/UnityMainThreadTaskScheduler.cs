using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        private readonly ConcurrentQueue<QueueItem> tasks = new ConcurrentQueue<QueueItem>();
        private static readonly ConditionalWeakTable<Task, QueueItem> itemTable = new ConditionalWeakTable<Task, QueueItem>();

        private class QueueItem : IEquatable<Task>, IEquatable<QueueItem>
        {
            public bool HasTask;
            private readonly WeakReference<Task> weakTask = null;
            public Task Task => weakTask.TryGetTarget(out var task) ? task : null;

            public QueueItem(Task task)
            {
                HasTask = true;
                weakTask = new WeakReference<Task>(task);
            }

            private bool Equals(WeakReference<Task> task)
                => weakTask.TryGetTarget(out var t1) && task.TryGetTarget(out var t2) && t1.Equals(t2);
            public bool Equals(Task other) => HasTask && weakTask.TryGetTarget(out var task) && other.Equals(task);
            public bool Equals(QueueItem other) => other.HasTask == HasTask && Equals(other.weakTask);
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
                    if (!tasks.IsEmpty)
                    {
                        var yieldAfter = YieldAfterTasks;
                        sw.Start();
                        for (int i = 0; i < yieldAfter && !tasks.IsEmpty 
                                                       && sw.Elapsed < YieldAfterTime; i++)
                        {
                            QueueItem task;
                            do if (!tasks.TryDequeue(out task)) goto exit; // try dequeue, if we can't exit
                            while (!task.HasTask); // if the dequeued task is empty, try again

                            TryExecuteTask(task.Task);
                        }
                        exit:
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

            var item = new QueueItem(task);
            itemTable.Add(task, item);
            tasks.Enqueue(item);
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
            {
                if (itemTable.TryGetValue(task, out var item))
                {
                    if (!item.HasTask) return false;
                    item.HasTask = false;
                }
                else return false; // if we couldn't remove it, its not in our queue, so it already ran
            }

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
