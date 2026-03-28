using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using UnityEngine;

namespace VibeVault.Commands;

public class CommandTrash : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "trash";
    public string Help => "Opens a trash vault. Items placed inside are destroyed when closed.";
    public string Syntax => "/trash";
    public List<string> Aliases => new List<string> { "t" };
    public List<string> Permissions => new List<string> { "vibevault.trash" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        var player = (UnturnedPlayer)caller;
        try
        {
            if (player.Dead)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance!.Translate("vault_must_be_alive"), Color.red);
                return;
            }

            if (player.Player.movement.getVehicle() != null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance!.Translate("vault_not_in_vehicle"), Color.red);
                return;
            }

            var vaultManager = VibeVaultPlugin.Instance!.VaultManager;
            if (vaultManager.GetOpenVault(player.CSteamID.m_SteamID.ToString()) != null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_already_open"), Color.red);
                return;
            }

            var success = VibeVaultPlugin.Instance.VaultService.OpenTrash(player);
            if (!success)
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance!.Translate("error_occurred"), Color.red);
        }
    }
}
