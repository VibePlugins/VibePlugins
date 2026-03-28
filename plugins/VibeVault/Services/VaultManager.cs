using Rocket.Core.Logging;
using UnityEngine;

namespace VibeVault.Services
{
    public class OpenVaultInfo
    {
        public string OwnerId { get; set; } = string.Empty;
        public string VaultName { get; set; } = string.Empty;
        public Transform? BarricadeTransform { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsGroupVault { get; set; }
    }

    public class VaultManager
    {
        // playerId -> open vault info
        private readonly Dictionary<string, OpenVaultInfo> _openVaults = new();

        // groupId -> playerId who has the group vault open
        private readonly Dictionary<string, string> _groupVaultLocks = new();

        public bool TryOpenVault(string playerId, OpenVaultInfo info)
        {
            if (_openVaults.ContainsKey(playerId))
            {
                Rocket.Core.Logging.Logger.LogWarning($"[VibeVault] Player {playerId} already has a vault open.");
                return false;
            }

            _openVaults[playerId] = info;
            return true;
        }

        public OpenVaultInfo? GetOpenVault(string playerId)
        {
            return _openVaults.TryGetValue(playerId, out var info) ? info : null;
        }

        public void CloseVault(string playerId)
        {
            if (_openVaults.TryGetValue(playerId, out var info))
            {
                if (info.IsGroupVault)
                {
                    // Find and release the group vault lock held by this player
                    var groupId = _groupVaultLocks
                        .Where(kvp => kvp.Value == playerId)
                        .Select(kvp => kvp.Key)
                        .FirstOrDefault();

                    if (groupId != null)
                    {
                        UnlockGroupVault(groupId);
                    }
                }

                _openVaults.Remove(playerId);
            }
        }

        public bool TryLockGroupVault(string groupId, string playerId)
        {
            if (_groupVaultLocks.ContainsKey(groupId))
            {
                return false;
            }

            _groupVaultLocks[groupId] = playerId;
            return true;
        }

        public void UnlockGroupVault(string groupId)
        {
            _groupVaultLocks.Remove(groupId);
        }

        public string? GetGroupVaultHolder(string groupId)
        {
            return _groupVaultLocks.TryGetValue(groupId, out var playerId) ? playerId : null;
        }

        public bool IsVaultOpen(string ownerId, string vaultName)
        {
            return _openVaults.Values.Any(v =>
                v.OwnerId == ownerId &&
                string.Equals(v.VaultName, vaultName, StringComparison.OrdinalIgnoreCase));
        }

        public void Cleanup(string playerId)
        {
            CloseVault(playerId);
            Rocket.Core.Logging.Logger.Log($"[VibeVault] Cleaned up vault state for player {playerId}.");
        }

        public void CloseAllVaults(Action<string> closeAction)
        {
            var playerIds = _openVaults.Keys.ToList();
            foreach (var playerId in playerIds)
            {
                try
                {
                    closeAction(playerId);
                }
                catch (Exception ex)
                {
                    Rocket.Core.Logging.Logger.LogError($"[VibeVault] Error closing vault for {playerId} during shutdown: {ex.Message}");
                }
            }

            _openVaults.Clear();
            _groupVaultLocks.Clear();
        }
    }
}
