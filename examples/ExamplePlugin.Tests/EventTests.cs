using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = ExamplePlugin.ExamplePlugin;

namespace ExamplePlugin.Tests
{
    public class EventTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task WelcomeMessage_WhenPlayerConnects_SendsWelcome()
        {
            // Arrange: start monitoring chat messages before the player connects
            var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

            // Act: create a mock player which triggers the PlayerConnected event
            var player = await CreatePlayerAsync(p => p.WithName("NewPlayer"));

            // Assert: the welcome message is sent to the new player
            var welcomeEvent = await chatMonitor.WaitForAsync(
                e => e.Text != null && e.Text.Contains("Welcome") && e.Text.Contains("NewPlayer"));

            Assert.Contains("Welcome to the server, NewPlayer!", welcomeEvent.Text);
        }

        [Fact]
        public async Task WelcomeMessage_ContainsPlayerName()
        {
            // Arrange
            var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

            // Act: connect a player with a distinctive name
            await CreatePlayerAsync(p => p.WithName("UniqueTestName123"));

            // Assert: the welcome message includes the player's name
            var evt = await chatMonitor.WaitForAsync(
                e => e.Text != null && e.Text.Contains("UniqueTestName123"));

            Assert.NotNull(evt);
        }

        [Fact]
        public async Task Announce_BroadcastsToAllPlayers()
        {
            // Arrange: create a couple of players and start monitoring chat
            await CreatePlayerAsync(p => p.WithName("Player1"));
            await CreatePlayerAsync(p => p.WithName("Player2"));

            var chatMonitor = await MonitorEventAsync<ChatMessageEvent>();

            // Act: execute /announce as an admin
            var result = await ExecuteCommand("announce")
                .AsPlayer("Admin", 76561198000000001, isAdmin: true)
                .WithArgs("Server", "restarting", "in", "5", "minutes")
                .RunAsync();

            // Assert: command succeeds and the broadcast message is captured
            result.ShouldSucceed();

            var broadcastEvent = await chatMonitor.WaitForAsync(
                e => e.Text != null && e.Text.Contains("Server restarting in 5 minutes"));

            Assert.NotNull(broadcastEvent);
        }

        [Fact]
        public async Task Announce_WithoutMessage_ShowsUsage()
        {
            // Act: execute /announce with no arguments
            var result = await ExecuteCommand("announce")
                .AsPlayer("Admin", 76561198000000001, isAdmin: true)
                .RunAsync();

            // Assert: command returns usage info
            result.ShouldSucceed()
                  .ShouldContainMessage("Usage:");
        }

        [Fact]
        public async Task PlayerConnected_EventIsFired()
        {
            // Arrange: monitor the PlayerConnected event directly
            var connectMonitor = await MonitorEventAsync<PlayerConnectedEvent>();

            // Act: create a mock player
            await CreatePlayerAsync(p => p.WithName("EventPlayer").WithSteamId(76561198000000099));

            // Assert: the connected event fires with the correct info
            var connectedEvent = await connectMonitor.WaitForAsync(
                e => e.PlayerName == "EventPlayer");

            Assert.Equal("EventPlayer", connectedEvent.PlayerName);
            Assert.Equal(76561198000000099UL, connectedEvent.SteamId);
        }
    }
}
