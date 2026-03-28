using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = VibeVault.VibeVaultPlugin;

namespace VibeVault.Tests
{
    public class VaultViewCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task VaultView_WithTarget_OpensReadOnly()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("ViewerPlayer").AsAdmin());
            var target = await CreatePlayerAsync(p => p.WithName("TargetPlayer"));

            var monitor = await MonitorEventAsync<ChatMessageEvent>();

            var result = await ExecuteCommand("vaultview")
                .AsPlayer(caller.HandleId)
                .WithArgs("TargetPlayer")
                .RunAsync();

            result.ShouldSucceed();
            result.ShouldContainMessage("read-only");
        }

        [Fact]
        public async Task VaultView_WithoutArgs_ShowsUsage()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoArgsPlayer").AsAdmin());

            var result = await ExecuteCommand("vaultview")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldContainMessage("Usage");
        }

        [Fact]
        public async Task VaultView_WithoutPermission_Fails()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoPermPlayer"));

            var result = await ExecuteCommand("vaultview")
                .AsPlayer(caller.HandleId)
                .WithArgs("SomePlayer")
                .RunAsync();

            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }
    }
}
