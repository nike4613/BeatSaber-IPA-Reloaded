using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Utilities.Async
{
    public class UnityMainThreadTaskScheduler : TaskScheduler, IDisposable
    {
        public static new TaskScheduler Default { get; } = new UnityMainThreadTaskScheduler();

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

        public bool IsRunning { get; private set; } = false;

        public int YieldAfterTasks { get; set; } = 4;

        public IEnumerator Coroutine()
        {
            ThrowIfDisposed();

            IsRunning = true;
            yield return null; // yield immediately

            try
            {
                while (true)
                {
                    if (queuePosition < queueEndPosition)
                    {
                        var yieldAfter = YieldAfterTasks;
                        for (int i = 0; i < yieldAfter && queuePosition < queueEndPosition; i++)
                        {
                            if (tasks.TryRemove(new QueueItem { Index = Interlocked.Increment(ref queuePosition) }, out var task))
                                TryExecuteTask(task); // we succesfully removed the task
                            else
                                i++; // we didn't
                        }
                    }
                    yield return null;
                }
            }
            finally
            {
                IsRunning = false;
            }
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

        protected override void QueueTask(Task task)
        {
            ThrowIfDisposed();

            tasks.TryAdd(new QueueItem(Interlocked.Increment(ref queueEndPosition), task), task);
        }

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
