using System;

namespace VibePlugins.RocketMod.TestBase.Xunit
{
    /// <summary>
    /// Assembly-level attribute that configures the number of server worker containers
    /// available for parallel test execution. Defaults to 1.
    /// </summary>
    /// <example>
    /// <code>[assembly: ServerWorker(Count = 2)]</code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ServerWorkerAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the number of server worker containers to use.
        /// Defaults to 1.
        /// </summary>
        public int Count { get; set; } = 1;
    }

    /// <summary>
    /// Marks a test as runnable without a container. When containers are unavailable
    /// (e.g., in CI without Docker), tests without this attribute may be skipped.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SkipContainerAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a test as requiring a running container. When containers are unavailable,
    /// tests with this attribute will be skipped with a diagnostic message.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequiresContainerAttribute : Attribute
    {
    }

    /// <summary>
    /// Provides the assembly-level attribute string constants for registering the
    /// <see cref="RocketModTestFramework"/>. Usage:
    /// <code>[assembly: TestFramework(RocketModTestFrameworkConstants.TypeName, RocketModTestFrameworkConstants.AssemblyName)]</code>
    /// Or simply:
    /// <code>[assembly: TestFramework("VibePlugins.RocketMod.TestBase.Xunit.RocketModTestFramework", "VibePlugins.RocketMod.TestBase.Xunit")]</code>
    /// </summary>
    public static class RocketModTestFrameworkConstants
    {
        /// <summary>The fully qualified type name of the test framework.</summary>
        public const string TypeName = "VibePlugins.RocketMod.TestBase.Xunit.RocketModTestFramework";

        /// <summary>The assembly name containing the test framework.</summary>
        public const string AssemblyName = "VibePlugins.RocketMod.TestBase.Xunit";
    }
}
