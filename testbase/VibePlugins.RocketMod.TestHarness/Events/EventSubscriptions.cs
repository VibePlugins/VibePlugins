using System;
using Newtonsoft.Json;
using Rocket.Core.Logging;
using SDG.Unturned;
using Steamworks;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using VibePlugins.RocketMod.TestHarness.Execution;

namespace VibePlugins.RocketMod.TestHarness.Events
{
    /// <summary>
    /// Subscribes to common Unturned and RocketMod events and records them
    /// into <see cref="EventCapture"/> so that test hosts can monitor and
    /// wait for specific game events.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Initialize"/> from <c>TestHarnessPlugin.Load()</c> and
    /// <see cref="Teardown"/> from <c>TestHarnessPlugin.Unload()</c>.
    /// Chat messages are already captured by the <c>ChatManagerPatch</c> Harmony
    /// patch, so they are not duplicated here.
    /// </remarks>
    public static class EventSubscriptions
    {
        private static bool _initialized;

        /// <summary>
        /// Subscribes to Unturned server events. Safe to call multiple times;
        /// subsequent calls are no-ops.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Player connect / disconnect
                Provider.onEnemyConnected += OnEnemyConnected;
                Provider.onEnemyDisconnected += OnEnemyDisconnected;

                // Player death
                PlayerLife.onPlayerDied += OnPlayerDied;

                // Damage requested
                DamageTool.damagePlayerRequested += OnDamagePlayerRequested;

                // Vehicle enter requested
                VehicleManager.onEnterVehicleRequested += OnEnterVehicleRequested;

                Logger.Log("[TestHarness] Event subscriptions initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Failed to initialize event subscriptions");
            }
        }

        /// <summary>
        /// Unsubscribes from all previously subscribed events.
        /// </summary>
        public static void Teardown()
        {
            if (!_initialized) return;
            _initialized = false;

            try
            {
                Provider.onEnemyConnected -= OnEnemyConnected;
                Provider.onEnemyDisconnected -= OnEnemyDisconnected;
                PlayerLife.onPlayerDied -= OnPlayerDied;
                DamageTool.damagePlayerRequested -= OnDamagePlayerRequested;
                VehicleManager.onEnterVehicleRequested -= OnEnterVehicleRequested;

                Logger.Log("[TestHarness] Event subscriptions torn down.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Failed to tear down event subscriptions");
            }
        }

        // ── Event handlers ────────────────────────────────────────────

        private static void OnEnemyConnected(SteamPlayer steamPlayer)
        {
            try
            {
                var evt = new PlayerConnectedEvent
                {
                    PlayerName = steamPlayer?.playerID?.playerName ?? "Unknown",
                    SteamId = steamPlayer?.playerID?.steamID.m_SteamID ?? 0
                };

                EventCapture.Record(
                    typeof(PlayerConnectedEvent).FullName,
                    JsonConvert.SerializeObject(evt));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Error recording PlayerConnectedEvent");
            }
        }

        private static void OnEnemyDisconnected(SteamPlayer steamPlayer)
        {
            try
            {
                var evt = new PlayerDisconnectedEvent
                {
                    PlayerName = steamPlayer?.playerID?.playerName ?? "Unknown",
                    SteamId = steamPlayer?.playerID?.steamID.m_SteamID ?? 0
                };

                EventCapture.Record(
                    typeof(PlayerDisconnectedEvent).FullName,
                    JsonConvert.SerializeObject(evt));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Error recording PlayerDisconnectedEvent");
            }
        }

        private static void OnPlayerDied(PlayerLife sender, EDeathCause cause, ELimb limb, CSteamID instigator)
        {
            try
            {
                string playerName = sender?.player?.channel?.owner?.playerID?.playerName ?? "Unknown";

                // Try to resolve the killer name from the instigator Steam ID.
                string killerName = null;
                if (instigator != CSteamID.Nil && instigator.m_SteamID != 0)
                {
                    var killerPlayer = PlayerTool.getPlayer(instigator);
                    killerName = killerPlayer?.channel?.owner?.playerID?.playerName;
                }

                var evt = new PlayerDeathEvent
                {
                    PlayerName = playerName,
                    KillerName = killerName,
                    Cause = cause.ToString(),
                    Limb = limb.ToString()
                };

                EventCapture.Record(
                    typeof(PlayerDeathEvent).FullName,
                    JsonConvert.SerializeObject(evt));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Error recording PlayerDeathEvent");
            }
        }

        private static void OnDamagePlayerRequested(
            ref DamagePlayerParameters parameters,
            ref bool shouldAllow)
        {
            try
            {
                string playerName = parameters.player?.channel?.owner?.playerID?.playerName ?? "Unknown";

                var evt = new PlayerDamageEvent
                {
                    PlayerName = playerName,
                    Damage = parameters.damage,
                    Cause = parameters.cause.ToString()
                };

                EventCapture.Record(
                    typeof(PlayerDamageEvent).FullName,
                    JsonConvert.SerializeObject(evt));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Error recording PlayerDamageEvent");
            }
        }

        private static void OnEnterVehicleRequested(
            Player player,
            InteractableVehicle vehicle,
            ref bool shouldAllow)
        {
            try
            {
                string playerName = player?.channel?.owner?.playerID?.playerName ?? "Unknown";

                var evt = new VehicleEnterEvent
                {
                    PlayerName = playerName,
                    VehicleId = vehicle?.instanceID ?? 0
                };

                EventCapture.Record(
                    typeof(VehicleEnterEvent).FullName,
                    JsonConvert.SerializeObject(evt));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Error recording VehicleEnterEvent");
            }
        }
    }
}
