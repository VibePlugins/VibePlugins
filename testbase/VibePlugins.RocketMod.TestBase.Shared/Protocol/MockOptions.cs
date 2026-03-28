using Newtonsoft.Json;

namespace VibePlugins.RocketMod.TestBase.Shared.Protocol
{
    /// <summary>
    /// Options for creating a mock player entity on the server.
    /// Supports a fluent builder pattern for convenient construction.
    /// </summary>
    public class PlayerOptions
    {
        /// <summary>The display name of the mock player.</summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "TestPlayer";

        /// <summary>The Steam64 ID to assign, or <c>null</c> for auto-generation.</summary>
        [JsonProperty("steamId")]
        public ulong? SteamId { get; set; }

        /// <summary>Starting health value (0-255).</summary>
        [JsonProperty("health")]
        public byte Health { get; set; } = 100;

        /// <summary>Maximum health value (0-255).</summary>
        [JsonProperty("maxHealth")]
        public byte MaxHealth { get; set; } = 100;

        /// <summary>Whether the mock player has admin privileges.</summary>
        [JsonProperty("isAdmin")]
        public bool IsAdmin { get; set; }

        /// <summary>World position as [x, y, z]. Defaults to origin.</summary>
        [JsonProperty("position")]
        public float[] Position { get; set; } = new float[] { 0f, 0f, 0f };

        /// <summary>Starting experience points.</summary>
        [JsonProperty("experience")]
        public uint Experience { get; set; }

        /// <summary>Sets the player display name.</summary>
        public PlayerOptions WithName(string name)
        {
            Name = name;
            return this;
        }

        /// <summary>Sets the player Steam64 ID.</summary>
        public PlayerOptions WithSteamId(ulong steamId)
        {
            SteamId = steamId;
            return this;
        }

        /// <summary>Sets the player's current health.</summary>
        public PlayerOptions WithHealth(byte health)
        {
            Health = health;
            return this;
        }

        /// <summary>Sets the player's maximum health.</summary>
        public PlayerOptions WithMaxHealth(byte maxHealth)
        {
            MaxHealth = maxHealth;
            return this;
        }

        /// <summary>Grants admin privileges to the mock player.</summary>
        public PlayerOptions AsAdmin()
        {
            IsAdmin = true;
            return this;
        }

        /// <summary>Sets the spawn position.</summary>
        public PlayerOptions AtPosition(float x, float y, float z)
        {
            Position = new float[] { x, y, z };
            return this;
        }

        /// <summary>Sets the starting experience points.</summary>
        public PlayerOptions WithExperience(uint experience)
        {
            Experience = experience;
            return this;
        }
    }

    /// <summary>
    /// Options for creating a mock zombie entity on the server.
    /// Supports a fluent builder pattern for convenient construction.
    /// </summary>
    public class ZombieOptions
    {
        /// <summary>World position as [x, y, z]. Defaults to origin.</summary>
        [JsonProperty("position")]
        public float[] Position { get; set; } = new float[] { 0f, 0f, 0f };

        /// <summary>Starting health value.</summary>
        [JsonProperty("health")]
        public ushort Health { get; set; } = 100;

        /// <summary>Zombie speciality/type identifier.</summary>
        [JsonProperty("speciality")]
        public byte Speciality { get; set; }

        /// <summary>Sets the spawn position.</summary>
        public ZombieOptions AtPosition(float x, float y, float z)
        {
            Position = new float[] { x, y, z };
            return this;
        }

        /// <summary>Sets the zombie's starting health.</summary>
        public ZombieOptions WithHealth(ushort health)
        {
            Health = health;
            return this;
        }

        /// <summary>Sets the zombie speciality/type.</summary>
        public ZombieOptions WithSpeciality(byte speciality)
        {
            Speciality = speciality;
            return this;
        }
    }

    /// <summary>
    /// Options for creating a mock animal entity on the server.
    /// Supports a fluent builder pattern for convenient construction.
    /// </summary>
    public class AnimalOptions
    {
        /// <summary>World position as [x, y, z]. Defaults to origin.</summary>
        [JsonProperty("position")]
        public float[] Position { get; set; } = new float[] { 0f, 0f, 0f };

        /// <summary>Starting health value.</summary>
        [JsonProperty("health")]
        public ushort Health { get; set; } = 100;

        /// <summary>The animal type identifier from Unturned's animal table.</summary>
        [JsonProperty("animalId")]
        public ushort AnimalId { get; set; }

        /// <summary>Sets the spawn position.</summary>
        public AnimalOptions AtPosition(float x, float y, float z)
        {
            Position = new float[] { x, y, z };
            return this;
        }

        /// <summary>Sets the animal's starting health.</summary>
        public AnimalOptions WithHealth(ushort health)
        {
            Health = health;
            return this;
        }

        /// <summary>Sets the animal type identifier.</summary>
        public AnimalOptions WithAnimalId(ushort animalId)
        {
            AnimalId = animalId;
            return this;
        }
    }
}
