using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Xunit
{
    /// <summary>
    /// Custom test discoverer that extends the default xUnit discovery with awareness of
    /// <see cref="SkipContainerAttribute"/> and <see cref="RequiresContainerAttribute"/>.
    /// When container support is unavailable, tests marked with <see cref="RequiresContainerAttribute"/>
    /// are excluded from discovery, while tests marked with <see cref="SkipContainerAttribute"/>
    /// are always included.
    /// </summary>
    public class RocketModTestFrameworkDiscoverer : XunitTestFrameworkDiscoverer
    {
        private readonly IMessageSink _diagnosticSink;

        /// <summary>
        /// Initializes a new instance of <see cref="RocketModTestFrameworkDiscoverer"/>.
        /// </summary>
        /// <param name="assemblyInfo">The assembly to discover tests in.</param>
        /// <param name="sourceProvider">Provides source file information for test cases.</param>
        /// <param name="diagnosticMessageSink">Sink for diagnostic messages.</param>
        public RocketModTestFrameworkDiscoverer(
            IAssemblyInfo assemblyInfo,
            ISourceInformationProvider sourceProvider,
            IMessageSink diagnosticMessageSink)
            : base(assemblyInfo, sourceProvider, diagnosticMessageSink)
        {
            _diagnosticSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Determines whether a test case should be included in discovery based on
        /// container availability and the presence of container-related attributes.
        /// </summary>
        /// <param name="testCase">The test case to evaluate.</param>
        /// <returns><c>true</c> if the test case should be included; otherwise <c>false</c>.</returns>
        protected override bool IsValidTestClass(ITypeInfo type)
        {
            if (!base.IsValidTestClass(type))
                return false;

            // If containers are available, all test classes are valid.
            if (ContainerSupport.IsAvailable)
                return true;

            // If the class is marked [SkipContainer], it is always valid.
            bool hasSkipContainer = type.GetCustomAttributes(typeof(SkipContainerAttribute)).Any();
            if (hasSkipContainer)
                return true;

            // If the class is marked [RequiresContainer] and containers are unavailable, skip it.
            bool hasRequiresContainer = type.GetCustomAttributes(typeof(RequiresContainerAttribute)).Any();
            if (hasRequiresContainer)
            {
                _diagnosticSink.OnMessage(new DiagnosticMessage(
                    $"[RocketModTestFrameworkDiscoverer] Skipping class '{type.Name}': " +
                    "marked [RequiresContainer] but containers are not available."));
                return false;
            }

            // Default: include the test class.
            return true;
        }
    }
}
