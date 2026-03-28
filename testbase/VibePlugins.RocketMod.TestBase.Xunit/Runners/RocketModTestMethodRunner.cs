using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit.Runners
{
    /// <summary>
    /// Method-level test runner that delegates individual test case execution to
    /// <see cref="RocketModTestCaseRunner"/>.
    /// </summary>
    public class RocketModTestMethodRunner : XunitTestMethodRunner
    {
        private readonly IMessageSink _diagnosticSink;
        private readonly object[] _constructorArguments;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestMethodRunner"/>.
        /// </summary>
        public RocketModTestMethodRunner(
            ITestMethod testMethod,
            IReflectionTypeInfo @class,
            IReflectionMethodInfo method,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource,
            object[] constructorArguments)
            : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus,
                   aggregator, cancellationTokenSource, constructorArguments)
        {
            _diagnosticSink = diagnosticMessageSink;
            _constructorArguments = constructorArguments;
        }

        /// <summary>
        /// Runs a single test case using <see cref="RocketModTestCaseRunner"/>.
        /// </summary>
        protected override Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
        {
            return new RocketModTestCaseRunner(
                testCase,
                testCase.DisplayName,
                testCase.SkipReason,
                _constructorArguments,
                testCase.TestMethodArguments,
                _diagnosticSink,
                MessageBus,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource)
                .RunAsync();
        }
    }
}
