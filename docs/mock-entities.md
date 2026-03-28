---
uid: mock-entities
---

# Mock Entities

Create mock players, zombies, and animals on the server to use in your tests.

## Basic Usage

```csharp
var player = await CreatePlayerAsync(p => p
    .WithName("Alice")
    .AsAdmin()
    .WithHealth(50)
    .WithMaxHealth(100));

// Use the handle to execute commands as this player
var result = await ExecuteCommand("heal")
    .AsPlayer(player.HandleId)
    .RunAsync();
```

`CreatePlayerAsync` returns a `CreateMockResponse` with a `HandleId` (Guid) that identifies the mock entity on the server.

## Players

### PlayerOptions Reference

| Method | Type | Default | Description |
|---|---|---|---|
| `WithName(string)` | `string` | `"TestPlayer"` | Display name |
| `WithSteamId(ulong)` | `ulong?` | auto-generated | Steam64 ID |
| `WithHealth(byte)` | `byte` | `100` | Current health (0-255) |
| `WithMaxHealth(byte)` | `byte` | `100` | Maximum health (0-255) |
| `AsAdmin()` | `bool` | `false` | Grant admin privileges |
| `AtPosition(float x, float y, float z)` | `float[]` | `[0, 0, 0]` | World position |
| `WithExperience(uint)` | `uint` | `0` | Starting experience points |

### Using the Handle

Pass `player.HandleId` to `CommandTestBuilder.AsPlayer(Guid)` to execute commands as that mock player. The server resolves the player's name, Steam ID, and admin status from the mock.

```csharp
var admin = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());
var target = await CreatePlayerAsync(p => p.WithName("Target").WithHealth(30));

var result = await ExecuteCommand("heal")
    .AsPlayer(admin.HandleId)
    .WithArgs("Target", "50")
    .RunAsync();
```

## Zombies

```csharp
var zombie = await CreateZombieAsync(z => z
    .AtPosition(100f, 0f, 200f)
    .WithHealth(500)
    .WithSpeciality(1));
```

### ZombieOptions Reference

| Method | Type | Default | Description |
|---|---|---|---|
| `AtPosition(float x, float y, float z)` | `float[]` | `[0, 0, 0]` | World position |
| `WithHealth(ushort)` | `ushort` | `100` | Starting health |
| `WithSpeciality(byte)` | `byte` | `0` | Zombie type identifier |

## Animals

```csharp
var animal = await CreateAnimalAsync(a => a
    .AtPosition(50f, 0f, 50f)
    .WithHealth(200)
    .WithAnimalId(3));
```

### AnimalOptions Reference

| Method | Type | Default | Description |
|---|---|---|---|
| `AtPosition(float x, float y, float z)` | `float[]` | `[0, 0, 0]` | World position |
| `WithHealth(ushort)` | `ushort` | `100` | Starting health |
| `WithAnimalId(ushort)` | `ushort` | `0` | Animal type from Unturned's animal table |

## Cleanup

Mocks are destroyed automatically between tests. The framework sends a `DestroyAllMocks` request during test disposal. You do not need to manually clean up mock entities.

If you need custom cleanup logic during a test, use `OnCleanup`:

```csharp
OnCleanup(async () =>
{
    // Custom cleanup runs before mock destruction
});
```
