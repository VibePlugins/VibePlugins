using System;
using UnityEngine;

namespace VibePlugins.RocketMod.TestHarness.Mocks
{
    /// <summary>
    /// Wraps a mock zombie entity created by <see cref="MockFactory"/>.
    /// Provides access to the zombie's handle ID, position, and health for test assertions.
    /// </summary>
    public sealed class MockZombieHandle
    {
        /// <summary>Unique handle identifier for tracking and cleanup.</summary>
        public Guid HandleId { get; }

        /// <summary>World position of the mock zombie.</summary>
        public Vector3 Position { get; }

        /// <summary>Health value assigned to the mock zombie.</summary>
        public ushort Health { get; }

        /// <summary>Zombie speciality/type identifier.</summary>
        public byte Speciality { get; }

        /// <summary>The <see cref="GameObject"/> hosting this mock zombie.</summary>
        public GameObject GameObject { get; }

        /// <summary>Whether this handle has been destroyed.</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="MockZombieHandle"/>.
        /// </summary>
        internal MockZombieHandle(
            Guid handleId,
            Vector3 position,
            ushort health,
            byte speciality,
            GameObject gameObject)
        {
            HandleId = handleId;
            Position = position;
            Health = health;
            Speciality = speciality;
            GameObject = gameObject;
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
