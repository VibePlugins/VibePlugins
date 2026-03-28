using VibeVault.Models;

namespace VibeVault.Storage
{
    public interface IVaultStorage
    {
        void Initialize();
        VaultData? LoadVault(string ownerId, string vaultName);
        void SaveVault(VaultData data);
        List<VaultData> GetPlayerVaults(string ownerId);
        VaultData? LoadGroupVault(string groupId);
        void SaveGroupVault(VaultData data);
        List<SharedVaultAccess> GetSharedVaults(string playerId);
        void ShareVault(SharedVaultAccess access);
        void UnshareVault(string ownerId, string vaultName, string sharedWithId);
        bool UpgradeVault(string ownerId, string vaultName, byte newWidth, byte newHeight);
    }
}
