using Newtonsoft.Json;

namespace VibeVault.Models
{
    public class VaultItem
    {
        [JsonProperty("id")]
        public ushort Id { get; set; }

        [JsonProperty("amount")]
        public byte Amount { get; set; }

        [JsonProperty("quality")]
        public byte Quality { get; set; }

        [JsonProperty("state")]
        public byte[] State { get; set; } = Array.Empty<byte>();

        [JsonProperty("x")]
        public byte X { get; set; }

        [JsonProperty("y")]
        public byte Y { get; set; }

        [JsonProperty("rot")]
        public byte Rot { get; set; }
    }
}
