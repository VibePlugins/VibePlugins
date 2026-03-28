using Newtonsoft.Json;

namespace VibePlugins.RocketMod.TestBase.Shared.Protocol
{
    /// <summary>
    /// Represents a chat message event captured by the event monitoring system.
    /// </summary>
    public class ChatMessageEvent
    {
        /// <summary>The text content of the chat message.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        /// <summary>The display name of the sender, if any.</summary>
        [JsonProperty("senderName")]
        public string SenderName { get; set; }

        /// <summary>The display name of the intended recipient, if any.</summary>
        [JsonProperty("recipientName")]
        public string RecipientName { get; set; }

        /// <summary>The chat mode (e.g. "Global", "Local", "Group").</summary>
        [JsonProperty("mode")]
        public string Mode { get; set; }
    }

    /// <summary>
    /// Raised when a player connects to the server.
    /// </summary>
    public class PlayerConnectedEvent
    {
        /// <summary>The display name of the player who connected.</summary>
        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        /// <summary>The Steam64 ID of the player.</summary>
        [JsonProperty("steamId")]
        public ulong SteamId { get; set; }
    }

    /// <summary>
    /// Raised when a player disconnects from the server.
    /// </summary>
    public class PlayerDisconnectedEvent
    {
        /// <summary>The display name of the player who disconnected.</summary>
        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        /// <summary>The Steam64 ID of the player.</summary>
        [JsonProperty("steamId")]
        public ulong SteamId { get; set; }
    }

    /// <summary>
    /// Raised when a player dies.
    /// </summary>
    public class PlayerDeathEvent
    {
        /// <summary>The display name of the player who died.</summary>
        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        /// <summary>The display name of the killer, if any.</summary>
        [JsonProperty("killerName")]
        public string KillerName { get; set; }

        /// <summary>The cause of death (e.g. "Gun", "Zombie", "Fall").</summary>
        [JsonProperty("cause")]
        public string Cause { get; set; }

        /// <summary>The body part that was hit (e.g. "Head", "Torso").</summary>
        [JsonProperty("limb")]
        public string Limb { get; set; }
    }

    /// <summary>
    /// Raised when a player takes damage.
    /// </summary>
    public class PlayerDamageEvent
    {
        /// <summary>The display name of the player who took damage.</summary>
        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        /// <summary>The amount of damage dealt.</summary>
        [JsonProperty("damage")]
        public float Damage { get; set; }

        /// <summary>The cause of the damage (e.g. "Gun", "Zombie", "Fall").</summary>
        [JsonProperty("cause")]
        public string Cause { get; set; }
    }

    /// <summary>
    /// Raised when a player enters a vehicle.
    /// </summary>
    public class VehicleEnterEvent
    {
        /// <summary>The display name of the player who entered the vehicle.</summary>
        [JsonProperty("playerName")]
        public string PlayerName { get; set; }

        /// <summary>The instance ID of the vehicle.</summary>
        [JsonProperty("vehicleId")]
        public uint VehicleId { get; set; }
    }
}
