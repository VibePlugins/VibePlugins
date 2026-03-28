---
uid: container-configuration
---

# Container Configuration

Customize the Docker container running the Unturned server for your tests.

## Basic Usage

Override `ConfigureContainer` in your test class:

```csharp
public class MyTests : RocketModPluginTestBase<MyPlugin>
{
    protected override void ConfigureContainer(UnturnedContainerBuilder builder)
    {
        builder
            .WithSkipAssets()
            .WithOfflineOnly()
            .WithMySql(opts => opts.WithDatabase("myplugin"));
    }
}
```

## Server Flags

| Method | Unturned Flag | Description |
|---|---|---|
| `WithSkipAssets(bool)` | `-SkipAssets` | Skip loading asset bundles. Reduces startup time. |
| `WithOfflineOnly()` | `-OfflineOnly` | Run without Steam networking. |
| `WithNoWebRequests()` | `-NoWebRequests` | Prevent outbound HTTP requests. |
| `WithMap(string)` | `+InternetServer/<map>` | Set the map name. Default: `"PEI"`. |
| `WithMaxPlayersLimit(int)` | `-MaxPlayersLimit` | Cap the maximum player count. |
| `WithGameplayConfigFile(string)` | `-GameplayConfigFile` | Path to a gameplay config file inside the container. |
| `WithLogGameplayConfig()` | `-LogGameplayConfig` | Log gameplay configuration on startup. |
| `WithConstNetEvents()` | `-ConstNetEvents` | Deterministic networking events. |
| `WithCustomFlag(string)` | (any) | Add an arbitrary CLI flag. |

## Image and Network

| Method | Default | Description |
|---|---|---|
| `WithImage(string)` | `"vibeplugins:rocketmod"` | Docker image to use. |
| `WithBridgePort(int)` | `27099` | TCP port for the test bridge inside the container. |
| `WithServerName(string)` | `"TestServer"` | Unturned server directory name. |

## MySQL Sidecar

Add a MySQL 8.0 container alongside the Unturned server:

```csharp
protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    builder.WithMySql(opts => opts
        .WithDatabase("myplugin_db")
        .WithUsername("root")
        .WithPassword("secret")
        .WithPort(3306));
}
```

### MySqlSidecarOptions Reference

| Method | Default | Description |
|---|---|---|
| `WithDatabase(string)` | `"test"` | Database name to create |
| `WithUsername(string)` | `"root"` | MySQL username |
| `WithPassword(string)` | `"test"` | MySQL password |
| `WithPort(int)` | `3306` | Port to expose |

Access the connection string in your tests:

```csharp
[Fact]
public async Task PluginStoresData()
{
    Assert.True(HasMySql);
    string connStr = MySqlConnectionString;
    // Use connStr to verify database state
}
```

## Redis Sidecar

Add a Redis 7 container:

```csharp
protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    builder.WithRedis();
}
```

## Custom Sidecar Containers

Add any Testcontainers `IContainer`:

```csharp
protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    var mongoContainer = new ContainerBuilder()
        .WithImage("mongo:7")
        .WithPortBinding(27017, true)
        .Build();

    builder.WithAdditionalContainer("mongo", mongoContainer);
}
```

## Cleanup Callbacks

Register callbacks that run when the container is disposed:

```csharp
protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    builder.WithCleanupCallback(async () =>
    {
        // Runs during container disposal
    });
}
```

## Timeouts

Override these properties in your test class to adjust wait times:

| Property | Default | Description |
|---|---|---|
| `HarnessReadyTimeout` | 120 seconds | Time to wait for the test harness to signal readiness |
| `PluginLoadTimeout` | 60 seconds | Time to wait for the plugin to finish loading |

```csharp
protected override TimeSpan HarnessReadyTimeout => TimeSpan.FromSeconds(180);
protected override TimeSpan PluginLoadTimeout => TimeSpan.FromSeconds(90);
```

## Container Image

The default image `vibeplugins:rocketmod` is built by the CLI tool:

```bash
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image
```

The image contains:
- SteamCMD (used to install the Unturned Dedicated Server)
- Unturned Dedicated Server
- RocketMod framework
- The test harness plugin (`VibePlugins.RocketMod.TestHarness`)

To rebuild the image from scratch:

```bash
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image --force
```
