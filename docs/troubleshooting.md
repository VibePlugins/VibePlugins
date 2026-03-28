---
uid: troubleshooting
---

# Troubleshooting

Common errors, diagnostics, and solutions.

## Common Errors

### PluginLoadFailedException

**Message:** `Plugin 'MyPlugin' failed to load: <error>`

**Causes:**
- Your plugin threw an exception during `Load()`.
- A required dependency assembly is missing from the plugin directory.
- The plugin class is not public or does not inherit from `RocketPlugin<T>`.

**Fix:** Check your plugin's `Load()` method for exceptions. Ensure all dependency DLLs are included in the build output. Verify the `TPlugin` type parameter matches your plugin class.

### ServerStartupFailedException

**Message:** `Server or harness failed to start`

**Causes:**
- Docker is not running.
- The container image `vibeplugins:rocketmod` has not been built.
- Insufficient system resources (CPU, memory, disk).

**Fix:**
```bash
# Verify Docker is running
docker info

# Rebuild the container image
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image --force
```

### TimeoutException (harness ready)

**Message:** `Timed out waiting for harness ready after 120.0s`

**Causes:**
- The Unturned server is taking too long to start.
- The container does not have enough resources.
- The test harness plugin failed to load inside the server.

**Fix:** Increase the timeout or reduce startup time:
```csharp
protected override TimeSpan HarnessReadyTimeout => TimeSpan.FromSeconds(180);

protected override void ConfigureContainer(UnturnedContainerBuilder builder)
{
    builder.WithSkipAssets(); // Skip asset loading for faster startup
}
```

### TimeoutException (event wait)

**Message:** `Timed out waiting for event 'ChatMessageEvent' after 10.0s`

**Causes:**
- The expected event was never fired by the plugin.
- The event monitor was started **after** the event already fired.
- The predicate does not match the actual event data.

**Fix:**
- Verify your plugin logic actually fires the event.
- Start monitoring **before** the action that triggers the event.
- Log or inspect the event data to check the predicate matches.
- Increase the timeout if the event is delayed:
  ```csharp
  var evt = await monitor.WaitForAsync(predicate, timeout: TimeSpan.FromSeconds(30));
  ```

### BridgeConnectionFailedException

**Message:** `TCP bridge cannot connect to container`

**Causes:**
- The bridge port is not exposed correctly.
- The container crashed before the harness started listening.
- Port conflict on the host.

**Fix:** Check container health with `docker ps` and `docker logs <container-id>`. The bridge port is mapped dynamically, so port conflicts are rare. If the container is crashing, check its logs for errors.

### RemoteExecutionException

**Message:** `Remote execution of TypeName.MethodName failed: ExceptionType: message`

**Causes:**
- The method invoked via `RunOnServerAsync` threw an exception on the server side.

**Fix:** Check the exception type and message in the error. The server-side stack trace is included in the exception info.

### OperationCanceledException

**Message:** `The operation was canceled`

**Causes:**
- A bridge request timed out (default 30 seconds for commands).

**Fix:** Increase the command timeout:
```csharp
var result = await ExecuteCommand("slow-command")
    .WithTimeout(TimeSpan.FromSeconds(60))
    .RunAsync();
```

## Diagnostics

### Container Logs

View logs from the Unturned server container:

```bash
docker ps                    # Find the container ID
docker logs <container-id>   # View server output
```

### Docker Availability

Check Docker status and the environment variable:

```bash
docker info
echo $VIBEPLUGINS_ENABLE_CONTAINERS
```

### Image Issues

If tests fail on container creation, rebuild the image:

```bash
dotnet run --project testbase/VibePlugins.RocketMod.TestBase.Tools -- build-image --force
```

### Plugin Not Loading

- Verify `TPlugin` is a public class inheriting from `RocketPlugin<T>`.
- Verify the assembly containing `TPlugin` is referenced in your test project.
- Check that all plugin dependencies are resolvable at runtime.
- Check the container logs for plugin load errors.

## FAQ

**Can I test multiple plugins at once?**
Each test class targets one plugin via the `TPlugin` type parameter. To test interactions between plugins, deploy both assemblies by overriding the deployment step or use `RunOnServerAsync` to invoke code from the second plugin.

**How do I test plugins that use MySQL?**
Configure a MySQL sidecar in `ConfigureContainer`:
```csharp
builder.WithMySql(opts => opts.WithDatabase("myplugin"));
```
Access the connection string via the `MySqlConnectionString` property and `HasMySql` to check availability.

**Tests pass locally but fail in CI?**
- Check Docker availability on the CI runner.
- Ensure `VIBEPLUGINS_ENABLE_CONTAINERS` is set correctly.
- Windows runners are required for `net48` targets.
- CI runners may have less memory -- reduce `ServerWorker` count or add `WithSkipAssets()`.

**How do I debug a test that hangs?**
- Check container logs for server-side errors.
- Reduce timeouts to fail faster and see which operation is blocking.
- Run with `--verbosity diagnostic` for detailed xUnit output.
- Verify the plugin loaded successfully by checking for `PluginLoadFailedException`.
