using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit
{
    /// <summary>
    /// Custom xUnit test framework that provides container lifecycle management for
    /// RocketMod plugin integration tests. Register this framework via the assembly attribute:
    /// <code>[assembly: TestFramework("VibePlugins.RocketMod.TestBase.Xunit.RocketModTestFramework", "VibePlugins.RocketMod.TestBase.Xunit")]</code>
    /// </summary>
    public class RocketModTestFramework : TestFramework
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestFramework"/>.
        /// </summary>
        /// <param name="messageSink">The diagnostic message sink provided by xUnit.</param>
        public RocketModTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            messageSink.OnMessage(new DiagnosticMessage(
                "[RocketModTestFramework] Custom test framework initialized."));
        }

        /// <summary>
        /// Creates the test framework discoverer that locates test cases.
        /// </summary>
        /// <param name="assemblyInfo">The assembly to discover tests in.</param>
        /// <returns>A <see cref="RocketModTestFrameworkDiscoverer"/>.</returns>
        protected override ITestFrameworkDiscoverer CreateDiscoverer(
            IAssemblyInfo assemblyInfo)
        {
            return new RocketModTestFrameworkDiscoverer(
                assemblyInfo,
                SourceInformationProvider,
                DiagnosticMessageSink);
        }

        /// <summary>
        /// Creates the test framework executor that runs discovered test cases.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the tests.</param>
        /// <returns>A <see cref="RocketModTestFrameworkExecutor"/>.</returns>
        protected override ITestFrameworkExecutor CreateExecutor(
            AssemblyName assemblyName)
        {
            return new RocketModTestFrameworkExecutor(
                assemblyName,
                SourceInformationProvider,
                DiagnosticMessageSink);
        }
    }
}
