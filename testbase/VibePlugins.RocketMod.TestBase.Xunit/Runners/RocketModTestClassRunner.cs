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
    /// Class-level test runner that delegates method execution to
    /// <see cref="RocketModTestMethodRunner"/>.
    /// </summary>
    public class RocketModTestClassRunner : XunitTestClassRunner
    {
        private readonly IMessageSink _diagnosticSink;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestClassRunner"/>.
        /// </summary>
        public RocketModTestClassRunner(
            ITestClass testClass,
            IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ITestCaseOrderer testCaseOrderer,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource,
            IDictionary<Type, object> collectionFixtureMappings)
            : base(testClass, @class, testCases, diagnosticMessageSink, messageBus,
                   testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
        {
            _diagnosticSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Creates a <see cref="RocketModTestMethodRunner"/> for each test method.
        /// </summary>
        protected override Task<RunSummary> RunTestMethodAsync(
            ITestMethod testMethod,
            IReflectionMethodInfo method,
            IEnumerable<IXunitTestCase> testCases,
            object[] constructorArguments)
        {
            return new RocketModTestMethodRunner(
                testMethod,
                Class,
                method,
                testCases,
                _diagnosticSink,
                MessageBus,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource,
                constructorArguments)
                .RunAsync();
        }
    }
}
