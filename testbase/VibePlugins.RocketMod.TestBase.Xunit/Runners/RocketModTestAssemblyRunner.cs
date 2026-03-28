using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Containers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit.Runners
{
    /// <summary>
    /// Assembly-level test runner that ensures the Docker container image is built
    /// before any tests execute. Reads <see cref="ServerWorkerAttribute"/> to determine
    /// the configured worker pool size. When the worker count is greater than 1, creates
    /// a <see cref="ContainerPool"/> and uses <see cref="DistributedTestRunner"/> for
    /// parallel work-stealing execution across multiple server instances.
    /// </summary>
    public class RocketModTestAssemblyRunner : XunitTestAssemblyRunner
    {
        private readonly IMessageSink _diagnosticSink;
        private int _workerCount = 1;
        private ContainerPool _pool;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestAssemblyRunner"/>.
        /// </summary>
        public RocketModTestAssemblyRunner(
            ITestAssembly testAssembly,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions)
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        {
            _diagnosticSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Gets the configured worker count from the <see cref="ServerWorkerAttribute"/>
        /// on the test assembly.
        /// </summary>
        public int WorkerCount => _workerCount;

        /// <summary>
        /// Override that reads configuration attributes, ensures the container image
        /// exists, optionally creates a container pool, and then routes to distributed
        /// execution or falls back to normal sequential collection execution.
        /// </summary>
        /// <remarks>
        /// In xUnit 2.9.x, <c>RunAsync()</c> is not virtual, so all initialization
        /// is performed here in <c>RunTestCollectionsAsync</c> which IS virtual.
        /// </remarks>
        protected override async Task<RunSummary> RunTestCollectionsAsync(
            IMessageBus messageBus,
            CancellationTokenSource cancellationTokenSource)
        {
            // 1. Read [assembly: ServerWorker(Count = N)] attribute
            var serverWorkerAttrs = TestAssembly.Assembly
                .GetCustomAttributes(typeof(ServerWorkerAttribute))
                .ToList();

            if (serverWorkerAttrs.Count > 0)
            {
                var attr = serverWorkerAttrs[0];
                _workerCount = attr.GetNamedArgument<int>(nameof(ServerWorkerAttribute.Count));
                if (_workerCount < 1)
                    _workerCount = 1;
            }

            _diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[RocketModTestAssemblyRunner] Worker count: {_workerCount}"));

            // 2. Ensure the container image is built (only if containers are available)
            if (ContainerSupport.IsAvailable)
            {
                try
                {
                    _diagnosticSink.OnMessage(new DiagnosticMessage(
                        "[RocketModTestAssemblyRunner] Ensuring container image exists..."));

                    // TODO: ContainerImageBuilder writes build progress to Console.WriteLine,
                    // which does not appear in VS Test Explorer output. Consider accepting an
                    // IMessageSink or Action<string> callback to forward build output through
                    // the xUnit diagnostic sink for better visibility during test runs.
                    await ContainerImageBuilder.EnsureImageExistsAsync(
                        cancellationToken: cancellationTokenSource.Token).ConfigureAwait(false);

                    _diagnosticSink.OnMessage(new DiagnosticMessage(
                        "[RocketModTestAssemblyRunner] Container image is ready."));
                }
                catch (Exception ex)
                {
                    _diagnosticSink.OnMessage(new DiagnosticMessage(
                        $"[RocketModTestAssemblyRunner] Failed to build container image: {ex.Message}"));
                    throw;
                }

                // 3. Create container pool if distributed mode is requested
                if (_workerCount > 1)
                {
                    try
                    {
                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            $"[RocketModTestAssemblyRunner] Creating container pool with {_workerCount} workers..."));

                        _pool = await ContainerPool.CreateAsync(
                            _workerCount,
                            configureContainer: null,
                            ct: cancellationTokenSource.Token).ConfigureAwait(false);

                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            $"[RocketModTestAssemblyRunner] Container pool ready with {_pool.WorkerCount} workers."));
                    }
                    catch (Exception ex)
                    {
                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            $"[RocketModTestAssemblyRunner] Failed to create container pool: {ex.Message}"));
                        throw;
                    }
                }
            }
            else
            {
                _diagnosticSink.OnMessage(new DiagnosticMessage(
                    "[RocketModTestAssemblyRunner] Container support unavailable; " +
                    "skipping image build. Only [SkipContainer] tests will run."));
            }

            // 4. Delegate to the appropriate execution path
            try
            {
                if (_pool != null && _workerCount > 1)
                {
                    // Distributed mode: flatten all test cases from all collections and
                    // run them through the work-stealing distributed runner
                    _diagnosticSink.OnMessage(new DiagnosticMessage(
                        "[RocketModTestAssemblyRunner] Using distributed test execution."));

                    return await DistributedTestRunner.RunDistributedAsync(
                        _pool,
                        TestCases,
                        _diagnosticSink,
                        messageBus,
                        TestCaseOrderer,
                        Aggregator,
                        cancellationTokenSource).ConfigureAwait(false);
                }

                // Sequential mode: use the standard collection-based execution
                return await base.RunTestCollectionsAsync(messageBus, cancellationTokenSource)
                    .ConfigureAwait(false);
            }
            finally
            {
                // Dispose the pool after all tests complete
                if (_pool != null)
                {
                    try
                    {
                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            "[RocketModTestAssemblyRunner] Disposing container pool..."));
                        await _pool.DisposeAsync().ConfigureAwait(false);
                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            "[RocketModTestAssemblyRunner] Container pool disposed."));
                    }
                    catch (Exception ex)
                    {
                        _diagnosticSink.OnMessage(new DiagnosticMessage(
                            $"[RocketModTestAssemblyRunner] Pool disposal error: {ex.Message}"));
                    }
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="RocketModTestCollectionRunner"/> for each test collection.
        /// Used in sequential (non-distributed) mode.
        /// </summary>
        protected override Task<RunSummary> RunTestCollectionAsync(
            IMessageBus messageBus,
            ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            CancellationTokenSource cancellationTokenSource)
        {
            return new RocketModTestCollectionRunner(
                testCollection,
                testCases,
                _diagnosticSink,
                messageBus,
                TestCaseOrderer,
                new ExceptionAggregator(Aggregator),
                cancellationTokenSource)
                .RunAsync();
        }
    }
}
