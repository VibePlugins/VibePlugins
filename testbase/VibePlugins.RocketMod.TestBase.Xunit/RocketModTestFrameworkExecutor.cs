using System.Collections.Generic;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit
{
    /// <summary>
    /// Custom test framework executor that creates <see cref="Runners.RocketModTestAssemblyRunner"/>
    /// to manage the assembly-level container image build and test execution lifecycle.
    /// </summary>
    public class RocketModTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestFrameworkExecutor"/>.
        /// </summary>
        /// <param name="assemblyName">The name of the test assembly.</param>
        /// <param name="sourceInformationProvider">Provides source file information.</param>
        /// <param name="diagnosticMessageSink">Sink for diagnostic messages.</param>
        public RocketModTestFrameworkExecutor(
            AssemblyName assemblyName,
            ISourceInformationProvider sourceInformationProvider,
            IMessageSink diagnosticMessageSink)
            : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
        {
        }

        /// <summary>
        /// Runs the given test cases by creating a <see cref="Runners.RocketModTestAssemblyRunner"/>.
        /// </summary>
        /// <param name="testCases">The test cases to run.</param>
        /// <param name="executionMessageSink">Sink for execution messages.</param>
        /// <param name="executionOptions">Execution options.</param>
        protected override async void RunTestCases(
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions)
        {
            using (var runner = new Runners.RocketModTestAssemblyRunner(
                TestAssembly,
                testCases,
                DiagnosticMessageSink,
                executionMessageSink,
                executionOptions))
            {
                await runner.RunAsync().ConfigureAwait(false);
            }
        }
    }
}
