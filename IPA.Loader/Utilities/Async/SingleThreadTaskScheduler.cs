using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Utilities.Async
{
    /// <summary>
    /// A single-threaded task scheduler that runs all of its tasks on the same thread.
    /// </summary>
    public class SingleThreadTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly Thread runThread = new Thread(ExecuteTasksS);
        private readonly BlockingCollection<Task> tasks = new BlockingCollection<Task>();
        private readonly CancellationTokenSource exitTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Gets whether or not the underlying thread has been started.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        public bool IsRunning
        {
            get
            {
                ThrowIfDisposed();
                return runThread.IsAlive;
            }
        }

        /// <summary>
        /// Starts the thread that executes tasks scheduled with this <see cref="TaskScheduler"/>
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        public void Start()
        {
            ThrowIfDisposed();

            runThread.Start(this);
        }

        /// <summary>
        /// Terminates the runner thread, and waits for the currently running task to complete.
        /// </summary>
        /// <remarks>
        /// After this method returns, this object has been disposed and is no longer in a valid state.
        /// </remarks>
        /// <returns>an <see cref="IEnumerable{T}"/> of <see cref="Task"/>s that did not execute</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        public IEnumerable<Task> Exit()
        {
            ThrowIfDisposed();

            tasks.CompleteAdding();
            exitTokenSource.Cancel();
            runThread.Join();

            var retTasks = new List<Task>();
            retTasks.AddRange(tasks);

            return retTasks;
        }

        /// <summary>
        /// Waits for the runner thread to complete all tasks in the queue, then exits.
        /// </summary>
        /// <remarks>
        /// After this method returns, this object has been disposed and is no longer in a valid state.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if this object has already been disposed.</exception>
        public void Join()
        {
            ThrowIfDisposed();

            tasks.CompleteAdding();
            runThread.Join();
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

            tasks.Add(task);
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

            return false;
        }

        private void ThrowIfDisposed()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(SingleThreadTaskScheduler));
        }

        private void ExecuteTasks()
        {
            ThrowIfDisposed();

            var token = exitTokenSource.Token;

            try
            {
                // while we are still accepting tasks, and we can pull out a task with an infinite wait duration
                while (!tasks.IsCompleted && tasks.TryTake(out var task, -1, token))
                {
                    TryExecuteTask(task);
                }
            }
            catch (OperationCanceledException)
            {
                // TryTake was cancelled, we'll just leave
            }
        }

        private static void ExecuteTasksS(object param)
        {
            var self = param as SingleThreadTaskScheduler;
            self.ExecuteTasks();
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
                    exitTokenSource.Dispose();
                    tasks.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes this object. This puts the object into an unusable state.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
