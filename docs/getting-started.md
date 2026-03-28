---
uid: getting-started
---

# Getting Started

Set up and run your first integration test for a RocketMod plugin.

## Prerequisites

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- .NET Framework 4.8 targeting pack (Windows) or Mono (Linux/macOS)
- [Docker](https://www.docker.com/products/docker-desktop/) -- Docker Desktop on Windows/macOS, Docker Engine on Linux

## Building the Container Image

The framework runs an Unturned dedicated server with RocketMod inside a Docker container. Build the image first:

```bash
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image
```

This creates the `vibeplugins:rocketmod` Docker image containing SteamCMD, the Unturned Dedicated Server, and RocketMod. To force a rebuild:

```bash
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image --force
```

## Creating a Test Project

Create an SDK-style project targeting `net48`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>MyPlugin.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\path\to\VibePlugins.RocketMod.TestBase.csproj" />
    <ProjectReference Include="..\path\to\VibePlugins.RocketMod.TestBase.Xunit.csproj" />
    <ProjectReference Include="..\path\to\MyPlugin.csproj" />
  </ItemGroup>
</Project>
```

## Assembly Attributes

Create `Properties/AssemblyInfo.cs` to register the custom test framework:

```csharp
using Xunit;
using VibePlugins.RocketMod.TestBase.Xunit;

[assembly: TestFramework(
    RocketModTestFrameworkConstants.TypeName,
    RocketModTestFrameworkConstants.AssemblyName)]
[assembly: ServerWorker(Count = 1)]
```

`ServerWorker(Count = 1)` runs tests against a single container. Increase the count for [distributed testing](distributed-testing.md).

## Your First Test

Inherit from `RocketModPluginTestBase<TPlugin>` where `TPlugin` is your plugin class:

```csharp
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using Xunit;

public class MyCommandTests : RocketModPluginTestBase<MyPlugin>
{
    [Fact]
    public async Task MyCommand_Executes_Successfully()
    {
        // Create a mock admin player
        var caller = await CreatePlayerAsync(p => p.WithName("Tester").AsAdmin());

        // Execute your command
        var result = await ExecuteCommand("mycommand")
            .AsPlayer(caller.HandleId)
            .WithArgs("arg1")
            .RunAsync();

        // Assert on the result
        result.ShouldSucceed()
              .ShouldContainMessage("expected output");
    }
}
```

## Running Tests

Run all tests:

```bash
dotnet test
```

Run a specific test:

```bash
dotnet test --filter "MyCommand_Executes_Successfully"
```

Verbose output:

```bash
dotnet test --verbosity normal
```

Control container support with the `VIBEPLUGINS_ENABLE_CONTAINERS` environment variable:

```bash
# Force containers on
VIBEPLUGINS_ENABLE_CONTAINERS=true dotnet test

# Force containers off (skips container-dependent tests)
VIBEPLUGINS_ENABLE_CONTAINERS=false dotnet test
```

## Test Lifecycle

Each test follows this lifecycle:

1. **Get or create container** -- a Docker container running the Unturned server is started (or reused from a previous test).
2. **Clean plugin directories** -- the Plugins/ and Libraries/ directories inside the container are cleared.
3. **Deploy plugin** -- your plugin assembly (from `typeof(TPlugin).Assembly`) is copied into the container.
4. **Reset MySQL** -- if a MySQL sidecar is configured, the database is reset.
5. **Restart server** -- the container is restarted to load the fresh plugin.
6. **Wait for harness ready** -- the test waits up to 120 seconds for the test harness inside the server to signal readiness.
7. **Wait for plugin load** -- the test waits up to 60 seconds for your plugin to finish loading.
8. **Run test** -- your test method executes, communicating with the server via TCP bridge.
9. **Cleanup** -- mocks are destroyed, event captures are cleared, and cleanup callbacks run.

Both timeout values can be overridden in your test class:

```csharp
protected override TimeSpan HarnessReadyTimeout => TimeSpan.FromSeconds(180);
protected override TimeSpan PluginLoadTimeout => TimeSpan.FromSeconds(90);
```

## Next Steps

- [Command Testing](command-testing.md) -- fluent builder API and assertions
- [Event Testing](event-testing.md) -- monitoring server events
- [Mock Entities](mock-entities.md) -- creating players, zombies, animals
- [Scenario Builder](scenario-builder.md) -- multi-step Given/When/Then tests
- [Container Configuration](container-configuration.md) -- server flags, MySQL, Redis
- [Distributed Testing](distributed-testing.md) -- parallel execution
- [CI/CD Integration](ci-cd-integration.md) -- GitHub Actions setup
- [Troubleshooting](troubleshooting.md) -- common errors and fixes
