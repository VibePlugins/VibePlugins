using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using Rocket.Core.Logging;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Action = System.Action;
using Logger = Rocket.Core.Logging.Logger;

namespace VibePlugins.RocketMod.TestHarness.Mocks
{
    /// <summary>
    /// Creates and tracks mock entities (players, zombies, animals) on the server.
    /// Uses <see cref="FormatterServices.GetUninitializedObject"/> and
    /// <see cref="Traverse"/> to construct Unturned objects without invoking
    /// their networking or Unity lifecycle code.
    /// </summary>
    /// <remarks>
    /// Must be called on the Unity main thread. Created objects are real
    /// <see cref="GameObject"/> instances suitable for RocketMod API interactions
    /// but will not participate in Unturned networking.
    /// </remarks>
    public static class MockFactory
    {
        private static readonly ConcurrentDictionary<Guid, MockHandle> ActiveMocks =
            new ConcurrentDictionary<Guid, MockHandle>();

        /// <summary>
        /// Thread-safe counter for generating unique fake Steam IDs when none is specified.
        /// Starting at 76561198000000001 to stay in a plausible range.
        /// </summary>
        private static long _nextSteamId = 76561198000000001L;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Creates a mock entity based on the request and returns a handle.
        /// </summary>
        /// <param name="request">The mock creation request.</param>
        /// <returns>A <see cref="CreateMockResponse"/> with the handle ID on success.</returns>
        /// <remarks>Must be called on the Unity main thread.</remarks>
        public static CreateMockResponse Create(CreateMockRequest request)
        {
            var handleId = Guid.NewGuid();

            try
            {
                Action destroyAction;

                switch (request.EntityType)
                {
                    case MockEntityType.Player:
                        var playerOpts = JsonConvert.DeserializeObject<PlayerOptions>(request.OptionsJson);
                        destroyAction = CreateMockPlayer(handleId, playerOpts);
                        break;

                    case MockEntityType.Zombie:
                        var zombieOpts = JsonConvert.DeserializeObject<ZombieOptions>(request.OptionsJson);
                        destroyAction = CreateMockZombie(handleId, zombieOpts);
                        break;

                    case MockEntityType.Animal:
                        var animalOpts = JsonConvert.DeserializeObject<AnimalOptions>(request.OptionsJson);
                        destroyAction = CreateMockAnimal(handleId, animalOpts);
                        break;

                    default:
                        return new CreateMockResponse
                        {
                            HandleId = handleId,
                            Success = false,
                            Error = $"Unsupported entity type: {request.EntityType}"
                        };
                }

                return new CreateMockResponse
                {
                    HandleId = handleId,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new CreateMockResponse
                {
                    HandleId = handleId,
                    Success = false,
                    Error = ex.ToString()
                };
            }
        }

        /// <summary>
        /// Retrieves a tracked mock handle by its ID.
        /// </summary>
        /// <param name="handleId">The handle identifier.</param>
        /// <param name="handle">The mock handle, if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public static bool TryGetHandle(Guid handleId, out MockHandle handle)
        {
            return ActiveMocks.TryGetValue(handleId, out handle);
        }

        /// <summary>
        /// Destroys all active mock entities and clears the tracking dictionary.
        /// </summary>
        /// <remarks>Must be called on the Unity main thread.</remarks>
        public static void DestroyAll()
        {
            foreach (var kvp in ActiveMocks)
            {
                try
                {
                    kvp.Value.Destroy();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex,
                        $"[TestHarness] Error destroying mock {kvp.Key}");
                }
            }

            ActiveMocks.Clear();
        }

        // ── Player creation ───────────────────────────────────────────

        private static Action CreateMockPlayer(Guid handleId, PlayerOptions options)
        {
            // Resolve or generate a Steam ID.
            ulong steamIdValue = options.SteamId ?? (ulong)System.Threading.Interlocked.Increment(ref _nextSteamId);
            var cSteamId = new CSteamID(steamIdValue);
            string playerName = options.Name ?? "TestPlayer";

            // Create a GameObject to host the mock player.
            var go = new GameObject($"TestPlayer_{playerName}");

            // Create a SteamPlayerID with the fake identity.
            var playerID = new SteamPlayerID(
                cSteamId,
                0, // characterID
                playerName,
                playerName, // characterName
                playerName, // nickName
                CSteamID.Nil);

            // Create an uninitialized SteamPlayer to avoid constructor side-effects
            // (networking, provider registration, etc.).
            SteamPlayer steamPlayer;
            try
            {
                steamPlayer = (SteamPlayer)FormatterServices.GetUninitializedObject(typeof(SteamPlayer));
            }
            catch (Exception ex)
            {
                UnityEngine.Object.Destroy(go);
                throw new InvalidOperationException(
                    $"Failed to create uninitialized SteamPlayer: {ex.Message}", ex);
            }

            // Set critical fields via Harmony Traverse.
            var traverse = Traverse.Create(steamPlayer);
            traverse.Field("_playerID").SetValue(playerID);
            traverse.Field("_isAdmin").SetValue(options.IsAdmin);
            traverse.Field("_model").SetValue(go.transform);

            // Set position on the GameObject.
            if (options.Position != null && options.Position.Length >= 3)
            {
                go.transform.position = new Vector3(
                    options.Position[0],
                    options.Position[1],
                    options.Position[2]);
            }

            // Build the handle.
            var handle = new MockPlayerHandle(
                handleId,
                playerName,
                steamIdValue,
                options.IsAdmin,
                steamPlayer,
                go);

            ActiveMocks[handleId] = new MockHandle(handleId, MockEntityType.Player, () => handle.Destroy());

            Logger.Log(
                $"[TestHarness] Mock player created: {playerName} (SteamId={steamIdValue}, Handle={handleId})");

            return () => handle.Destroy();
        }

        // ── Zombie creation ───────────────────────────────────────────

        private static Action CreateMockZombie(Guid handleId, ZombieOptions options)
        {
            var position = options.Position != null && options.Position.Length >= 3
                ? new Vector3(options.Position[0], options.Position[1], options.Position[2])
                : Vector3.zero;

            var go = new GameObject($"TestZombie_{handleId:N}");
            go.transform.position = position;

            // Try to create an uninitialized Zombie and set fields via Traverse.
            // Zombie is a MonoBehaviour so we cannot use FormatterServices; instead
            // we add it as a component and configure it afterward.
            Zombie zombie = null;
            try
            {
                zombie = go.AddComponent<Zombie>();
            }
            catch
            {
                // If AddComponent fails (e.g., missing dependencies), proceed
                // with just the GameObject so the handle is still valid for tracking.
            }

            if (zombie != null)
            {
                var traverse = Traverse.Create(zombie);
                traverse.Field("health").SetValue(options.Health);
                traverse.Field("speciality").SetValue((EZombieSpeciality)options.Speciality);
            }

            var handle = new MockZombieHandle(handleId, position, options.Health, options.Speciality, go);
            ActiveMocks[handleId] = new MockHandle(handleId, MockEntityType.Zombie, () => handle.Destroy());

            Logger.Log(
                $"[TestHarness] Mock zombie created at ({position.x}, {position.y}, {position.z}) Health={options.Health} (Handle={handleId})");

            return () => handle.Destroy();
        }

        // ── Animal creation ───────────────────────────────────────────

        private static Action CreateMockAnimal(Guid handleId, AnimalOptions options)
        {
            var position = options.Position != null && options.Position.Length >= 3
                ? new Vector3(options.Position[0], options.Position[1], options.Position[2])
                : Vector3.zero;

            var go = new GameObject($"TestAnimal_{handleId:N}");
            go.transform.position = position;

            // Animal is a MonoBehaviour; add as component and configure via Traverse.
            Animal animal = null;
            try
            {
                animal = go.AddComponent<Animal>();
            }
            catch
            {
                // If AddComponent fails, proceed with just the GameObject.
            }

            if (animal != null)
            {
                var traverse = Traverse.Create(animal);
                traverse.Field("health").SetValue(options.Health);
                traverse.Field("id").SetValue(options.AnimalId);
            }

            var handle = new MockAnimalHandle(handleId, position, options.Health, options.AnimalId, go);
            ActiveMocks[handleId] = new MockHandle(handleId, MockEntityType.Animal, () => handle.Destroy());

            Logger.Log(
                $"[TestHarness] Mock animal created: ID={options.AnimalId} at ({position.x}, {position.y}, {position.z}) (Handle={handleId})");

            return () => handle.Destroy();
        }

        // ── Inner handle wrapper ──────────────────────────────────────

        /// <summary>
        /// Tracks a single mock entity with its destroy callback.
        /// </summary>
        public sealed class MockHandle
        {
            /// <summary>Unique identifier for this mock.</summary>
            public Guid Id { get; }

            /// <summary>The type of entity this mock represents.</summary>
            public MockEntityType EntityType { get; }

            private readonly Action _destroyAction;

            /// <summary>
            /// Initializes a new <see cref="MockHandle"/>.
            /// </summary>
            public MockHandle(Guid id, MockEntityType entityType, Action destroyAction)
            {
                Id = id;
                EntityType = entityType;
                _destroyAction = destroyAction;
            }

            /// <summary>
            /// Invokes the destroy callback for this mock entity.
            /// </summary>
            public void Destroy()
            {
                _destroyAction?.Invoke();
            }
        }
    }
}
