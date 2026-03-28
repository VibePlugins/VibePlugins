using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = ExamplePlugin.ExamplePlugin;

namespace ExamplePlugin.Tests
{
    public class ScenarioTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task HealAndAnnounce_Scenario()
        {
            // Demonstrates a multi-step scenario using the ScenarioBuilder:
            // 1. Set up players
            // 2. Heal a wounded player
            // 3. Announce the heal to the server
            // 4. Verify the expected messages appear

            CreateMockResponse caller = null;
            CreateMockResponse target = null;

            var scenarioResult = await CreateScenario()

                // Given: an admin and a wounded player exist on the server
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

                // When: the admin heals the wounded player
                .When(async () =>
                {
                    var healResult = await ExecuteCommand("heal")
                        .AsPlayer(caller.HandleId)
                        .WithArgs("Injured", "80")
                        .RunAsync();

                    healResult.ShouldSucceed();
                })

                // When: the admin announces the heal
                .When(async () =>
                {
                    var announceResult = await ExecuteCommand("announce")
                        .AsPlayer(caller.HandleId)
                        .WithArgs("Injured", "has", "been", "healed!")
                        .RunAsync();

                    announceResult.ShouldSucceed();
                })

                // Then: expect a broadcast chat message about the heal
                .ThenExpectMessage("Injured has been healed!")

                // Then: no one should die during this scenario
                .ThenExpectNoEvent<PlayerDeathEvent>()

                .ExecuteAsync();

            scenarioResult.ShouldPass();
        }

        [Fact]
        public async Task PlayerJoinsAndReceivesWelcome_Scenario()
        {
            // Demonstrates monitoring events within a scenario:
            // A new player connects and receives the configured welcome message.

            var scenarioResult = await CreateScenario()

                // When: a new player connects
                .When(async () =>
                {
                    await CreatePlayerAsync(p => p.WithName("Newcomer"));
                })

                // Then: the welcome message is sent
                .ThenExpect<ChatMessageEvent>(
                    e => e.Text != null && e.Text.Contains("Welcome") && e.Text.Contains("Newcomer"))

                // Then: a player connected event fires
                .ThenExpect<PlayerConnectedEvent>(
                    e => e.PlayerName == "Newcomer")

                .ExecuteAsync();

            scenarioResult.ShouldPass();
        }

        [Fact]
        public async Task HealNonExistentPlayer_Scenario()
        {
            // Demonstrates a negative-path scenario:
            // Healing a non-existent player should produce an error message
            // and no death events.

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

                    // The command still "succeeds" (no exception), but shows an error message
                    result.ShouldSucceed()
                          .ShouldContainMessage("not found");
                })

                .ThenExpectNoEvent<PlayerDeathEvent>()

                .ExecuteAsync();

            scenarioResult.ShouldPass();
        }
    }
}
