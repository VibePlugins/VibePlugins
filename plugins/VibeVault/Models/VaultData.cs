using Newtonsoft.Json;

namespace VibeVault.Models
{
    public class VaultData
    {
        [JsonProperty("ownerId")]
        public string OwnerId { get; set; } = string.Empty;

        [JsonProperty("vaultName")]
        public string VaultName { get; set; } = string.Empty;

        [JsonProperty("width")]
        public byte Width { get; set; }

        [JsonProperty("height")]
        public byte Height { get; set; }

        [JsonProperty("items")]
        public List<VaultItem> Items { get; set; } = new List<VaultItem>();
    }
}
