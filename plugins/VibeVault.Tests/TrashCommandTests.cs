using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = VibeVault.VibeVaultPlugin;

namespace VibeVault.Tests
{
    public class TrashCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task Trash_WithPermission_Opens()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("TrashPlayer").AsAdmin());

            var result = await ExecuteCommand("trash")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
        }

        [Fact]
        public async Task Trash_WithoutPermission_Fails()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoPermPlayer"));

            var result = await ExecuteCommand("trash")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }

        [Fact]
        public async Task Trash_ViaAlias_Works()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("AliasTrashPlayer").AsAdmin());

            var result = await ExecuteCommand("t")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
        }
    }
}
