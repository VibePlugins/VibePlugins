using VibeVault.Models;
using VibeVault.Services;
using VibeVault.Storage;

namespace VibeVault.API
{
    public class VibeVaultAPI : IVibeVaultAPI
    {
        private readonly IVaultStorage _storage;
        private readonly VaultManager _manager;

        public event Action<string, string>? OnVaultOpened;
        public event Action<string, string>? OnVaultClosed;

        public VibeVaultAPI(IVaultStorage storage, VaultManager manager)
        {
            _storage = storage;
            _manager = manager;
        }

        public VaultData? GetVault(string ownerId, string vaultName)
        {
            return _storage.LoadVault(ownerId, vaultName);
        }

        public bool SaveVault(VaultData data)
        {
            _storage.SaveVault(data);
            return true;
        }

        public List<VaultData> GetPlayerVaults(string ownerId)
        {
            return _storage.GetPlayerVaults(ownerId);
        }

        public bool IsVaultOpen(string ownerId, string vaultName)
        {
            return _manager.IsVaultOpen(ownerId, vaultName);
        }

        public bool ShareVault(string ownerId, string vaultName, string targetId, bool canModify)
        {
            var access = new SharedVaultAccess
            {
                OwnerId = ownerId,
                VaultName = vaultName,
                SharedWithId = targetId,
                CanModify = canModify
            };
            _storage.ShareVault(access);
            return true;
        }

        public bool UnshareVault(string ownerId, string vaultName, string targetId)
        {
            _storage.UnshareVault(ownerId, vaultName, targetId);
            return true;
        }

        public List<SharedVaultAccess> GetSharedVaults(string playerId)
        {
            return _storage.GetSharedVaults(playerId);
        }

        internal void RaiseVaultOpened(string ownerId, string vaultName)
        {
            OnVaultOpened?.Invoke(ownerId, vaultName);
        }

        internal void RaiseVaultClosed(string ownerId, string vaultName)
        {
            OnVaultClosed?.Invoke(ownerId, vaultName);
        }
    }
}
