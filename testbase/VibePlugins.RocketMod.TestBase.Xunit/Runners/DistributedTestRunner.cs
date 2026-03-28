using System;
using System.Collections.Concurrent;
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
    /// Implements a work-stealing distributed test execution algorithm. Tests are placed
    /// in a shared queue and N worker tasks each dequeue and execute one test at a time.
    /// Each worker operates on its own <see cref="WorkerSlot"/> with an independent server
    /// container, ensuring full isolation between concurrently executing tests.
    /// </summary>
    internal static class DistributedTestRunner
    {
        /// <summary>
        /// Runs all test cases distributed across the workers in the container pool.
        /// Each worker dequeues tests from a shared queue and runs them one at a time.
        /// </summary>
        /// <param name="pool">The container pool providing worker slots.</param>
        /// <param name="testCases">All test cases to execute.</param>
        /// <param name="diagnosticSink">Sink for diagnostic messages.</param>
        /// <param name="messageBus">The xUnit message bus for reporting test results.</param>
        /// <param name="testCaseOrderer">Orderer used to sort test cases.</param>
        /// <param name="aggregator">Exception aggregator for collecting errors.</param>
        /// <param name="cancellationTokenSource">Cancellation token source for stopping execution.</param>
        /// <returns>A combined <see cref="RunSummary"/> aggregating results from all workers.</returns>
        public static async Task<RunSummary> RunDistributedAsync(
            ContainerPool pool,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticSink,
            IMessageBus messageBus,
            ITestCaseOrderer testCaseOrderer,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            // Enqueue all test cases in the order determined by the orderer
            IEnumerable<IXunitTestCase> orderedCases = testCaseOrderer != null
                ? testCaseOrderer.OrderTestCases(testCases)
                : testCases;

            var queue = new ConcurrentQueue<IXunitTestCase>(orderedCases);

            diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[DistributedTestRunner] Distributing {queue.Count} test(s) across {pool.WorkerCount} worker(s)."));

            // Launch one task per worker
            var workerTasks = new Task<RunSummary>[pool.WorkerCount];
            for (int i = 0; i < pool.WorkerCount; i++)
            {
                WorkerSlot worker = pool.GetWorker(i);
                workerTasks[i] = RunWorkerLoopAsync(
                    worker,
                    queue,
                    diagnosticSink,
                    messageBus,
                    aggregator,
                    cancellationTokenSource);
            }

            RunSummary[] workerSummaries = await Task.WhenAll(workerTasks).ConfigureAwait(false);

            // Aggregate results
            var combined = new RunSummary();
            foreach (RunSummary summary in workerSummaries)
            {
                combined.Aggregate(summary);
            }

            diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[DistributedTestRunner] Complete: {combined.Total} total, " +
                $"{combined.Failed} failed, {combined.Skipped} skipped."));

            return combined;
        }

        /// <summary>
        /// Worker loop: dequeues test cases one at a time and executes them on the given worker slot.
        /// Continues until the queue is empty or cancellation is requested. Individual test failures
        /// do not stop the worker from processing remaining tests.
        /// </summary>
        private static async Task<RunSummary> RunWorkerLoopAsync(
            WorkerSlot worker,
            ConcurrentQueue<IXunitTestCase> queue,
            IMessageSink diagnosticSink,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            var workerSummary = new RunSummary();

            while (!cancellationTokenSource.IsCancellationRequested && queue.TryDequeue(out IXunitTestCase testCase))
            {
                diagnosticSink.OnMessage(new DiagnosticMessage(
                    $"[Worker {worker.Index}] Picked up: {testCase.DisplayName}"));

                try
                {
                    var caseRunner = new WorkerTestCaseRunner(
                        worker,
                        testCase,
                        diagnosticSink,
                        messageBus,
                        new ExceptionAggregator(aggregator),
                        cancellationTokenSource);

                    RunSummary caseSummary = await caseRunner.RunAsync().ConfigureAwait(false);
                    workerSummary.Aggregate(caseSummary);
                }
                catch (Exception ex)
                {
                    diagnosticSink.OnMessage(new DiagnosticMessage(
                        $"[Worker {worker.Index}] Unhandled exception running {testCase.DisplayName}: " +
                        $"{ex.GetType().Name}: {ex.Message}"));

                    // Record the failure but keep the worker alive
                    workerSummary.Total++;
                    workerSummary.Failed++;

                    // Report the failure through the message bus
                    try
                    {
                        var test = new XunitTest(testCase, testCase.DisplayName);
                        messageBus.QueueMessage(new TestFailed(test, 0, null, ex));
                    }
                    catch (Exception reportEx)
                    {
                        diagnosticSink.OnMessage(new DiagnosticMessage(
                            $"[Worker {worker.Index}] Failed to report test failure: {reportEx.Message}"));
                    }
                }

                // Run cleanup between tests, but don't let cleanup failure kill the worker
                try
                {
                    await worker.RunCleanupAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    diagnosticSink.OnMessage(new DiagnosticMessage(
                        $"[Worker {worker.Index}] Cleanup failed after {testCase.DisplayName}: {ex.Message}"));
                }
            }

            diagnosticSink.OnMessage(new DiagnosticMessage(
                $"[Worker {worker.Index}] Finished: {workerSummary.Total} test(s), " +
                $"{workerSummary.Failed} failed."));

            return workerSummary;
        }
    }
}
