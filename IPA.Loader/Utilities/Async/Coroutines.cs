using IPA.Loader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Utilities.Async
{
    /// <summary>
    /// A class providing coroutine helpers.
    /// </summary>
    public static class Coroutines
    {
        /// <summary>
        /// Stalls the coroutine until <paramref name="task"/> completes, faults, or is canceled.
        /// </summary>
        /// <param name="task">the <see cref="Task"/> to wait for</param>
        /// <returns>a coroutine waiting for the given task</returns>
        public static IEnumerator WaitForTask(Task task)
            => WaitForTask(task, false);

        /// <summary>
        /// Stalls the coroutine until <paramref name="task"/> completes, faults, or is canceled.
        /// </summary>
        /// <param name="task">the <see cref="Task"/> to wait for</param>
        /// <param name="throwOnFault">whether or not to throw if the task faulted</param>
        /// <returns>a coroutine waiting for the given task</returns>
        public static IEnumerator WaitForTask(Task task, bool throwOnFault = false)
        {
            while (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                yield return null;
            if (throwOnFault && task.IsFaulted)
                throw task.Exception;
        }

        /// <summary>
        /// Binds a <see cref="Task"/> to a Unity coroutine, capturing exceptions as well as the coroutine call stack.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This may be called off of the Unity main thread. If it is, the coroutine start will be scheduled using the default
        /// <see cref="UnityMainThreadTaskScheduler"/> and will be run on the main thread as required by Unity.
        /// </para>
        /// <para>
        /// Unity provides a handful of coroutine helpers that are not <see cref="IEnumerable"/>s. Most of these are not terribly
        /// helpful on their own, however <see cref="UnityEngine.WaitForSeconds"/> may be. Instead, prefer to use the typical .NET
        /// <see cref="Task.Wait(TimeSpan)"/> or similar overloads, or use <see cref="UnityEngine.WaitForSecondsRealtime"/>.
        /// </para>
        /// </remarks>
        /// <param name="coroutine">the coroutine to bind to a task</param>
        /// <returns>a <see cref="Task"/> that completes when <paramref name="coroutine"/> completes, and fails when it throws</returns>
        public static Task AsTask(IEnumerator coroutine)
        {
            if (!UnityGame.OnMainThread)
                return UnityMainThreadTaskScheduler.Factory.StartNew(() => AsTask(coroutine), default, default, UnityMainThreadTaskScheduler.Default).Unwrap();

            var tcs = new TaskCompletionSource<VoidStruct>(coroutine, AsTaskSourceOptions);
            _ = PluginComponent.Instance.StartCoroutine(new AsTaskCoroutineExecutor(coroutine, tcs));
            return tcs.Task;
        }

#if NET4
        private static readonly TaskCreationOptions AsTaskSourceOptions = TaskCreationOptions.RunContinuationsAsynchronously;
#else
        private static readonly TaskCreationOptions AsTaskSourceOptions = TaskCreationOptions.None;
#endif

        private struct VoidStruct { }
        private class ExceptionLocation : Exception
        {
            public ExceptionLocation(IEnumerable<string> locations) 
                : base(string.Join("\n", Utils.StrJP(locations.Select(s => "in " + s))))
            {
            }
        }
        private class AsTaskCoroutineExecutor : IEnumerator
        {
            private readonly TaskCompletionSource<VoidStruct> completionSource;

            public AsTaskCoroutineExecutor(IEnumerator coroutine, TaskCompletionSource<VoidStruct> completion)
            {
                completionSource = completion;
                enumerators.Push(coroutine);
            }

            private readonly Stack<IEnumerator> enumerators = new(2);

            public object Current => enumerators.FirstOrDefault()?.Current; // effectively a TryPeek

            public bool MoveNext()
            {
                do
                {
                    if (enumerators.Count == 0)
                    {
                        completionSource.SetResult(new VoidStruct());
                        return false;
                    }

                    try
                    {
                        var top = enumerators.Peek();
                        if (top.MoveNext())
                        {
                            if (top.Current is IEnumerator enumerator)
                            {
                                enumerators.Push(enumerator);
                                continue;
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        { // this enumerator completed, so pop it and continue
                            _ = enumerators.Pop();
                            continue;
                        }
                    }
                    catch (Exception e)
                    { // execution errored
                        completionSource.SetException(new AggregateException(
                            e, new ExceptionLocation(enumerators.Select(e => e.GetType().ToString()))
                        ));
                        return false;
                    }
                }
                while (true);
            }

            public void Reset() => throw new InvalidOperationException();
        }

    }
}
