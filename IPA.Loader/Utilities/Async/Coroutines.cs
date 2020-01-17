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
        {
            while (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                yield return null;
        }
    }
}
