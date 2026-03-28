using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace VibeVault.Commands;

public class CommandVault : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "vault";
    public string Help => "Opens a personal vault.";
    public string Syntax => "/vault [name] [player]";
    public List<string> Aliases => new List<string> { "v" };
    public List<string> Permissions => new List<string> { "vibevault.vault" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        var player = (UnturnedPlayer)caller;
        try
        {
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

            if (command.Length == 0)
            {
                // Open best available vault
                var tier = service.GetBestVaultTier(player);
                if (tier == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                var success = service.OpenVault(player);
                if (!success)
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
            }
            else if (command.Length == 1)
            {
                // Open named vault
                var vaultName = command[0];
                var success = service.OpenVault(player, vaultName);
                if (!success)
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_not_found", vaultName), Color.red);
            }
            else if (command.Length >= 2)
            {
                // Open another player's vault - requires extra permission
                if (!player.HasPermission("vibevault.vault.other"))
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                var vaultName = command[0];
                var targetName = command[1];
                var target = UnturnedPlayer.FromName(targetName);

                if (target == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_player_not_found", targetName), Color.red);
                    return;
                }

                var success = service.OpenOtherVault(player, target.CSteamID.m_SteamID.ToString(), vaultName);
                if (!success)
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_not_found", vaultName), Color.red);
            }
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
    }
}
