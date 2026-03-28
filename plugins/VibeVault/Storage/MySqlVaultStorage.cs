using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using VibeVault.Models;

namespace VibeVault.Storage
{
    public class MySqlVaultStorage : IVaultStorage
    {
        private readonly string _connectionString;

        public MySqlVaultStorage(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Initialize()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS vibevault_vaults (
                    owner_id VARCHAR(32) NOT NULL,
                    vault_name VARCHAR(64) NOT NULL,
                    width TINYINT UNSIGNED NOT NULL,
                    height TINYINT UNSIGNED NOT NULL,
                    items_json MEDIUMTEXT NOT NULL,
                    PRIMARY KEY (owner_id, vault_name)
                );

                CREATE TABLE IF NOT EXISTS vibevault_group_vaults (
                    group_id VARCHAR(64) NOT NULL,
                    width TINYINT UNSIGNED NOT NULL,
                    height TINYINT UNSIGNED NOT NULL,
                    items_json MEDIUMTEXT NOT NULL,
                    PRIMARY KEY (group_id)
                );

                CREATE TABLE IF NOT EXISTS vibevault_shared (
                    owner_id VARCHAR(32) NOT NULL,
                    vault_name VARCHAR(64) NOT NULL,
                    shared_with_id VARCHAR(32) NOT NULL,
                    can_modify TINYINT(1) NOT NULL DEFAULT 0,
                    PRIMARY KEY (owner_id, vault_name, shared_with_id)
                );";
            cmd.ExecuteNonQuery();
        }

        public VaultData? LoadVault(string ownerId, string vaultName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT width, height, items_json FROM vibevault_vaults WHERE owner_id = @ownerId AND vault_name = @vaultName;";
            cmd.Parameters.AddWithValue("@ownerId", ownerId);
            cmd.Parameters.AddWithValue("@vaultName", vaultName);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new VaultData
            {
                OwnerId = ownerId,
                VaultName = vaultName,
                Width = (byte)Convert.ToByte(reader["width"]),
                Height = (byte)Convert.ToByte(reader["height"]),
                Items = DeserializeItems(reader["items_json"].ToString()!)
            };
        }

        public void SaveVault(VaultData data)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO vibevault_vaults (owner_id, vault_name, width, height, items_json)
                VALUES (@ownerId, @vaultName, @width, @height, @itemsJson)
                ON DUPLICATE KEY UPDATE width = @width, height = @height, items_json = @itemsJson;";
            cmd.Parameters.AddWithValue("@ownerId", data.OwnerId);
            cmd.Parameters.AddWithValue("@vaultName", data.VaultName);
            cmd.Parameters.AddWithValue("@width", data.Width);
            cmd.Parameters.AddWithValue("@height", data.Height);
            cmd.Parameters.AddWithValue("@itemsJson", SerializeItems(data.Items));

            cmd.ExecuteNonQuery();
        }

        public List<VaultData> GetPlayerVaults(string ownerId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT vault_name, width, height, items_json FROM vibevault_vaults WHERE owner_id = @ownerId;";
            cmd.Parameters.AddWithValue("@ownerId", ownerId);

            var vaults = new List<VaultData>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                vaults.Add(new VaultData
                {
                    OwnerId = ownerId,
                    VaultName = reader["vault_name"].ToString()!,
                    Width = (byte)Convert.ToByte(reader["width"]),
                    Height = (byte)Convert.ToByte(reader["height"]),
                    Items = DeserializeItems(reader["items_json"].ToString()!)
                });
            }

            return vaults;
        }

        public VaultData? LoadGroupVault(string groupId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT width, height, items_json FROM vibevault_group_vaults WHERE group_id = @groupId;";
            cmd.Parameters.AddWithValue("@groupId", groupId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new VaultData
            {
                OwnerId = groupId,
                VaultName = "group",
                Width = (byte)Convert.ToByte(reader["width"]),
                Height = (byte)Convert.ToByte(reader["height"]),
                Items = DeserializeItems(reader["items_json"].ToString()!)
            };
        }

        public void SaveGroupVault(VaultData data)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO vibevault_group_vaults (group_id, width, height, items_json)
                VALUES (@groupId, @width, @height, @itemsJson)
                ON DUPLICATE KEY UPDATE width = @width, height = @height, items_json = @itemsJson;";
            cmd.Parameters.AddWithValue("@groupId", data.OwnerId);
            cmd.Parameters.AddWithValue("@width", data.Width);
            cmd.Parameters.AddWithValue("@height", data.Height);
            cmd.Parameters.AddWithValue("@itemsJson", SerializeItems(data.Items));

            cmd.ExecuteNonQuery();
        }

        public List<SharedVaultAccess> GetSharedVaults(string playerId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT owner_id, vault_name, can_modify FROM vibevault_shared WHERE shared_with_id = @playerId;";
            cmd.Parameters.AddWithValue("@playerId", playerId);

            var results = new List<SharedVaultAccess>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new SharedVaultAccess
                {
                    OwnerId = reader["owner_id"].ToString()!,
                    VaultName = reader["vault_name"].ToString()!,
                    SharedWithId = playerId,
                    CanModify = Convert.ToBoolean(reader["can_modify"])
                });
            }

            return results;
        }

        public void ShareVault(SharedVaultAccess access)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO vibevault_shared (owner_id, vault_name, shared_with_id, can_modify)
                VALUES (@ownerId, @vaultName, @sharedWithId, @canModify)
                ON DUPLICATE KEY UPDATE can_modify = @canModify;";
            cmd.Parameters.AddWithValue("@ownerId", access.OwnerId);
            cmd.Parameters.AddWithValue("@vaultName", access.VaultName);
            cmd.Parameters.AddWithValue("@sharedWithId", access.SharedWithId);
            cmd.Parameters.AddWithValue("@canModify", access.CanModify);

            cmd.ExecuteNonQuery();
        }

        public void UnshareVault(string ownerId, string vaultName, string sharedWithId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM vibevault_shared WHERE owner_id = @ownerId AND vault_name = @vaultName AND shared_with_id = @sharedWithId;";
            cmd.Parameters.AddWithValue("@ownerId", ownerId);
            cmd.Parameters.AddWithValue("@vaultName", vaultName);
            cmd.Parameters.AddWithValue("@sharedWithId", sharedWithId);

            cmd.ExecuteNonQuery();
        }

        public bool UpgradeVault(string ownerId, string vaultName, byte newWidth, byte newHeight)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE vibevault_vaults SET width = @width, height = @height
                WHERE owner_id = @ownerId AND vault_name = @vaultName;";
            cmd.Parameters.AddWithValue("@ownerId", ownerId);
            cmd.Parameters.AddWithValue("@vaultName", vaultName);
            cmd.Parameters.AddWithValue("@width", newWidth);
            cmd.Parameters.AddWithValue("@height", newHeight);

            var rows = cmd.ExecuteNonQuery();
            return rows > 0;
        }

        private static string SerializeItems(List<VaultItem> items)
            => JsonConvert.SerializeObject(items);

        private static List<VaultItem> DeserializeItems(string json)
            => JsonConvert.DeserializeObject<List<VaultItem>>(json) ?? new List<VaultItem>();
    }
}
