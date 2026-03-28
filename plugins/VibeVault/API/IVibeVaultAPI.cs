using VibeVault.Models;

namespace VibeVault.API
{
    public interface IVibeVaultAPI
    {
        VaultData? GetVault(string ownerId, string vaultName);
        bool SaveVault(VaultData data);
        List<VaultData> GetPlayerVaults(string ownerId);
        bool IsVaultOpen(string ownerId, string vaultName);
        bool ShareVault(string ownerId, string vaultName, string targetId, bool canModify);
        bool UnshareVault(string ownerId, string vaultName, string targetId);
        List<SharedVaultAccess> GetSharedVaults(string playerId);

        event Action<string, string>? OnVaultOpened;
        event Action<string, string>? OnVaultClosed;
    }
}
