using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using VibeVault.Models;
using VibeVault.Storage;

namespace VibeVault.Services
{
    public class VaultService
    {
        private readonly IVaultStorage _storage;
        private readonly VaultManager _manager;
        private readonly VibeVaultConfiguration _config;
        private readonly API.VibeVaultAPI? _api;

        public IVaultStorage Storage => _storage;

        public VaultService(IVaultStorage storage, VaultManager manager, VibeVaultConfiguration config, API.VibeVaultAPI? api = null)
        {
            _storage = storage;
            _manager = manager;
            _config = config;
            _api = api;
        }

        public VaultTierConfig? GetBestVaultTier(IRocketPlayer player)
        {
            return _config.VaultTiers
                .Where(tier => player.HasPermission(tier.Permission))
                .OrderByDescending(tier => tier.Priority)
                .FirstOrDefault();
        }

        public VaultTierConfig? GetVaultTierByName(string name)
        {
            return _config.VaultTiers
                .FirstOrDefault(tier => string.Equals(tier.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public bool OpenVault(UnturnedPlayer player, string? vaultName = null)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();

            // Determine the vault tier
            VaultTierConfig? tier;
            if (vaultName != null)
            {
                tier = GetVaultTierByName(vaultName);
                if (tier == null)
                {
                    UnturnedChat.Say(player, $"Vault tier '{vaultName}' does not exist.");
                    return false;
                }

                if (!player.HasPermission(tier.Permission))
                {
                    UnturnedChat.Say(player, "You do not have permission for this vault tier.");
                    return false;
                }
            }
            else
            {
                tier = GetBestVaultTier(player);
                if (tier == null)
                {
                    UnturnedChat.Say(player, "You do not have permission to use any vault.");
                    return false;
                }

                vaultName = tier.Name;
            }

            // Close existing vault if open
            var existing = _manager.GetOpenVault(playerId);
            if (existing != null)
            {
                CloseVault(player);
            }

            // Check if this vault is already open by another player
            if (_manager.IsVaultOpen(playerId, vaultName))
            {
                UnturnedChat.Say(player, "This vault is already open.");
                return false;
            }

            // Load or create vault data
            var data = _storage.LoadVault(playerId, vaultName);
            if (data == null)
            {
                data = new VaultData
                {
                    OwnerId = playerId,
                    VaultName = vaultName,
                    Width = tier.Width,
                    Height = tier.Height
                };
            }

            // Spawn barricade and open storage
            return SpawnAndOpenStorage(player, playerId, data, isReadOnly: false, isGroupVault: false);
        }

        public bool OpenOtherVault(UnturnedPlayer player, string targetId, string vaultName)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();

            // Close existing vault if open
            var existing = _manager.GetOpenVault(playerId);
            if (existing != null)
            {
                CloseVault(player);
            }

            // Check if vault is already open
            if (_manager.IsVaultOpen(targetId, vaultName))
            {
                UnturnedChat.Say(player, "That vault is currently in use by another player.");
                return false;
            }

            var data = _storage.LoadVault(targetId, vaultName);
            if (data == null)
            {
                UnturnedChat.Say(player, "That vault does not exist.");
                return false;
            }

            return SpawnAndOpenStorage(player, targetId, data, isReadOnly: false, isGroupVault: false);
        }

        public bool OpenGroupVault(UnturnedPlayer player)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();
            var groupId = player.Player.quests.groupID.m_SteamID.ToString();

            if (!player.HasPermission(_config.GroupVault.Permission))
            {
                UnturnedChat.Say(player, "You do not have permission to use the group vault.");
                return false;
            }

            if (player.Player.quests.groupID.m_SteamID == 0)
            {
                UnturnedChat.Say(player, "You are not in a group.");
                return false;
            }

            // Try to acquire the group vault lock
            if (!_manager.TryLockGroupVault(groupId, playerId))
            {
                var holder = _manager.GetGroupVaultHolder(groupId);
                UnturnedChat.Say(player, $"The group vault is currently in use by another member.");
                return false;
            }

            // Close existing vault if open
            var existing = _manager.GetOpenVault(playerId);
            if (existing != null)
            {
                CloseVault(player);
            }

            // Load or create group vault data
            var data = _storage.LoadGroupVault(groupId);
            if (data == null)
            {
                data = new VaultData
                {
                    OwnerId = groupId,
                    VaultName = "group",
                    Width = _config.GroupVault.Width,
                    Height = _config.GroupVault.Height
                };
            }

            var success = SpawnAndOpenStorage(player, groupId, data, isReadOnly: false, isGroupVault: true);
            if (!success)
            {
                _manager.UnlockGroupVault(groupId);
            }

            return success;
        }

        public bool OpenReadOnlyVault(UnturnedPlayer player, string targetId, string vaultName)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();

            // Close existing vault if open
            var existing = _manager.GetOpenVault(playerId);
            if (existing != null)
            {
                CloseVault(player);
            }

            var data = _storage.LoadVault(targetId, vaultName);
            if (data == null)
            {
                UnturnedChat.Say(player, "That vault does not exist.");
                return false;
            }

            return SpawnAndOpenStorage(player, targetId, data, isReadOnly: true, isGroupVault: false);
        }

        public bool OpenTrash(UnturnedPlayer player)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();

            if (!player.HasPermission(_config.Trash.Permission))
            {
                UnturnedChat.Say(player, "You do not have permission to use the trash.");
                return false;
            }

            // Close existing vault if open
            var existing = _manager.GetOpenVault(playerId);
            if (existing != null)
            {
                CloseVault(player);
            }

            // Trash vault is always empty - items placed in it are discarded on close
            var data = new VaultData
            {
                OwnerId = playerId,
                VaultName = "__trash__",
                Width = _config.Trash.Width,
                Height = _config.Trash.Height
            };

            return SpawnAndOpenStorage(player, playerId, data, isReadOnly: false, isGroupVault: false);
        }

        public void CloseVault(UnturnedPlayer player)
        {
            var playerId = player.CSteamID.m_SteamID.ToString();
            CloseVaultInternal(playerId);
        }

        public void OnStorageClosed(string playerId)
        {
            try
            {
                CloseVaultInternal(playerId);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[VibeVault] Error closing vault for {playerId}: {ex.Message}");
            }
        }

        private void CloseVaultInternal(string playerId)
        {
            var info = _manager.GetOpenVault(playerId);
            if (info == null)
            {
                return;
            }

            try
            {
                if (info.BarricadeTransform != null)
                {
                    // Save items unless it's a trash vault or read-only
                    bool isTrash = info.VaultName == "__trash__";

                    if (!info.IsReadOnly && !isTrash)
                    {
                        var drop = BarricadeManager.FindBarricadeByRootTransform(info.BarricadeTransform);
                        if (drop?.interactable is InteractableStorage storage)
                        {
                            var items = new List<VaultItem>();
                            foreach (var jar in storage.items.items)
                            {
                                items.Add(new VaultItem
                                {
                                    Id = jar.item.id,
                                    Amount = jar.item.amount,
                                    Quality = jar.item.quality,
                                    State = jar.item.state,
                                    X = jar.x,
                                    Y = jar.y,
                                    Rot = jar.rot
                                });
                            }

                            var data = new VaultData
                            {
                                OwnerId = info.OwnerId,
                                VaultName = info.VaultName,
                                Width = storage.items.width,
                                Height = storage.items.height,
                                Items = items
                            };

                            if (info.IsGroupVault)
                            {
                                _storage.SaveGroupVault(data);
                            }
                            else
                            {
                                _storage.SaveVault(data);
                            }

                            Rocket.Core.Logging.Logger.Log($"[VibeVault] Saved vault '{info.VaultName}' for {info.OwnerId} with {items.Count} items.");
                        }
                    }

                    // Destroy the barricade
                    DestroyBarricade(info.BarricadeTransform);
                }
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[VibeVault] Error saving vault for {playerId}: {ex.Message}");
            }
            finally
            {
                var closedInfo = _manager.GetOpenVault(playerId);
                _manager.CloseVault(playerId);
                if (closedInfo != null)
                {
                    _api?.RaiseVaultClosed(closedInfo.OwnerId, closedInfo.VaultName);
                }
            }
        }

        private bool SpawnAndOpenStorage(
            UnturnedPlayer player,
            string ownerId,
            VaultData data,
            bool isReadOnly,
            bool isGroupVault)
        {
            try
            {
                var barricade = new Barricade(_config.StorageBarricadeId);
                var position = player.Position + new Vector3(0, -100, 0);
                var transform = BarricadeManager.dropNonPlantedBarricade(
                    barricade, position, Quaternion.identity, 0, 0);

                if (transform == null)
                {
                    Rocket.Core.Logging.Logger.LogError("[VibeVault] Failed to spawn storage barricade.");
                    UnturnedChat.Say(player, "Failed to open vault. Please try again.");
                    return false;
                }

                var drop = BarricadeManager.FindBarricadeByRootTransform(transform);
                if (drop?.interactable is not InteractableStorage storage)
                {
                    Rocket.Core.Logging.Logger.LogError("[VibeVault] Spawned barricade is not a storage.");
                    DestroyBarricade(transform);
                    UnturnedChat.Say(player, "Failed to open vault. Invalid storage barricade.");
                    return false;
                }

                // Resize and load items
                storage.items.resize(data.Width, data.Height);

                foreach (var vaultItem in data.Items)
                {
                    var item = new Item(vaultItem.Id, vaultItem.Amount, vaultItem.Quality, vaultItem.State);
                    storage.items.loadItem(vaultItem.X, vaultItem.Y, vaultItem.Rot, item);
                }

                storage.shouldCloseWhenOutsideRange = false;

                // Register in manager
                var playerId = player.CSteamID.m_SteamID.ToString();
                var vaultInfo = new OpenVaultInfo
                {
                    OwnerId = ownerId,
                    VaultName = data.VaultName,
                    BarricadeTransform = transform,
                    IsReadOnly = isReadOnly,
                    IsGroupVault = isGroupVault
                };

                if (!_manager.TryOpenVault(playerId, vaultInfo))
                {
                    Rocket.Core.Logging.Logger.LogError($"[VibeVault] Failed to register vault for player {playerId}.");
                    DestroyBarricade(transform);
                    UnturnedChat.Say(player, "Failed to open vault. Please try again.");
                    return false;
                }

                // Open storage for player
                player.Player.inventory.openStorage(storage);

                _api?.RaiseVaultOpened(ownerId, data.VaultName);

                Rocket.Core.Logging.Logger.Log($"[VibeVault] Opened vault '{data.VaultName}' for player {playerId} " +
                           $"(owner: {ownerId}, readonly: {isReadOnly}, group: {isGroupVault}).");

                return true;
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[VibeVault] Error spawning vault storage: {ex.Message}");
                UnturnedChat.Say(player, "An error occurred while opening the vault.");
                return false;
            }
        }

        private static void DestroyBarricade(Transform transform)
        {
            try
            {
                if (BarricadeManager.tryGetRegion(transform, out byte x, out byte y, out ushort plant, out BarricadeRegion region))
                {
                    var drop = BarricadeManager.FindBarricadeByRootTransform(transform);
                    if (drop != null)
                    {
                        BarricadeManager.destroyBarricade(drop, x, y, plant);
                    }
                }
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[VibeVault] Error destroying barricade: {ex.Message}");
            }
        }
    }
}
