---
uid: scenario-builder
---

# Scenario Builder

Define multi-step test scenarios using a BDD-style Given/When/Then pattern.

## Basic Usage

```csharp
var result = await CreateScenario()
    .Given(async () =>
    {
        caller = await CreatePlayerAsync(p => p.WithName("Doctor").AsAdmin());
    })
    .When(async () =>
    {
        var healResult = await ExecuteCommand("heal")
            .AsPlayer(caller.HandleId)
            .RunAsync();
        healResult.ShouldSucceed();
    })
    .ThenExpectMessage("healed")
    .ThenExpectNoEvent<PlayerDeathEvent>()
    .ExecuteAsync();

result.ShouldPass();
```

## ScenarioBuilder Methods

| Method | Parameters | Description |
|---|---|---|
| `Given(Func<Task>)` | async setup function | Add a setup step. Multiple Given steps run in order. |
| `When(Func<Task>)` | async action function | Add an action step. Multiple When steps run in order after all Given steps. |
| `ThenExpect<TEvent>(predicate, timeout)` | `Func<TEvent, bool>`, `TimeSpan?` | Assert an event occurs. Default timeout: 10s. |
| `ThenExpectMessage(string)` | substring | Assert a chat message containing the substring is produced. |
| `ThenExpectNoEvent<TEvent>(timeout)` | `TimeSpan?` | Assert an event does NOT occur. Default timeout: 2s. |
| `ExecuteAsync()` | -- | Run the scenario and return a `ScenarioResult`. |

## ScenarioResult

| Member | Type | Description |
|---|---|---|
| `AllExpectationsMet` | `bool` | `true` if all expectations passed |
| `Failures` | `IReadOnlyList<string>` | List of failure descriptions |
| `ShouldPass()` | method | Throws `XunitException` if any expectation failed |

## Execution Order

1. All `Given` steps run in the order they were added.
2. All `When` actions run in the order they were added.
3. All `ThenExpect` / `ThenExpectMessage` / `ThenExpectNoEvent` expectations are evaluated.

If a Given or When step throws, execution stops and the failure is recorded.

## Examples

### Multi-Step with Multiple Players

```csharp
[Fact]
public async Task HealAndAnnounce_Scenario()
{
    CreateMockResponse caller = null;
    CreateMockResponse target = null;

    var scenarioResult = await CreateScenario()
        .Given(async () =>
        {
            caller = await CreatePlayerAsync(p => p
                .WithName("Doctor")
                .AsAdmin()
                .WithHealth(100));
        })
        .Given(async () =>
        {
            target = await CreatePlayerAsync(p => p
                .WithName("Injured")
                .WithHealth(15)
                .WithMaxHealth(100));
        })
        .When(async () =>
        {
            var healResult = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .WithArgs("Injured", "80")
                .RunAsync();
            healResult.ShouldSucceed();
        })
        .When(async () =>
        {
            var announceResult = await ExecuteCommand("announce")
                .AsPlayer(caller.HandleId)
                .WithArgs("Injured", "has", "been", "healed!")
                .RunAsync();
            announceResult.ShouldSucceed();
        })
        .ThenExpectMessage("Injured has been healed!")
        .ThenExpectNoEvent<PlayerDeathEvent>()
        .ExecuteAsync();

    scenarioResult.ShouldPass();
}
```

### Event Expectations

```csharp
[Fact]
public async Task PlayerJoinsAndReceivesWelcome_Scenario()
{
    var scenarioResult = await CreateScenario()
        .When(async () =>
        {
            await CreatePlayerAsync(p => p.WithName("Newcomer"));
        })
        .ThenExpect<ChatMessageEvent>(
            e => e.Text != null && e.Text.Contains("Welcome") && e.Text.Contains("Newcomer"))
        .ThenExpect<PlayerConnectedEvent>(
            e => e.PlayerName == "Newcomer")
        .ExecuteAsync();

    scenarioResult.ShouldPass();
}
```

### Negative Path Testing

```csharp
[Fact]
public async Task HealNonExistentPlayer_Scenario()
{
    CreateMockResponse admin = null;

    var scenarioResult = await CreateScenario()
        .Given(async () =>
        {
            admin = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());
        })
        .When(async () =>
        {
            var result = await ExecuteCommand("heal")
                .AsPlayer(admin.HandleId)
                .WithArgs("GhostPlayer")
                .RunAsync();

            result.ShouldSucceed()
                  .ShouldContainMessage("not found");
        })
        .ThenExpectNoEvent<PlayerDeathEvent>()
        .ExecuteAsync();

    scenarioResult.ShouldPass();
}
```

## When to Use ScenarioBuilder vs Direct Tests

Use **ScenarioBuilder** when:
- The test has multiple setup steps and actions that form a coherent scenario.
- You need to assert on events and messages produced across multiple actions.
- You want the Given/When/Then structure for readability.

Use **direct tests** (ExecuteCommand + assertions) when:
- The test is a single command with a single assertion.
- You do not need event expectations.
- Simplicity is more important than structure.
