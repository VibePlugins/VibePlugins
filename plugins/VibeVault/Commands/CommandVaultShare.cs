using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using UnityEngine;
using VibeVault.Models;

namespace VibeVault.Commands;

public class CommandVaultShare : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;
    public string Name => "vaultshare";
    public string Help => "Share or unshare a vault with another player.";
    public string Syntax => "/vaultshare <player> [vault] [readonly] | /vaultshare remove <player> [vault]";
    public List<string> Aliases => new List<string> { "vshare", "vs" };
    public List<string> Permissions => new List<string> { "vibevault.share" };

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

            var service = VibeVaultPlugin.Instance.VaultService;
            var storage = service.Storage;
            var ownerId = player.CSteamID.m_SteamID.ToString();

            // Handle unshare: /vaultshare remove <player> [vault]
            if (command[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                if (command.Length < 2)
                {
                    UnturnedChat.Say(player, $"Usage: /vaultshare remove <player> [vault]", Color.yellow);
                    return;
                }

                var targetName = command[1];
                var target = UnturnedPlayer.FromName(targetName);

                if (target == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_player_not_found", targetName), Color.red);
                    return;
                }

                var targetId = target.CSteamID.m_SteamID.ToString();
                string vaultName;

                if (command.Length >= 3)
                {
                    vaultName = command[2];
                }
                else
                {
                    var tier = service.GetBestVaultTier(player);
                    if (tier == null)
                    {
                        UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                        return;
                    }

                    vaultName = tier.Name;
                }

                storage.UnshareVault(ownerId, vaultName, targetId);
                UnturnedChat.Say(player,
                    VibeVaultPlugin.Instance.Translate("vault_unshared", target.DisplayName, vaultName),
                    Color.green);
                return;
            }

            // Handle share: /vaultshare <player> [vault] [readonly]
            var shareTargetName = command[0];
            var shareTarget = UnturnedPlayer.FromName(shareTargetName);

            if (shareTarget == null)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_player_not_found", shareTargetName), Color.red);
                return;
            }

            var shareTargetId = shareTarget.CSteamID.m_SteamID.ToString();

            if (shareTargetId == ownerId)
            {
                UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_share_self"), Color.red);
                return;
            }

            string shareVaultName;
            bool canModify = true;

            if (command.Length >= 2)
            {
                shareVaultName = command[1];

                if (command.Length >= 3 && command[2].Equals("readonly", StringComparison.OrdinalIgnoreCase))
                {
                    canModify = false;
                }
            }
            else
            {
                var tier = service.GetBestVaultTier(player);
                if (tier == null)
                {
                    UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("vault_no_permission"), Color.red);
                    return;
                }

                shareVaultName = tier.Name;
            }

            var access = new SharedVaultAccess
            {
                OwnerId = ownerId,
                VaultName = shareVaultName,
                SharedWithId = shareTargetId,
                CanModify = canModify
            };

            storage.ShareVault(access);
            UnturnedChat.Say(player,
                VibeVaultPlugin.Instance.Translate("vault_shared", shareVaultName, shareTarget.DisplayName),
                Color.green);
        }
        catch (Exception ex)
        {
            Rocket.Core.Logging.Logger.LogException(ex);
            UnturnedChat.Say(player, VibeVaultPlugin.Instance.Translate("error_occurred"), Color.red);
        }
    }
}
