using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit.Runners
{
    /// <summary>
    /// Test-case-level runner that wraps each test execution with diagnostic logging
    /// and timing information. The actual test instance lifecycle (including
    /// <see cref="IAsyncLifetime.InitializeAsync"/> and <see cref="IAsyncLifetime.DisposeAsync"/>
    /// on <c>RocketModPluginTestBase</c>) is managed automatically by xUnit's infrastructure.
    /// This runner adds framework-specific diagnostics around that execution.
    /// </summary>
    public class RocketModTestCaseRunner : XunitTestCaseRunner
    {
        private readonly IMessageSink _diagnosticSink;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestCaseRunner"/>.
        /// </summary>
        public RocketModTestCaseRunner(
            IXunitTestCase testCase,
            string displayName,
            string skipReason,
            object[] constructorArguments,
            object[] testMethodArguments,
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, constructorArguments,
                   testMethodArguments, messageBus, aggregator, cancellationTokenSource)
        {
            _diagnosticSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Runs the test case with diagnostic timing. If the test class derives from
        /// <c>RocketModPluginTestBase</c>, xUnit's built-in <see cref="IAsyncLifetime"/>
        /// handling will invoke <c>InitializeAsync</c> (container startup, plugin deploy)
        /// and <c>DisposeAsync</c> (mock cleanup, event capture reset) automatically.
        /// </summary>
        protected override async Task<RunSummary> RunTestAsync()
        {
            string testName = DisplayName;
            _diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[RocketModTestCaseRunner] Starting: {testName}"));

            var stopwatch = Stopwatch.StartNew();
            RunSummary summary;

            try
            {
                summary = await base.RunTestAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _diagnosticSink.OnMessage(new DiagnosticMessage(
                    $"[RocketModTestCaseRunner] FAILED: {testName} " +
                    $"({stopwatch.Elapsed.TotalSeconds:F2}s) — {ex.GetType().Name}: {ex.Message}"));
                throw;
            }

            stopwatch.Stop();

            string outcome = summary.Failed > 0 ? "FAILED"
                           : summary.Skipped > 0 ? "SKIPPED"
                           : "PASSED";

            _diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[RocketModTestCaseRunner] {outcome}: {testName} ({stopwatch.Elapsed.TotalSeconds:F2}s)"));

            return summary;
        }
    }
}
