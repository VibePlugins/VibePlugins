using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Containers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit.Runners
{
    /// <summary>
    /// Runs a single test case on a specific <see cref="WorkerSlot"/>. Handles the full
    /// lifecycle: preparing the worker (deploy plugin, restart, wait for load), creating
    /// the test class instance with the worker's bridge injected via <see cref="WorkerSlotContext"/>,
    /// executing the test method, and returning results.
    /// </summary>
    internal class WorkerTestCaseRunner
    {
        private readonly WorkerSlot _worker;
        private readonly IXunitTestCase _testCase;
        private readonly IMessageSink _diagnosticSink;
        private readonly IMessageBus _messageBus;
        private readonly ExceptionAggregator _aggregator;
        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of <see cref="WorkerTestCaseRunner"/>.
        /// </summary>
        /// <param name="worker">The worker slot to execute the test on.</param>
        /// <param name="testCase">The xUnit test case to run.</param>
        /// <param name="diagnosticSink">Sink for diagnostic messages.</param>
        /// <param name="messageBus">The xUnit message bus for reporting results.</param>
        /// <param name="aggregator">Exception aggregator.</param>
        /// <param name="cancellationTokenSource">Cancellation token source.</param>
        public WorkerTestCaseRunner(
            WorkerSlot worker,
            IXunitTestCase testCase,
            IMessageSink diagnosticSink,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _testCase = testCase ?? throw new ArgumentNullException(nameof(testCase));
            _diagnosticSink = diagnosticSink ?? throw new ArgumentNullException(nameof(diagnosticSink));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
        }

        /// <summary>
        /// Runs the test case: prepares the worker, sets the <see cref="WorkerSlotContext"/>,
        /// and delegates to the standard xUnit test case runner. The worker slot context is set
        /// via <see cref="AsyncLocal{T}"/> so that <c>RocketModPluginTestBase.InitializeAsync</c>
        /// can detect it and skip its own container creation.
        /// </summary>
        /// <returns>A <see cref="RunSummary"/> with the test outcome.</returns>
        public async Task<RunSummary> RunAsync()
        {
            string testName = _testCase.DisplayName;
            var stopwatch = Stopwatch.StartNew();

            // Determine the plugin assembly from the test class
            Type testClassType = _testCase.TestMethod.TestClass.Class.ToRuntimeType();
            Assembly pluginAssembly = ResolvePluginAssembly(testClassType);
            string pluginName = ResolvePluginName(testClassType);

            _diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[WorkerTestCaseRunner] Worker {_worker.Index} preparing for: {testName} (plugin: {pluginName})"));

            // 1. Prepare the worker: deploy plugin, restart, wait for load
            await _worker.PrepareForTestAsync(
                pluginAssembly,
                pluginName,
                harnessTimeout: TimeSpan.FromSeconds(120),
                pluginLoadTimeout: TimeSpan.FromSeconds(60),
                ct: _cancellationTokenSource.Token).ConfigureAwait(false);

            // 2. Set the worker slot context so the test base picks it up
            WorkerSlotContext.Current = _worker;

            try
            {
                // 3. Run the test case through xUnit's standard pipeline
                var runner = new RocketModTestCaseRunner(
                    _testCase,
                    _testCase.DisplayName,
                    _testCase.SkipReason,
                    Array.Empty<object>(),
                    _testCase.TestMethodArguments,
                    _diagnosticSink,
                    _messageBus,
                    _aggregator,
                    _cancellationTokenSource);

                RunSummary summary = await runner.RunAsync().ConfigureAwait(false);

                stopwatch.Stop();

                string outcome = summary.Failed > 0 ? "FAILED"
                    : summary.Skipped > 0 ? "SKIPPED"
                    : "PASSED";

                _diagnosticSink.OnMessage(new DiagnosticMessage(
                    $"[WorkerTestCaseRunner] {outcome}: {testName} on worker {_worker.Index} " +
                    $"({stopwatch.Elapsed.TotalSeconds:F2}s)"));

                return summary;
            }
            finally
            {
                // Always clear the context after execution
                WorkerSlotContext.Current = null;
            }
        }

        /// <summary>
        /// Resolves the plugin assembly from the test class type. Walks the generic type
        /// hierarchy to find the <c>TPlugin</c> type parameter of <c>RocketModPluginTestBase{TPlugin}</c>.
        /// Falls back to the test class's own assembly.
        /// </summary>
        private static Assembly ResolvePluginAssembly(Type testClassType)
        {
            Type pluginType = FindPluginTypeParameter(testClassType);
            return pluginType?.Assembly ?? testClassType.Assembly;
        }

        /// <summary>
        /// Resolves the plugin name from the test class type. Extracts the <c>TPlugin</c>
        /// generic argument name from <c>RocketModPluginTestBase{TPlugin}</c>.
        /// </summary>
        private static string ResolvePluginName(Type testClassType)
        {
            Type pluginType = FindPluginTypeParameter(testClassType);
            return pluginType?.Name ?? testClassType.Name;
        }

        /// <summary>
        /// Walks the base type chain to find the concrete type argument for
        /// <c>RocketModPluginTestBase{TPlugin}</c>.
        /// </summary>
        private static Type FindPluginTypeParameter(Type type)
        {
            Type current = type;
            while (current != null)
            {
                if (current.IsGenericType)
                {
                    Type genericDef = current.GetGenericTypeDefinition();
                    if (genericDef.FullName != null &&
                        genericDef.FullName.StartsWith(
                            "VibePlugins.RocketMod.TestBase.RocketModPluginTestBase`1",
                            StringComparison.Ordinal))
                    {
                        return current.GetGenericArguments()[0];
                    }
                }

                current = current.BaseType;
            }

            return null;
        }
    }
}
