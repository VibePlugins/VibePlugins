using System;
using UnityEngine;

namespace VibePlugins.RocketMod.TestHarness.Mocks
{
    /// <summary>
    /// Wraps a mock animal entity created by <see cref="MockFactory"/>.
    /// Provides access to the animal's handle ID, position, health, and type for test assertions.
    /// </summary>
    public sealed class MockAnimalHandle
    {
        /// <summary>Unique handle identifier for tracking and cleanup.</summary>
        public Guid HandleId { get; }

        /// <summary>World position of the mock animal.</summary>
        public Vector3 Position { get; }

        /// <summary>Health value assigned to the mock animal.</summary>
        public ushort Health { get; }

        /// <summary>The animal type identifier from Unturned's animal table.</summary>
        public ushort AnimalId { get; }

        /// <summary>The <see cref="GameObject"/> hosting this mock animal.</summary>
        public GameObject GameObject { get; }

        /// <summary>Whether this handle has been destroyed.</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="MockAnimalHandle"/>.
        /// </summary>
        internal MockAnimalHandle(
            Guid handleId,
            Vector3 position,
            ushort health,
            ushort animalId,
            GameObject gameObject)
        {
            HandleId = handleId;
            Position = position;
            Health = health;
            AnimalId = animalId;
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
