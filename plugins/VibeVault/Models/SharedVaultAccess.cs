using Newtonsoft.Json;

namespace VibeVault.Models
{
    public class SharedVaultAccess
    {
        [JsonProperty("ownerId")]
        public string OwnerId { get; set; } = string.Empty;

        [JsonProperty("vaultName")]
        public string VaultName { get; set; } = string.Empty;

        [JsonProperty("sharedWithId")]
        public string SharedWithId { get; set; } = string.Empty;

        [JsonProperty("canModify")]
        public bool CanModify { get; set; }
    }
}
