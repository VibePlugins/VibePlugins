using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using UnityEngine;

namespace VibeVault.Commands;

public class CommandVaultView : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "vaultview";
    public string Help => "Opens a read-only view of another player's vault.";
    public string Syntax => "/vaultview <player> [name]";
    public List<string> Aliases => new List<string> { "vview", "vv" };
    public List<string> Permissions => new List<string> { "vibevault.view" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        var player = (UnturnedPlayer)caller;
        try
        {
            if (command.Length < 1)
            {
                UnturnedChat.Say(player, $"Usage: {Syntax}", Color.yellow);
                return;
            }

            if (player.Dead)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_must_be_alive"), Color.red);
                return;
            }

            if (player.Player.movement.getVehicle() != null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_not_in_vehicle"), Color.red);
                return;
            }

            var vaultManager = VibeVaultPlugin.Instance.VaultManager;
            if (vaultManager.GetOpenVault(player.CSteamID.m_SteamID.ToString()) != null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_already_open"), Color.red);
                return;
            }

            var service = VibeVaultPlugin.Instance.VaultService;
            var targetName = command[0];
            var target = UnturnedPlayer.FromName(targetName);

            if (target == null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_player_not_found", targetName), Color.red);
                return;
            }

            var targetId = target.CSteamID.m_SteamID.ToString();
            string? vaultName = command.Length >= 2 ? command[1] : null;

            // If no vault name specified, use target's best tier name
            if (vaultName == null)
            {
                var tier = service.GetBestVaultTier(target);
                if (tier == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_not_found", "default"), Color.red);
                    return;
                }

                vaultName = tier.Name;
            }

            var success = service.OpenReadOnlyVault(player, targetId, vaultName);
            if (!success)
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_not_found", vaultName), Color.red);
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
    }
}
