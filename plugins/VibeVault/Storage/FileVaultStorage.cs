using Newtonsoft.Json;
using VibeVault.Models;

namespace VibeVault.Storage
{
    public class FileVaultStorage : IVaultStorage
    {
        private readonly string _baseDir;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private string PlayersDir => Path.Combine(_baseDir, "players");
        private string GroupsDir => Path.Combine(_baseDir, "groups");
        private string SharedFilePath => Path.Combine(_baseDir, "shared.json");

        public FileVaultStorage(string directoryPath)
        {
            _baseDir = directoryPath;
        }

        public void Initialize()
        {
            Directory.CreateDirectory(PlayersDir);
            Directory.CreateDirectory(GroupsDir);
        }

        public VaultData? LoadVault(string ownerId, string vaultName)
        {
            var path = GetPlayerVaultPath(ownerId, vaultName);
            return ReadJson<VaultData>(path);
        }

        public void SaveVault(VaultData data)
        {
            var path = GetPlayerVaultPath(data.OwnerId, data.VaultName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteJson(path, data);
        }

        public List<VaultData> GetPlayerVaults(string ownerId)
        {
            var dir = Path.Combine(PlayersDir, ownerId);
            var vaults = new List<VaultData>();

            if (!Directory.Exists(dir))
                return vaults;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var vault = ReadJson<VaultData>(file);
                if (vault != null)
                    vaults.Add(vault);
            }

            return vaults;
        }

        public VaultData? LoadGroupVault(string groupId)
        {
            var path = GetGroupVaultPath(groupId);
            return ReadJson<VaultData>(path);
        }

        public void SaveGroupVault(VaultData data)
        {
            Directory.CreateDirectory(GroupsDir);
            var path = GetGroupVaultPath(data.OwnerId);
            WriteJson(path, data);
        }

        public List<SharedVaultAccess> GetSharedVaults(string playerId)
        {
            var allShared = LoadSharedList();
            return allShared.Where(s => s.SharedWithId == playerId).ToList();
        }

        public void ShareVault(SharedVaultAccess access)
        {
            var allShared = LoadSharedList();
            allShared.RemoveAll(s =>
                s.OwnerId == access.OwnerId &&
                s.VaultName == access.VaultName &&
                s.SharedWithId == access.SharedWithId);
            allShared.Add(access);
            WriteJson(SharedFilePath, allShared);
        }

        public void UnshareVault(string ownerId, string vaultName, string sharedWithId)
        {
            var allShared = LoadSharedList();
            allShared.RemoveAll(s =>
                s.OwnerId == ownerId &&
                s.VaultName == vaultName &&
                s.SharedWithId == sharedWithId);
            WriteJson(SharedFilePath, allShared);
        }

        public bool UpgradeVault(string ownerId, string vaultName, byte newWidth, byte newHeight)
        {
            var vault = LoadVault(ownerId, vaultName);
            if (vault == null)
                return false;

            vault.Width = newWidth;
            vault.Height = newHeight;
            SaveVault(vault);
            return true;
        }

        private string GetPlayerVaultPath(string ownerId, string vaultName)
            => Path.Combine(PlayersDir, ownerId, $"{vaultName}.json");

        private string GetGroupVaultPath(string groupId)
            => Path.Combine(GroupsDir, $"{groupId}.json");

        private List<SharedVaultAccess> LoadSharedList()
        {
            return ReadJson<List<SharedVaultAccess>>(SharedFilePath)
                ?? new List<SharedVaultAccess>();
        }

        private T? ReadJson<T>(string path) where T : class
        {
            _semaphore.Wait();
            try
            {
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void WriteJson<T>(string path, T data)
        {
            _semaphore.Wait();
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
