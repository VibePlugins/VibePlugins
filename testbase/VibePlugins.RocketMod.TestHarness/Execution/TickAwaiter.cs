using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Provides the ability to wait for a specified number of Unity update ticks
    /// (Update, FixedUpdate, or LateUpdate) before continuing execution.
    /// Tick callbacks are driven by <see cref="TestHarnessPlugin"/>.
    /// </summary>
    public static class TickAwaiter
    {
        private static readonly ConcurrentQueue<TickWaiter> UpdateWaiters = new ConcurrentQueue<TickWaiter>();
        private static readonly ConcurrentQueue<TickWaiter> FixedUpdateWaiters = new ConcurrentQueue<TickWaiter>();
        private static readonly ConcurrentQueue<TickWaiter> LateUpdateWaiters = new ConcurrentQueue<TickWaiter>();

        /// <summary>
        /// Waits for the specified number of ticks in the given update phase.
        /// </summary>
        /// <param name="count">Number of ticks to wait.</param>
        /// <param name="tickType">Which update phase to count ticks in.</param>
        /// <returns>A task that completes after the ticks have elapsed.</returns>
        public static Task WaitAsync(int count, TickType tickType)
        {
            if (count <= 0)
            {
                return Task.CompletedTask;
            }

            var waiter = new TickWaiter(count);

            switch (tickType)
            {
                case TickType.Update:
                    UpdateWaiters.Enqueue(waiter);
                    break;
                case TickType.FixedUpdate:
                    FixedUpdateWaiters.Enqueue(waiter);
                    break;
                case TickType.LateUpdate:
                    LateUpdateWaiters.Enqueue(waiter);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tickType));
            }

            return waiter.Task;
        }

        /// <summary>
        /// Called from <see cref="TestHarnessPlugin.Update"/> each frame.
        /// Decrements all pending Update waiters.
        /// </summary>
        internal static void OnUpdate()
        {
            ProcessQueue(UpdateWaiters);
        }

        /// <summary>
        /// Called from <see cref="TestHarnessPlugin.FixedUpdate"/> each physics step.
        /// Decrements all pending FixedUpdate waiters.
        /// </summary>
        internal static void OnFixedUpdate()
        {
            ProcessQueue(FixedUpdateWaiters);
        }

        /// <summary>
        /// Called from <see cref="TestHarnessPlugin.LateUpdate"/> each frame.
        /// Decrements all pending LateUpdate waiters.
        /// </summary>
        internal static void OnLateUpdate()
        {
            ProcessQueue(LateUpdateWaiters);
        }

        private static void ProcessQueue(ConcurrentQueue<TickWaiter> queue)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out var waiter))
                    break;

                waiter.Tick();

                if (!waiter.IsComplete)
                {
                    queue.Enqueue(waiter);
                }
            }
        }

        /// <summary>
        /// Represents a single tick-wait operation with a countdown.
        /// </summary>
        private sealed class TickWaiter
        {
            private int _remaining;
            private readonly TaskCompletionSource<bool> _tcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Task => _tcs.Task;
            public bool IsComplete => _remaining <= 0;

            public TickWaiter(int count)
            {
                _remaining = count;
            }

            public void Tick()
            {
                if (Interlocked.Decrement(ref _remaining) <= 0)
                {
                    _tcs.TrySetResult(true);
                }
            }
        }
    }
}
