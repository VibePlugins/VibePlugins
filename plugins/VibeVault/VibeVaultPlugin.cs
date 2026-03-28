using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using VibeVault.API;
using VibeVault.Services;
using VibeVault.Storage;

namespace VibeVault
{
    public class VibeVaultPlugin : RocketPlugin<VibeVaultConfiguration>
    {
        public static VibeVaultPlugin? Instance { get; private set; }

        public VaultService VaultService { get; private set; } = null!;
        public VaultManager VaultManager { get; private set; } = null!;
        public IVaultStorage VaultStorage { get; private set; } = null!;
        public VibeVaultAPI API { get; private set; } = null!;

        protected override void Load()
        {
            Instance = this;

            VaultManager = new VaultManager();
            VaultStorage = CreateStorage();
            API = new VibeVaultAPI(VaultStorage, VaultManager);
            VaultService = new VaultService(VaultStorage, VaultManager, Configuration.Instance, API);

            try
            {
                VaultStorage.Initialize();
                Rocket.Core.Logging.Logger.Log("VibeVault storage initialized successfully.");
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogException(ex, "Failed to initialize VibeVault storage.");
            }

            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            PlayerLife.onPlayerDied += OnPlayerDied;

            Rocket.Core.Logging.Logger.Log($"VibeVault loaded! Storage: {Configuration.Instance.StorageType}, " +
                $"Vault tiers: {Configuration.Instance.VaultTiers.Count}");
        }

        protected override void Unload()
        {
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            PlayerLife.onPlayerDied -= OnPlayerDied;

            // Close all open vaults
            VaultManager.CloseAllVaults(playerId =>
            {
                var player = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(ulong.Parse(playerId)));
                if (player != null)
                {
                    VaultService.CloseVault(player);
                }
            });

            Instance = null;
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            var steamId = player.CSteamID.m_SteamID.ToString();
            var openVault = VaultManager.GetOpenVault(steamId);
            if (openVault != null)
            {
                VaultService.CloseVault(player);
            }
        }

        private void OnPlayerDied(PlayerLife sender, EDeathCause cause, ELimb limb, Steamworks.CSteamID instigator)
        {
            var steamId = sender.player.channel.owner.playerID.steamID.m_SteamID.ToString();
            var openVault = VaultManager.GetOpenVault(steamId);
            if (openVault != null)
            {
                var player = UnturnedPlayer.FromPlayer(sender.player);
                if (player != null)
                {
                    VaultService.CloseVault(player);
                }
            }
        }

        private IVaultStorage CreateStorage()
        {
            var config = Configuration.Instance;
            return config.StorageType.ToLowerInvariant() switch
            {
                "mysql" => new MySqlVaultStorage(config.MySqlConnectionString),
                _ => new FileVaultStorage(
                    System.IO.Path.Combine(Rocket.Core.Environment.PluginsDirectory, "VibeVault", "Data"))
            };
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "vault_opened", "Opened vault: {0}" },
            { "vault_closed", "Vault closed and saved." },
            { "vault_not_found", "Vault '{0}' not found or you don't have permission." },
            { "vault_already_open", "You already have a vault open. Close it first." },
            { "vault_no_permission", "You don't have permission to access this vault." },
            { "vault_player_not_found", "Player \"{0}\" was not found." },
            { "vault_must_be_alive", "You must be alive to use vaults." },
            { "vault_not_in_vehicle", "You cannot open vaults while in a vehicle." },
            { "vault_list_header", "=== Your Vaults ===" },
            { "vault_list_item", "  {0} ({1}x{2})" },
            { "vault_list_shared", "  {0}'s {1} ({2}x{3}) [shared]" },
            { "vault_list_group", "  Group Vault ({0}x{1})" },
            { "vault_list_empty", "You have no vaults available." },
            { "vault_shared", "Shared vault '{0}' with {1}." },
            { "vault_unshared", "Removed {0}'s access to vault '{1}'." },
            { "vault_share_self", "You cannot share a vault with yourself." },
            { "vault_view_opened", "Viewing {0}'s vault: {1} (read-only)" },
            { "vault_upgrade_success", "Upgraded vault from {0} to {1}! Cost: {2} XP" },
            { "vault_upgrade_no_xp", "Not enough XP. Need {0}, have {1}." },
            { "vault_upgrade_not_found", "No upgrade path found from '{0}' to '{1}'." },
            { "vault_upgrade_list", "Available upgrades:" },
            { "vault_upgrade_item", "  {0} -> {1} (Cost: {2} XP)" },
            { "trash_opened", "Trash opened. Items will be destroyed when closed." },
            { "trash_closed", "Trash closed. Items destroyed." },
            { "group_vault_locked", "Group vault is currently in use by {0}." },
            { "group_vault_no_group", "You must be in a group to use group vaults." },
            { "group_vault_opened", "Group vault opened." },
            { "error_occurred", "An error occurred. Please try again." }
        };
    }
}
