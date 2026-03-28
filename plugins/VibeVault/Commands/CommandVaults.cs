using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using Steamworks;
using UnityEngine;

namespace VibeVault.Commands;

public class CommandVaults : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "vaults";
    public string Help => "Lists all vaults you have access to.";
    public string Syntax => "/vaults [player]";
    public List<string> Aliases => new List<string> { "vlist" };
    public List<string> Permissions => new List<string> { "vibevault.vaults" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        var player = (UnturnedPlayer)caller;
        try
        {
            var service = VibeVaultPlugin.Instance.VaultService;
            var storage = service.Storage;
            string targetId;
            string targetName;

            if (command.Length >= 1)
            {
                // Listing another player's vaults
                if (!player.HasPermission("vibevault.vault.other"))
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                var target = UnturnedPlayer.FromName(command[0]);
                if (target == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_player_not_found", command[0]), Color.red);
                    return;
                }

                targetId = target.CSteamID.m_SteamID.ToString();
                targetName = target.DisplayName;
            }
            else
            {
                targetId = player.CSteamID.m_SteamID.ToString();
                targetName = player.DisplayName;
            }

            // Get own/target vaults
            var ownVaults = storage.GetPlayerVaults(targetId);

            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_list_header"), Color.cyan);

            if (ownVaults.Count == 0)
            {
                UnturnedChat.Say(player, "  No vaults found.", Color.gray);
            }
            else
            {
                foreach (var vault in ownVaults)
                {
                    UnturnedChat.Say(player,
                        VibeVaultPlugin.Instance.Translate("vault_list_item", vault.VaultName, vault.Width, vault.Height),
                        Color.white);
                }
            }

            // Show shared vaults (only when listing own vaults)
            if (command.Length == 0)
            {
                var sharedVaults = storage.GetSharedVaults(targetId);
                foreach (var shared in sharedVaults)
                {
                    var ownerName = GetPlayerName(shared.OwnerId) ?? shared.OwnerId;
                    UnturnedChat.Say(player,
                        VibeVaultPlugin.Instance.Translate("vault_list_shared", ownerName, shared.VaultName, 0, 0),
                        Color.yellow);
                }

                // Show group vault if in a group
                var groupId = player.Player.quests.groupID;
                if (groupId != CSteamID.Nil && player.HasPermission(VibeVaultPlugin.Instance.Configuration.Instance.GroupVault.Permission))
                {
                    var groupConfig = VibeVaultPlugin.Instance.Configuration.Instance.GroupVault;
                    UnturnedChat.Say(player,
                        VibeVaultPlugin.Instance.Translate("vault_list_group", groupConfig.Width, groupConfig.Height),
                        Color.green);
                }
            }
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
    }

    private static string? GetPlayerName(string steamId)
    {
        if (ulong.TryParse(steamId, out var id))
        {
            var target = UnturnedPlayer.FromCSteamID(new CSteamID(id));
            return target?.DisplayName;
        }

        return null;
    }
}
