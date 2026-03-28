---
uid: command-testing
---

# Command Testing

Test RocketMod commands by executing them against the server and asserting on the results.

## Basic Usage

```csharp
var result = await ExecuteCommand("heal")
    .AsPlayer(caller.HandleId)
    .WithArgs("Wounded", "50")
    .RunAsync();

result.ShouldSucceed()
      .ShouldContainMessage("Healed");
```

`ExecuteCommand` returns a `CommandTestBuilder`. Configure the caller and arguments, then call `RunAsync()` to send the command to the server. The returned `CommandTestResult` supports fluent assertion chaining.

## CommandTestBuilder Methods

| Method | Description |
|---|---|
| `AsPlayer(Guid handleId)` | Set caller to a mock player created with `CreatePlayerAsync`. The harness resolves name, Steam ID, and admin status from the mock. |
| `AsPlayer(string name, ulong steamId, bool isAdmin = false)` | Set caller to an ad-hoc player identity. |
| `AsConsole()` | Set caller to server console (Steam ID 0, admin). This is the default. |
| `WithArgs(params string[] args)` | Set the command arguments. |
| `WithTimeout(TimeSpan timeout)` | Override the request timeout. Default: 30 seconds. |
| `RunAsync()` | Send the command and return a `CommandTestResult`. |

## Direct Execution

For simple cases without the builder, use `ExecuteCommandAsync`:

```csharp
var result = await ExecuteCommandAsync(
    commandName: "heal",
    args: new[] { "Wounded", "50" },
    callerSteamId: 0,
    callerName: "Console",
    isAdmin: true);

result.ShouldSucceed();
```

## CommandTestResult Assertions

All assertion methods return `this` for fluent chaining.

| Method | Description |
|---|---|
| `ShouldSucceed()` | Assert status is `CommandStatus.Success`. |
| `ShouldFail()` | Assert status is not `CommandStatus.Success`. |
| `ShouldHaveStatus(CommandStatus expected)` | Assert a specific status (e.g. `PermissionDenied`). |
| `ShouldThrow<TException>()` | Assert the command threw a specific exception type. |
| `ShouldThrow(string typeName)` | Assert the exception type name contains the given substring. |
| `ShouldContainMessage(string substring)` | Assert at least one chat message contains the substring. |
| `ShouldContainMessage(Func<ChatMessageInfo, bool> predicate)` | Assert at least one chat message matches the predicate. |
| `ShouldNotContainMessage(string substring)` | Assert no chat message contains the substring. |
| `ShouldHaveMessageCount(int expected)` | Assert the exact number of chat messages produced. |

### CommandTestResult Properties

| Property | Type | Description |
|---|---|---|
| `Status` | `CommandStatus` | The command execution status. |
| `ChatMessages` | `IReadOnlyList<ChatMessageInfo>` | Chat messages produced during execution. |
| `Exception` | `SerializableExceptionInfo` | Exception info if the command threw, or `null`. |
| `Response` | `ExecuteCommandResponse` | The underlying response message. |

### CommandStatus Values

| Value | Meaning |
|---|---|
| `Success` | Command executed without error. |
| `NotFound` | Command name not recognized. |
| `Exception` | Command threw an exception. |
| `PermissionDenied` | Caller lacks required permissions. |

## Examples

### Testing with a Mock Player

```csharp
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
```

### Testing Permission Denied

```csharp
[Fact]
public async Task Heal_WithoutPermission_Fails()
{
    var caller = await CreatePlayerAsync(p => p.WithName("NoPerms").WithHealth(50));

    var result = await ExecuteCommand("heal")
        .AsPlayer(caller.HandleId)
        .RunAsync();

    result.ShouldHaveStatus(CommandStatus.PermissionDenied);
}
```

### Testing Error Messages

```csharp
[Fact]
public async Task Heal_InvalidTarget_ShowsError()
{
    var caller = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());

    var result = await ExecuteCommand("heal")
        .AsPlayer(caller.HandleId)
        .WithArgs("NonExistentPlayer")
        .RunAsync();

    result.ShouldSucceed()
          .ShouldContainMessage("not found");
}
```

### Testing Command Aliases

```csharp
[Fact]
public async Task Heal_ViaAlias_Works()
{
    var caller = await CreatePlayerAsync(p => p.WithName("AliasUser").AsAdmin().WithHealth(40));

    var result = await ExecuteCommand("h")
        .AsPlayer(caller.HandleId)
        .RunAsync();

    result.ShouldSucceed()
          .ShouldContainMessage("healed yourself");
}
```

### Testing with an Ad-Hoc Player

```csharp
[Fact]
public async Task Announce_BroadcastsMessage()
{
    var result = await ExecuteCommand("announce")
        .AsPlayer("Admin", 76561198000000001, isAdmin: true)
        .WithArgs("Server", "restarting")
        .RunAsync();

    result.ShouldSucceed();
}
```

## Tips

- The default caller is `AsConsole()` (admin). If your command requires a non-admin player, use `AsPlayer` explicitly.
- Use `AsPlayer(Guid)` when you need the mock player's state (health, position) to be tracked by the server. Use `AsPlayer(string, ulong, bool)` when you only need an identity.
- Chain multiple assertions to verify both status and message content in one statement.
