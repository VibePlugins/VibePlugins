using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using UnityEngine;
using VibeVault.Models;

namespace VibeVault.Commands;

public class CommandVaultUpgrade : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "vaultupgrade";
    public string Help => "Upgrade your vault to a larger tier.";
    public string Syntax => "/vaultupgrade [from] [to]";
    public List<string> Aliases => new List<string> { "vupgrade", "vu" };
    public List<string> Permissions => new List<string> { "vibevault.upgrade" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        var player = (UnturnedPlayer)caller;
        try
        {
            var service = VibeVaultPlugin.Instance.VaultService;
            var storage = service.Storage;
            var config = VibeVaultPlugin.Instance.Configuration.Instance;
            var upgrades = config.Upgrades;
            var ownerId = player.CSteamID.m_SteamID.ToString();

            if (upgrades == null || upgrades.Count == 0)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_upgrade_not_found"), Color.red);
                return;
            }

            VaultUpgradeConfig? upgrade = null;

            if (command.Length >= 2)
            {
                // Explicit from/to
                var fromTier = command[0];
                var toTier = command[1];
                upgrade = upgrades.FirstOrDefault(u =>
                    u.FromTier.Equals(fromTier, StringComparison.OrdinalIgnoreCase) &&
                    u.ToTier.Equals(toTier, StringComparison.OrdinalIgnoreCase));
            }
            else if (command.Length == 1)
            {
                // Upgrade current best tier to specified tier
                var currentTier = service.GetBestVaultTier(player);
                if (currentTier == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                var toTier = command[0];
                upgrade = upgrades.FirstOrDefault(u =>
                    u.FromTier.Equals(currentTier.Name, StringComparison.OrdinalIgnoreCase) &&
                    u.ToTier.Equals(toTier, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Auto-detect: find first available upgrade from current tier
                var currentTier = service.GetBestVaultTier(player);
                if (currentTier == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                upgrade = upgrades.FirstOrDefault(u =>
                    u.FromTier.Equals(currentTier.Name, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(u.Permission) || player.HasPermission(u.Permission)));
            }

            if (upgrade == null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_upgrade_not_found"), Color.red);
                return;
            }

            // Check permission for this upgrade
            if (!string.IsNullOrEmpty(upgrade.Permission) && !player.HasPermission(upgrade.Permission))
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                return;
            }

            // Check experience
            var playerXp = player.Experience;
            if (playerXp < upgrade.ExperienceCost)
            {
                UnturnedChat.Say(player,
                    VibeVaultPlugin.Instance.Translate("vault_upgrade_no_xp", upgrade.ExperienceCost, playerXp),
                    Color.red);
                return;
            }

            // Get the target tier config to find new dimensions
            var toTierConfig = service.GetVaultTierByName(upgrade.ToTier);
            if (toTierConfig == null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_upgrade_not_found"), Color.red);
                return;
            }

            // Deduct experience
            player.Experience -= upgrade.ExperienceCost;

            // Perform upgrade
            var success = storage.UpgradeVault(ownerId, upgrade.FromTier, toTierConfig.Width, toTierConfig.Height);
            if (!success)
            {
                // Refund experience on failure
                player.Experience += upgrade.ExperienceCost;
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
                return;
            }

            UnturnedChat.Say(player,
                VibeVaultPlugin.Instance.Translate("vault_upgrade_success", upgrade.FromTier, upgrade.ToTier, upgrade.ExperienceCost),
                Color.green);
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
    }
}
