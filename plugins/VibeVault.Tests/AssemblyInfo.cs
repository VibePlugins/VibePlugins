using VibePlugins.RocketMod.TestBase.Xunit;
using Xunit;

[assembly: TestFramework(RocketModTestFrameworkConstants.TypeName, RocketModTestFrameworkConstants.AssemblyName)]
[assembly: ServerWorker(Count = 1)]
