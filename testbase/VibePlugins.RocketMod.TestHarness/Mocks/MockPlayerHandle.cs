using System;
using Rocket.API;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace VibePlugins.RocketMod.TestHarness.Mocks
{
    /// <summary>
    /// Wraps a mock player entity created by <see cref="MockFactory"/>.
    /// Implements <see cref="IRocketPlayer"/> so it can be used for command execution
    /// and other RocketMod API interactions.
    /// </summary>
    public sealed class MockPlayerHandle : IRocketPlayer
    {
        /// <summary>Unique handle identifier for tracking and cleanup.</summary>
        public Guid HandleId { get; }

        /// <summary>Display name of the mock player.</summary>
        public string Name { get; }

        /// <summary>Steam64 ID assigned to this mock player.</summary>
        public ulong SteamId { get; }

        /// <summary>Whether this mock player has admin privileges.</summary>
        public bool IsAdmin { get; }

        /// <summary>The underlying <see cref="SteamPlayer"/> instance (uninitialized object with fields set via reflection).</summary>
        public SteamPlayer SteamPlayer { get; }

        /// <summary>The <see cref="GameObject"/> hosting this mock player.</summary>
        public GameObject GameObject { get; }

        /// <summary>Whether this handle has been destroyed.</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="MockPlayerHandle"/>.
        /// </summary>
        internal MockPlayerHandle(
            Guid handleId,
            string name,
            ulong steamId,
            bool isAdmin,
            SteamPlayer steamPlayer,
            GameObject gameObject)
        {
            HandleId = handleId;
            Name = name;
            SteamId = steamId;
            IsAdmin = isAdmin;
            SteamPlayer = steamPlayer;
            GameObject = gameObject;
        }

        // ── IRocketPlayer implementation ─────────────────────────────

        /// <inheritdoc />
        string IRocketPlayer.Id => SteamId.ToString();

        /// <inheritdoc />
        string IRocketPlayer.DisplayName => Name;

        /// <inheritdoc />
        int IComparable.CompareTo(object obj)
        {
            if (obj is IRocketPlayer other)
                return string.Compare(SteamId.ToString(), other.Id, StringComparison.Ordinal);
            return 1;
        }

        /// <summary>
        /// Destroys the associated <see cref="GameObject"/> and marks this handle as destroyed.
        /// </summary>
        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;

            if (GameObject != null)
            {
                UnityEngine.Object.Destroy(GameObject);
            }
        }
    }
}
