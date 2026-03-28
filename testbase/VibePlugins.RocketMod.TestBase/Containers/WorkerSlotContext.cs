using System.Threading;

namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Provides an ambient <see cref="AsyncLocal{T}"/> context for the currently active
    /// <see cref="WorkerSlot"/>. In distributed test execution mode, the
    /// <c>WorkerTestCaseRunner</c> sets this before invoking the test so that
    /// <c>RocketModPluginTestBase.InitializeAsync</c> can detect it and skip creating
    /// its own container/session.
    /// </summary>
    public static class WorkerSlotContext
    {
        private static readonly AsyncLocal<WorkerSlot> CurrentSlot = new AsyncLocal<WorkerSlot>();

        /// <summary>
        /// Gets or sets the <see cref="WorkerSlot"/> for the current async execution context.
        /// Returns <c>null</c> when not running in distributed mode.
        /// </summary>
        public static WorkerSlot Current
        {
            get => CurrentSlot.Value;
            set => CurrentSlot.Value = value;
        }

        /// <summary>
        /// Gets whether a worker slot is active in the current async context,
        /// indicating distributed execution mode.
        /// </summary>
        public static bool IsDistributed => CurrentSlot.Value != null;
    }
}
