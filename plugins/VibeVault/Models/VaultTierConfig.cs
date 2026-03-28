using System.Xml.Serialization;

namespace VibeVault.Models
{
    public class VaultTierConfig
    {
        [XmlAttribute]
        public string Name { get; set; } = "default";

        [XmlAttribute]
        public string Permission { get; set; } = "vibevault.vault.default";

        [XmlAttribute]
        public byte Width { get; set; } = 8;

        [XmlAttribute]
        public byte Height { get; set; } = 4;

        [XmlAttribute]
        public int Priority { get; set; } = 0;
    }

    public class GroupVaultConfig
    {
        [XmlAttribute]
        public byte Width { get; set; } = 8;

        [XmlAttribute]
        public byte Height { get; set; } = 6;

        [XmlAttribute]
        public string Permission { get; set; } = "vibevault.group";
    }

    public class TrashConfig
    {
        [XmlAttribute]
        public byte Width { get; set; } = 8;

        [XmlAttribute]
        public byte Height { get; set; } = 8;

        [XmlAttribute]
        public string Permission { get; set; } = "vibevault.trash";
    }

    public class VaultUpgradeConfig
    {
        [XmlAttribute]
        public string FromTier { get; set; } = "default";

        [XmlAttribute]
        public string ToTier { get; set; } = "vip";

        [XmlAttribute]
        public uint ExperienceCost { get; set; } = 1000;

        [XmlAttribute]
        public string Permission { get; set; } = "vibevault.upgrade";
    }
}
