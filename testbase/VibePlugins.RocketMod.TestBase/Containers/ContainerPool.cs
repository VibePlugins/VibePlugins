using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Represents a single worker slot in the container pool. Each slot owns an independent
    /// Unturned server container with its own sidecars and bridge connection.
    /// </summary>
    public class WorkerSlot
    {
        private readonly TestSession _session;

        internal WorkerSlot(int index, TestSession session)
        {
            Index = index;
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Gets the zero-based index of this worker in the pool.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the underlying Unturned server container for this worker.
        /// </summary>
        public UnturnedServerContainer Container => _session.Container;

        /// <summary>
        /// Gets the TCP bridge client for communicating with this worker's harness.
        /// </summary>
        public TestBridgeClient Bridge => _session.Bridge;

        /// <summary>
        /// Gets the MySQL connection string if a MySQL sidecar is configured, otherwise <c>null</c>.
        /// </summary>
        public string MySqlConnectionString => _session.MySqlConnectionString;

        /// <summary>
        /// Gets the underlying test session.
        /// </summary>
        internal TestSession Session => _session;

        /// <summary>
        /// Prepares this worker for executing a test against the specified plugin assembly.
        /// Cleans plugin directories, deploys the plugin and its dependencies, restarts the
        /// server container, and waits for both the harness and plugin to become ready.
        /// </summary>
        /// <param name="pluginAssembly">The assembly containing the RocketMod plugin under test.</param>
        /// <param name="pluginName">The name of the plugin type to wait for loading.</param>
        /// <param name="harnessTimeout">Maximum time to wait for the harness to become ready.</param>
        /// <param name="pluginLoadTimeout">Maximum time to wait for the plugin to load.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task PrepareForTestAsync(
            Assembly pluginAssembly,
            string pluginName,
            TimeSpan harnessTimeout,
            TimeSpan pluginLoadTimeout,
            CancellationToken ct = default)
        {
            if (pluginAssembly == null) throw new ArgumentNullException(nameof(pluginAssembly));
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name must not be null or empty.", nameof(pluginName));

            // 1. Clean plugin directories
            await _session.CleanPluginDirectoriesAsync().ConfigureAwait(false);

            // 2. Deploy plugin DLL + dependencies
            await _session.DeployPluginAsync(pluginAssembly).ConfigureAwait(false);

            // 3. Docker restart the container
            await _session.RestartServerAsync(ct).ConfigureAwait(false);

            // 4. Wait for harness ready
            await _session.WaitForHarnessReadyAsync(harnessTimeout, ct).ConfigureAwait(false);

            // 5. Wait for plugin load
            PluginLoadedMessage loadMsg = await _session.WaitForPluginLoadAsync(pluginName, pluginLoadTimeout, ct)
                .ConfigureAwait(false);

            if (!loadMsg.Success)
            {
                throw new PluginLoadFailedException(
                    $"Worker {Index}: Plugin '{pluginName}' failed to load: {loadMsg.Error}");
            }
        }

        /// <summary>
        /// Runs cleanup commands on this worker's harness: destroys all mocks, clears event
        /// captures, and runs any registered cleanup routines.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task RunCleanupAsync(CancellationToken ct = default)
        {
            if (Bridge == null || !Bridge.IsConnected)
                return;

            // 1. Send RunCleanup command
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    await Bridge.SendAndWaitAsync<RunCleanupResponse>(
                        new RunCleanupRequest(), cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[WorkerSlot {Index}] RunCleanup failed: {ex.Message}");
            }

            // 2. Send DestroyAllMocks
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await Bridge.SendAndWaitAsync<DestroyAllMocksResponse>(
                        new DestroyAllMocksRequest(), cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[WorkerSlot {Index}] DestroyAllMocks failed: {ex.Message}");
            }

            // 3. Send ClearEventCaptures
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await Bridge.SendAndWaitAsync<ClearEventCapturesResponse>(
                        new ClearEventCapturesRequest(), cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[WorkerSlot {Index}] ClearEventCaptures failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Manages a pool of N reusable container worker slots for distributed test execution.
    /// Each worker slot has its own independent Unturned server container, sidecars, and bridge
    /// connection. Workers can be reused across tests by cleaning up and redeploying plugins.
    /// </summary>
    public class ContainerPool : IAsyncDisposable
    {
        private readonly WorkerSlot[] _workers;
        private bool _disposed;

        private ContainerPool(WorkerSlot[] workers)
        {
            _workers = workers ?? throw new ArgumentNullException(nameof(workers));
        }

        /// <summary>
        /// Gets the number of worker slots in this pool.
        /// </summary>
        public int WorkerCount => _workers.Length;

        /// <summary>
        /// Creates a new container pool with the specified number of workers. Each worker
        /// gets its own independently built container stack.
        /// </summary>
        /// <param name="workerCount">The number of parallel workers to create.</param>
        /// <param name="configureContainer">Optional configuration for each container builder.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A fully initialized <see cref="ContainerPool"/> with all workers started.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="workerCount"/> is less than 1.</exception>
        public static async Task<ContainerPool> CreateAsync(
            int workerCount,
            Action<UnturnedContainerBuilder> configureContainer = null,
            CancellationToken ct = default)
        {
            if (workerCount < 1)
                throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be at least 1.");

            var startTasks = new Task<WorkerSlot>[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                int index = i;
                startTasks[i] = CreateWorkerAsync(index, configureContainer, ct);
            }

            WorkerSlot[] workers;
            try
            {
                workers = await Task.WhenAll(startTasks).ConfigureAwait(false);
            }
            catch
            {
                // If any worker failed to start, dispose the ones that succeeded
                var disposeTasks = new List<Task>();
                for (int i = 0; i < startTasks.Length; i++)
                {
                    if (startTasks[i].Status == TaskStatus.RanToCompletion && startTasks[i].Result != null)
                    {
                        disposeTasks.Add(startTasks[i].Result.Session.DisposeAsync().AsTask());
                    }
                }

                if (disposeTasks.Count > 0)
                {
                    try { await Task.WhenAll(disposeTasks).ConfigureAwait(false); }
                    catch { /* best effort cleanup */ }
                }

                throw;
            }

            return new ContainerPool(workers);
        }

        /// <summary>
        /// Gets the worker slot at the specified index.
        /// </summary>
        /// <param name="index">The zero-based worker index.</param>
        /// <returns>The <see cref="WorkerSlot"/> at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is out of range.</exception>
        public WorkerSlot GetWorker(int index)
        {
            if (index < 0 || index >= _workers.Length)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Worker index must be between 0 and {_workers.Length - 1}.");

            return _workers[index];
        }

        /// <summary>
        /// Stops and disposes all worker containers and their associated resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            var disposeTasks = new Task[_workers.Length];
            for (int i = 0; i < _workers.Length; i++)
            {
                disposeTasks[i] = DisposeWorkerAsync(_workers[i]);
            }

            await Task.WhenAll(disposeTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Safely disposes a single worker slot, catching and logging any errors.
        /// </summary>
        private static async Task DisposeWorkerAsync(WorkerSlot worker)
        {
            try
            {
                await worker.Session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[ContainerPool] Failed to dispose worker {worker.Index}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates and starts a single worker with its own container stack.
        /// </summary>
        private static async Task<WorkerSlot> CreateWorkerAsync(
            int index,
            Action<UnturnedContainerBuilder> configureContainer,
            CancellationToken ct)
        {
            var builder = new UnturnedContainerBuilder();
            configureContainer?.Invoke(builder);

            UnturnedServerContainer container = builder.Build();
            await container.StartAsync(ct).ConfigureAwait(false);

            var bridge = new TestBridgeClient();
            var session = new TestSession(container, bridge);

            return new WorkerSlot(index, session);
        }
    }
}
