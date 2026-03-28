using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = VibeVault.VibeVaultPlugin;

namespace VibeVault.Tests
{
    public class VaultShareCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task VaultShare_ShareWithPlayer_Succeeds()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("ShareOwner").AsAdmin());
            var target = await CreatePlayerAsync(p => p.WithName("ShareTarget"));

            var result = await ExecuteCommand("vaultshare")
                .AsPlayer(caller.HandleId)
                .WithArgs("ShareTarget")
                .RunAsync();

            result.ShouldSucceed();
            result.ShouldContainMessage("Shared");
        }

        [Fact]
        public async Task VaultShare_ShareWithSelf_ShowsError()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("SelfSharePlayer").AsAdmin());

            var result = await ExecuteCommand("vaultshare")
                .AsPlayer(caller.HandleId)
                .WithArgs("SelfSharePlayer")
                .RunAsync();

            result.ShouldContainMessage("yourself");
        }

        [Fact]
        public async Task VaultShare_RemoveShare_Succeeds()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("UnshareOwner").AsAdmin());
            var target = await CreatePlayerAsync(p => p.WithName("UnshareTarget"));

            var result = await ExecuteCommand("vaultshare")
                .AsPlayer(caller.HandleId)
                .WithArgs("remove", "UnshareTarget")
                .RunAsync();

            result.ShouldSucceed();
            result.ShouldContainMessage("Removed");
        }

        [Fact]
        public async Task VaultShare_WithoutPermission_Fails()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoPermPlayer"));

            var result = await ExecuteCommand("vaultshare")
                .AsPlayer(caller.HandleId)
                .WithArgs("SomePlayer")
                .RunAsync();

            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }
    }
}
