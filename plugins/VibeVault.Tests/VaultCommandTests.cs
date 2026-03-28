using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit;
using Plugin = VibeVault.VibeVaultPlugin;

namespace VibeVault.Tests
{
    public class VaultCommandTests : RocketModPluginTestBase<Plugin>
    {
        [Fact]
        public async Task Vault_WithPermission_OpensVault()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("VaultPlayer").AsAdmin());

            var result = await ExecuteCommand("vault")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
        }

        [Fact]
        public async Task Vault_WithoutPermission_Fails()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NoPermPlayer"));

            var result = await ExecuteCommand("vault")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldHaveStatus(CommandStatus.PermissionDenied);
        }

        [Fact]
        public async Task Vault_WithSpecificName_OpensNamedVault()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("NamedVaultPlayer").AsAdmin());

            var result = await ExecuteCommand("vault")
                .AsPlayer(caller.HandleId)
                .WithArgs("default")
                .RunAsync();

            result.ShouldSucceed();
        }

        [Fact]
        public async Task Vault_WhileDead_ShowsError()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("DeadPlayer").AsAdmin().WithHealth(0).WithMaxHealth(100));

            var monitor = await MonitorEventAsync<ChatMessageEvent>();

            var result = await ExecuteCommand("vault")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldContainMessage("alive");
        }

        [Fact]
        public async Task Vault_OtherPlayer_WithPermission()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("AdminPlayer").AsAdmin());
            var target = await CreatePlayerAsync(p => p.WithName("TargetPlayer"));

            var result = await ExecuteCommand("vault")
                .AsPlayer(caller.HandleId)
                .WithArgs("default", "TargetPlayer")
                .RunAsync();

            result.ShouldSucceed();
        }

        [Fact]
        public async Task Vault_ViaAlias_Works()
        {
            var caller = await CreatePlayerAsync(p => p.WithName("AliasPlayer").AsAdmin());

            var result = await ExecuteCommand("v")
                .AsPlayer(caller.HandleId)
                .RunAsync();

            result.ShouldSucceed();
        }
    }
}
