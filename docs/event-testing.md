---
uid: event-testing
---

# Event Testing

Monitor server-side events during tests to verify that your plugin triggers the correct behavior.

## Basic Usage

```csharp
// Start monitoring BEFORE the action that triggers the event
var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

// Trigger the action
await CreatePlayerAsync(p => p.WithName("NewPlayer"));

// Wait for the event
var evt = await chatMonitor.WaitForAsync(
    e => e.Text != null && e.Text.Contains("Welcome"));

Assert.Contains("NewPlayer", evt.Text);
```

Start monitoring before performing the action. The monitor buffers events, so even if the event arrives before `WaitForAsync` is called, it will still be matched.

## EventMonitor Methods

| Method | Parameters | Default Timeout | Description |
|---|---|---|---|
| `WaitForAsync(predicate, timeout)` | `Func<TEvent, bool>`, `TimeSpan?` | 10s | Wait for a single event matching the predicate. Returns the event. |
| `WaitForCountAsync(count, predicate, timeout)` | `int`, `Func<TEvent, bool>`, `TimeSpan?` | 10s | Wait for `count` matching events. Returns a list. |
| `ShouldNotOccurAsync(predicate, timeout)` | `Func<TEvent, bool>`, `TimeSpan?` | 2s | Assert that no matching event occurs within the timeout. |

All predicates are optional. If `null`, any event of the type matches.

## Available Event Types

### ChatMessageEvent

| Property | Type | Description |
|---|---|---|
| `Text` | `string` | The chat message content |
| `SenderName` | `string` | Display name of the sender |
| `RecipientName` | `string` | Display name of the recipient |
| `Mode` | `string` | Chat mode: "Global", "Local", "Group" |

### PlayerConnectedEvent

| Property | Type | Description |
|---|---|---|
| `PlayerName` | `string` | Display name of the player |
| `SteamId` | `ulong` | Steam64 ID |

### PlayerDisconnectedEvent

| Property | Type | Description |
|---|---|---|
| `PlayerName` | `string` | Display name of the player |
| `SteamId` | `ulong` | Steam64 ID |

### PlayerDeathEvent

| Property | Type | Description |
|---|---|---|
| `PlayerName` | `string` | Display name of the player who died |
| `KillerName` | `string` | Display name of the killer |
| `Cause` | `string` | Cause of death (e.g. "Gun", "Zombie", "Fall") |
| `Limb` | `string` | Body part hit (e.g. "Head", "Torso") |

### PlayerDamageEvent

| Property | Type | Description |
|---|---|---|
| `PlayerName` | `string` | Display name of the player |
| `Damage` | `float` | Amount of damage dealt |
| `Cause` | `string` | Cause of damage |

### VehicleEnterEvent

| Property | Type | Description |
|---|---|---|
| `PlayerName` | `string` | Display name of the player |
| `VehicleId` | `uint` | Instance ID of the vehicle |

## Examples

### Waiting for a Chat Event

```csharp
[Fact]
public async Task WelcomeMessage_WhenPlayerConnects_SendsWelcome()
{
    var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

    var player = await CreatePlayerAsync(p => p.WithName("NewPlayer"));

    var welcomeEvent = await chatMonitor.WaitForAsync(
        e => e.Text != null && e.Text.Contains("Welcome") && e.Text.Contains("NewPlayer"));

    Assert.Contains("Welcome to the server, NewPlayer!", welcomeEvent.Text);
}
```

### Monitoring Player Connection

```csharp
[Fact]
public async Task PlayerConnected_EventIsFired()
{
    var connectMonitor = await MonitorEventAsync<PlayerConnectedEvent>();

    await CreatePlayerAsync(p => p.WithName("EventPlayer").WithSteamId(76561198000000099));

    var connectedEvent = await connectMonitor.WaitForAsync(
        e => e.PlayerName == "EventPlayer");

    Assert.Equal("EventPlayer", connectedEvent.PlayerName);
    Assert.Equal(76561198000000099UL, connectedEvent.SteamId);
}
```

### Negative Assertion

```csharp
[Fact]
public async Task NoDeathEvent_WhenPlayerHealed()
{
    var deathMonitor = await MonitorEventAsync<PlayerDeathEvent>();

    var caller = await CreatePlayerAsync(p => p.WithName("Healer").AsAdmin());
    await ExecuteCommand("heal").AsPlayer(caller.HandleId).RunAsync();

    // Assert no death event fires within 2 seconds (default)
    await deathMonitor.ShouldNotOccurAsync();
}
```

### Collecting Multiple Events

```csharp
[Fact]
public async Task MultiplePlayersConnect()
{
    var connectMonitor = await MonitorEventAsync<PlayerConnectedEvent>();

    await CreatePlayerAsync(p => p.WithName("Player1"));
    await CreatePlayerAsync(p => p.WithName("Player2"));
    await CreatePlayerAsync(p => p.WithName("Player3"));

    var events = await connectMonitor.WaitForCountAsync(3);

    Assert.Equal(3, events.Count);
}
```

## Tips

- Always start monitoring **before** the action that triggers the event. If you create the monitor after the action, the event may have already fired.
- `WaitForAsync` with no predicate returns the first event of that type, regardless of content.
- `ShouldNotOccurAsync` defaults to 2 seconds. Increase the timeout if the event might arrive with delay.
- Buffered events: if an event arrives between `MonitorEventAsync` and `WaitForAsync`, it is still returned.
