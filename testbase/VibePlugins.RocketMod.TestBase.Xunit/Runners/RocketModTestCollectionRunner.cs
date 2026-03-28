using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit.Runners
{
    /// <summary>
    /// Collection-level test runner that delegates class execution to
    /// <see cref="RocketModTestClassRunner"/>.
    /// </summary>
    public class RocketModTestCollectionRunner : XunitTestCollectionRunner
    {
        private readonly IMessageSink _diagnosticSink;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestCollectionRunner"/>.
        /// </summary>
        public RocketModTestCollectionRunner(
            ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ITestCaseOrderer testCaseOrderer,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            : base(testCollection, testCases, diagnosticMessageSink, messageBus,
                   testCaseOrderer, aggregator, cancellationTokenSource)
        {
            _diagnosticSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Creates a <see cref="RocketModTestClassRunner"/> for each test class in the collection.
        /// </summary>
        protected override Task<RunSummary> RunTestClassAsync(
            ITestClass testClass,
            IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases)
        {
            return new RocketModTestClassRunner(
                testClass,
                @class,
                testCases,
                _diagnosticSink,
                MessageBus,
                TestCaseOrderer,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource,
                CollectionFixtureMappings)
                .RunAsync();
        }
    }
}
