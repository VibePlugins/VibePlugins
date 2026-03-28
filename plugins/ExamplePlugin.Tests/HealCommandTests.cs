using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = ExamplePlugin.ExamplePlugin;

namespace ExamplePlugin.Tests
{
    public class HealCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task Heal_WithTarget_HealsTargetPlayer()
        {
            // Arrange: create an admin caller and a wounded target player
            var caller = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin().WithHealth(100));
            var target = await CreatePlayerAsync(p => p.WithName("Wounded").WithHealth(30).WithMaxHealth(100));

            // Act: execute /heal Wounded 50
            var result = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .WithArgs("Wounded", "50")
                .RunAsync();

            // Assert: command succeeds and confirmation message references the target
            result.ShouldSucceed()
                  .ShouldContainMessage("Healed")
                  .ShouldContainMessage("Wounded")
                  .ShouldContainMessage("50");
        }

        [Fact]
        public async Task Heal_WithoutPermission_Fails()
        {
            // Arrange: create a non-admin player with no permissions
            var caller = await CreatePlayerAsync(p => p.WithName("NoPerms").WithHealth(50));

            // Act: attempt /heal without permission
            var result = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            // Assert: command is denied
            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }

        [Fact]
        public async Task Heal_SelfHeal_WhenNoTarget()
        {
            // Arrange: create a wounded admin player
            var caller = await CreatePlayerAsync(p => p.WithName("SelfHealer").AsAdmin().WithHealth(20).WithMaxHealth(100));

            // Act: execute /heal with no target (heals self using default amount)
            var result = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            // Assert: command succeeds with self-heal confirmation
            result.ShouldSucceed()
                  .ShouldContainMessage("healed yourself");
        }

        [Fact]
        public async Task Heal_InvalidTarget_ShowsError()
        {
            // Arrange: create an admin caller (no other players on the server)
            var caller = await CreatePlayerAsync(p => p.WithName("Admin").AsAdmin());

            // Act: try to heal a player that does not exist
            var result = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .WithArgs("NonExistentPlayer")
                .RunAsync();

            // Assert: command succeeds (no exception) but the error message is shown
            result.ShouldSucceed()
                  .ShouldContainMessage("not found");
        }

        [Fact]
        public async Task Heal_WithCustomAmount_UsesSpecifiedAmount()
        {
            // Arrange
            var caller = await CreatePlayerAsync(p => p.WithName("Healer").AsAdmin());
            var target = await CreatePlayerAsync(p => p.WithName("Patient").WithHealth(10).WithMaxHealth(100));

            // Act: heal with a specific amount
            var result = await ExecuteCommand("heal")
                .AsPlayer(caller.HandleId)
                .WithArgs("Patient", "75")
                .RunAsync();

            // Assert
            result.ShouldSucceed()
                  .ShouldContainMessage("75");
        }

        [Fact]
        public async Task Heal_ViaAlias_Works()
        {
            // Arrange
            var caller = await CreatePlayerAsync(p => p.WithName("AliasUser").AsAdmin().WithHealth(40));

            // Act: use the "h" alias instead of "heal"
            var result = await ExecuteCommand("h")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            // Assert: self-heal works via the alias
            result.ShouldSucceed()
                  .ShouldContainMessage("healed yourself");
        }
    }
}
