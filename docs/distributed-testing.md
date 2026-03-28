---
uid: distributed-testing
---

# Distributed Testing

Run tests in parallel across multiple Docker containers to reduce total execution time.

## Basic Usage

Set the worker count in your assembly attributes:

```csharp
[assembly: ServerWorker(Count = 3)]
```

This creates 3 container workers. Test classes are distributed across workers and run in parallel.

## How It Works

1. The custom xUnit test framework creates a pool of N worker containers at the start of the test run.
2. Each worker runs a full Unturned server with RocketMod and the test harness.
3. Test classes are assigned to workers via load balancing.
4. All tests within a single test class run on the same worker, in sequence.
5. Different test classes run in parallel across different workers.

## Container Reuse

Containers are reused between test classes to avoid the cost of starting a new server for each class:

- The container is **not destroyed** between test classes. It is restarted.
- Plugin directories are cleaned and the new test class's plugin is deployed.
- The server restarts and waits for the harness and plugin to load again.

This means container startup cost is paid once per worker, not once per class.

## Standalone vs Distributed Mode

| | Standalone (Count = 1) | Distributed (Count > 1) |
|---|---|---|
| Container management | Handled in `InitializeAsync` | Handled by the distributed runner |
| Plugin deployment | Per-test lifecycle | Per-class, managed by worker |
| Cleanup | `DisposeAsync` sends DestroyAllMocks + ClearEventCaptures | Worker runner handles harness-level cleanup |
| Parallelism | Sequential | Test classes run in parallel |

In distributed mode, `InitializeAsync` detects that a worker slot is active and adopts the pre-configured session instead of managing its own container.

## Configuration

The only configuration is the `ServerWorker` count:

```csharp
// Properties/AssemblyInfo.cs
using Xunit;
using VibePlugins.RocketMod.TestBase.Xunit;

[assembly: TestFramework(
    RocketModTestFrameworkConstants.TypeName,
    RocketModTestFrameworkConstants.AssemblyName)]
[assembly: ServerWorker(Count = 2)]
```

If `ServerWorker` is not specified, the default count is 1 (standalone mode).

## Performance Tips

- Each container runs a full Unturned server. Set the count based on available CPU and memory.
- Use `WithSkipAssets()` in `ConfigureContainer` to reduce server startup time.
- Group related tests into the same test class to minimize container restarts.
- Start with 2-3 workers and increase if your machine has capacity.
- On CI runners with limited resources, consider using `Count = 1` to avoid contention.
