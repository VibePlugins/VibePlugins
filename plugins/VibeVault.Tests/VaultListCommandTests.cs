using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = VibeVault.VibeVaultPlugin;

namespace VibeVault.Tests
{
    public class VaultListCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task Vaults_ListsOwnVaults()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("ListPlayer").AsAdmin());

            var monitor = await MonitorEventAsync<ChatMessageEvent>();

            var result = await ExecuteCommand("vaults")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
            result.ShouldContainMessage("Vaults");
        }

        [Fact]
        public async Task Vaults_WithoutPermission_Fails()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoPermPlayer"));

            var result = await ExecuteCommand("vaults")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }

        [Fact]
        public async Task Vaults_ViaAlias_Works()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("AliasListPlayer").AsAdmin());

            var result = await ExecuteCommand("vlist")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
        }
    }
}
