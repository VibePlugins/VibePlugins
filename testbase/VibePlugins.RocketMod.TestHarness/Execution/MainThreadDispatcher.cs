using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Provides a mechanism to enqueue work items that must execute on the Unity main thread.
    /// Uses a <see cref="ConcurrentQueue{T}"/> drained each frame from
    /// <see cref="TestHarnessPlugin.Update"/>.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<WorkItem> Queue = new ConcurrentQueue<WorkItem>();

        /// <summary>
        /// Enqueues a synchronous function to run on the main thread and returns its result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="action">The function to execute on the main thread.</param>
        /// <returns>A task that completes with the result once the function has executed.</returns>
        public static Task<T> EnqueueAsync<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            Queue.Enqueue(new WorkItem(() =>
            {
                try
                {
                    T result = action();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return Task.CompletedTask;
            }));

            return tcs.Task;
        }

        /// <summary>
        /// Enqueues an asynchronous function to run on the main thread.
        /// The async callback is started on the main thread; its continuations may
        /// run on the thread-pool.
        /// </summary>
        /// <param name="action">The async function to execute.</param>
        /// <returns>A task that completes when the async function finishes.</returns>
        public static Task EnqueueAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Queue.Enqueue(new WorkItem(async () =>
            {
                try
                {
                    await action().ConfigureAwait(false);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));

            return tcs.Task;
        }

        /// <summary>
        /// Drains the work queue and executes all pending items. Must be called
        /// from the Unity main thread (typically in <c>Update()</c>).
        /// </summary>
        public static void ProcessQueue()
        {
            while (Queue.TryDequeue(out var item))
            {
                try
                {
                    // Fire the work item. If it's truly async, it will continue on the
                    // thread pool after its first await. Synchronous items complete inline.
                    item.Execute();
                }
                catch (Exception ex)
                {
                    Rocket.Core.Logging.Logger.LogException(ex,
                        "[TestHarness] MainThreadDispatcher work item error");
                }
            }
        }

        /// <summary>
        /// Internal work item wrapping an async delegate.
        /// </summary>
        private sealed class WorkItem
        {
            private readonly Func<Task> _action;

            public WorkItem(Func<Task> action)
            {
                _action = action;
            }

            public void Execute()
            {
                // We intentionally do not await here; the TCS bridges the result
                // to the original caller.
                _action();
            }
        }
    }
}
