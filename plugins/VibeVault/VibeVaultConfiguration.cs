using Rocket.API;
using VibeVault.Models;

namespace VibeVault
{
    public class VibeVaultConfiguration : IRocketPluginConfiguration
    {
        public string StorageType { get; set; } = "file";
        public string MySqlConnectionString { get; set; } = "Server=localhost;Database=unturned;Uid=root;Pwd=;";
        public ushort StorageBarricadeId { get; set; } = 328;

        public List<VaultTierConfig> VaultTiers { get; set; } = new List<VaultTierConfig>();
        public GroupVaultConfig GroupVault { get; set; } = new GroupVaultConfig();
        public TrashConfig Trash { get; set; } = new TrashConfig();
        public List<VaultUpgradeConfig> Upgrades { get; set; } = new List<VaultUpgradeConfig>();

        public void LoadDefaults()
        {
            StorageType = "file";
            MySqlConnectionString = "Server=localhost;Database=unturned;Uid=root;Pwd=;";
            StorageBarricadeId = 328;

            VaultTiers = new List<VaultTierConfig>
            {
                new VaultTierConfig
                {
                    Name = "default",
                    Permission = "vibevault.vault.default",
                    Width = 8,
                    Height = 4,
                    Priority = 0
                },
                new VaultTierConfig
                {
                    Name = "vip",
                    Permission = "vibevault.vault.vip",
                    Width = 10,
                    Height = 6,
                    Priority = 1
                },
                new VaultTierConfig
                {
                    Name = "elite",
                    Permission = "vibevault.vault.elite",
                    Width = 12,
                    Height = 8,
                    Priority = 2
                }
            };

            GroupVault = new GroupVaultConfig
            {
                Width = 8,
                Height = 6,
                Permission = "vibevault.group"
            };

            Trash = new TrashConfig
            {
                Width = 8,
                Height = 8,
                Permission = "vibevault.trash"
            };

            Upgrades = new List<VaultUpgradeConfig>
            {
                new VaultUpgradeConfig
                {
                    FromTier = "default",
                    ToTier = "vip",
                    ExperienceCost = 1000,
                    Permission = "vibevault.upgrade"
                },
                new VaultUpgradeConfig
                {
                    FromTier = "vip",
                    ToTier = "elite",
                    ExperienceCost = 5000,
                    Permission = "vibevault.upgrade"
                }
            };
        }
    }
}
