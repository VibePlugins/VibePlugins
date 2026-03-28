# VibeVault

Containerized integration testing framework for RocketMod Unturned plugins. Runs a real Unturned dedicated server inside Docker, deploys your plugin, and lets you execute commands, monitor events, and create mock entities from xUnit tests.

## Quick Example

```csharp
public class HealCommandTests : RocketModPluginTestBase<ExamplePlugin>
{
    [Fact]
    public async Task Heal_WithTarget_HealsTargetPlayer()
    {
        var caller = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin().WithHealth(100));
        var target = await CreatePlayerAsync(p => p.WithName("Wounded").WithHealth(30).WithMaxHealth(100));

        var result = await ExecuteCommand("heal")
            .AsPlayer(caller.HandleId)
            .WithArgs("Wounded", "50")
            .RunAsync();

        result.ShouldSucceed()
              .ShouldContainMessage("Healed")
              .ShouldContainMessage("Wounded")
              .ShouldContainMessage("50");
    }
}
```

## Available Plugins

### Core Packages (`testbase/`)

| Plugin / Package | Description |
|---|---|
| `VibePlugins.RocketMod.TestBase` | Core testing framework with fluent APIs for commands, events, mocks, and scenarios |
| `VibePlugins.RocketMod.TestBase.Xunit` | xUnit integration with custom test framework and distributed test runners |
| `VibePlugins.RocketMod.TestBase.Shared` | Protocol types and shared DTOs used by the test host and harness |
| `VibePlugins.RocketMod.TestBase.Tools` | CLI tool for building and managing the Docker container image |
| `VibePlugins.RocketMod.TestHarness` | Server-side harness plugin that runs inside the Unturned container |

### Examples (`examples/`)

| Example | Description |
|---|---|
| `ExamplePlugin` | Sample RocketMod plugin demonstrating commands, events, and configuration |
| `ExamplePlugin.Tests` | Integration test suite showing all testing framework features |

## Features

- Fluent command testing with chained assertions
- Event monitoring with predicates and negative assertions
- Mock entity creation (players, zombies, animals)
- BDD-style scenario builder (Given/When/Then)
- Remote code execution on the server
- Game tick/frame waiting
- Container configuration with MySQL and Redis sidecars
- Distributed testing across parallel server containers
- CI/CD integration with conditional container support

## Getting Started

1. Install [Docker](https://www.docker.com/products/docker-desktop/).

2. Build the container image:
   ```bash
   dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image
   ```

3. Create a test project (`MyPlugin.Tests.csproj`):
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net48</TargetFramework>
       <IsPackable>false</IsPackable>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
       <PackageReference Include="xunit" Version="2.9.3" />
       <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
       <ProjectReference Include="..\path\to\VibePlugins.RocketMod.TestBase.csproj" />
       <ProjectReference Include="..\path\to\VibePlugins.RocketMod.TestBase.Xunit.csproj" />
       <ProjectReference Include="..\path\to\MyPlugin.csproj" />
     </ItemGroup>
   </Project>
   ```

4. Add assembly attributes (create `Properties/AssemblyInfo.cs`):
   ```csharp
   using Xunit;
   using VibePlugins.RocketMod.TestBase.Xunit;

   [assembly: TestFramework(
       RocketModTestFrameworkConstants.TypeName,
       RocketModTestFrameworkConstants.AssemblyName)]
   [assembly: ServerWorker(Count = 1)]
   ```

5. Write a test class inheriting `RocketModPluginTestBase<TPlugin>` (see example above).

6. Run tests:
   ```bash
   dotnet test
   ```

See [Getting Started](docs/getting-started.md) for the full setup guide.

## Documentation

Full documentation is also available at [vibevault.github.io/vibevault](https://vibevault.github.io/vibevault/).

- [Getting Started](docs/getting-started.md) -- prerequisites, project setup, first test
- [Command Testing](docs/command-testing.md) -- fluent builder, assertions, caller configuration
- [Event Testing](docs/event-testing.md) -- monitoring events, predicates, negative assertions
- [Mock Entities](docs/mock-entities.md) -- creating players, zombies, animals
- [Scenario Builder](docs/scenario-builder.md) -- Given/When/Then multi-step tests
- [Container Configuration](docs/container-configuration.md) -- server flags, MySQL, Redis, sidecars
- [Distributed Testing](docs/distributed-testing.md) -- parallel execution with server workers
- [CI/CD Integration](docs/ci-cd-integration.md) -- GitHub Actions, conditional tests
- [Troubleshooting](docs/troubleshooting.md) -- common errors, diagnostics, FAQ

## Project Structure

```
testbase/
  VibePlugins.RocketMod.TestBase/           Core testing framework
  VibePlugins.RocketMod.TestBase.Shared/    Protocol types shared with harness
  VibePlugins.RocketMod.TestBase.Xunit/     xUnit framework integration
  VibePlugins.RocketMod.TestBase.Tools/     CLI tools (build-image)
  VibePlugins.RocketMod.TestHarness/        Server-side harness plugin
examples/
  ExamplePlugin/                            Sample RocketMod plugin
  ExamplePlugin.Tests/                      Integration tests for the sample plugin
references/
  harmony/                                  Harmony library (submodule)
  rocketmod/                                RocketMod framework (submodule)
  unturned/                                 Unturned assemblies (submodule)
```

## License

MIT
