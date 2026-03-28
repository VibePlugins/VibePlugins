using System;
using System.Threading.Tasks;
using HarmonyLib;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using SDG.Unturned;
using VibePlugins.RocketMod.TestHarness.Events;
using VibePlugins.RocketMod.TestHarness.Execution;
using VibePlugins.RocketMod.TestHarness.Patches;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness
{
    /// <summary>
    /// RocketMod plugin that hosts the test harness TCP bridge inside the Unturned server.
    /// Applies Harmony patches, starts the TCP server on port 27099, and relays test
    /// commands between the external test host and the game.
    /// </summary>
    public class TestHarnessPlugin : RocketPlugin<TestHarnessConfig>
    {
        /// <summary>Singleton instance of the plugin, used by patches and static helpers.</summary>
        public static TestHarnessPlugin Instance { get; private set; }

        /// <summary>The TCP server that communicates with the test host process.</summary>
        public HarnessTcpServer TcpServer { get; private set; }

        /// <summary>The command dispatcher that routes incoming messages to handlers.</summary>
        public TestCommandDispatcher Dispatcher { get; private set; }

        private Harmony _harmony;
        private bool _levelReady;

        /// <summary>
        /// Called by RocketMod when the plugin is loaded. Applies Harmony patches,
        /// starts the TCP server, and subscribes to level-loaded events.
        /// </summary>
        protected override void Load()
        {
            Instance = this;

            // Apply all Harmony patches in this assembly.
            _harmony = new Harmony("com.vibeplugins.testharness");
            _harmony.PatchAll(typeof(TestHarnessPlugin).Assembly);

            // Wire up the plugin tracker so it can send messages over TCP.
            PluginTracker.Initialize(() => TcpServer);

            // Create and start the TCP server.
            TcpServer = new HarnessTcpServer(Configuration.Instance.Port);
            Dispatcher = new TestCommandDispatcher(TcpServer);
            TcpServer.MessageReceived += OnMessageReceived;
            TcpServer.ClientConnected += OnClientConnected;
            TcpServer.Start();

            // Subscribe to common Unturned events for test monitoring.
            EventSubscriptions.Initialize();

            // Subscribe to the level-loaded event so we can signal readiness.
            Level.onPostLevelLoaded += OnPostLevelLoaded;

            // If the level is already loaded (plugin loaded after level init), set ready immediately.
            if (Level.isLoaded)
            {
                _levelReady = true;
                Logger.Log("[TestHarness] Level already loaded at plugin init; marking ready.");
            }

            Logger.Log(
                $"[TestHarness] Activated on port {Configuration.Instance.Port}.");
        }

        /// <summary>
        /// Called by RocketMod when the plugin is unloaded. Stops the TCP server
        /// and removes all Harmony patches.
        /// </summary>
        protected override void Unload()
        {
            Level.onPostLevelLoaded -= OnPostLevelLoaded;

            EventSubscriptions.Teardown();

            if (TcpServer != null)
            {
                TcpServer.MessageReceived -= OnMessageReceived;
                TcpServer.ClientConnected -= OnClientConnected;
                TcpServer.Stop();
                TcpServer = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchAll("com.vibeplugins.testharness");
                _harmony = null;
            }

            Instance = null;

            Logger.Log("[TestHarness] Deactivated.");
        }

        /// <summary>
        /// Unity Update callback -- drains the main-thread dispatch queue and
        /// advances tick counters every frame.
        /// </summary>
        public void Update()
        {
            MainThreadDispatcher.ProcessQueue();
            TickAwaiter.OnUpdate();
        }

        /// <summary>
        /// Unity FixedUpdate callback -- used by TickAwaiter for physics-tick counting.
        /// </summary>
        public void FixedUpdate()
        {
            TickAwaiter.OnFixedUpdate();
        }

        /// <summary>
        /// Unity LateUpdate callback -- used by TickAwaiter for late-tick counting.
        /// </summary>
        public void LateUpdate()
        {
            TickAwaiter.OnLateUpdate();
        }

        private void OnPostLevelLoaded(int level)
        {
            _levelReady = true;

            var readyMsg = new HarnessReadyMessage
            {
                ServerVersion = Provider.APP_VERSION,
                MapName = Level.info?.name ?? "Unknown",
                MaxPlayers = Provider.maxPlayers,
                HarnessPort = Configuration.Instance.Port
            };

            TcpServer?.SendMessageAsync(readyMsg);
        }

        /// <summary>
        /// Called when a test host client connects. Always sends the HarnessReadyMessage
        /// since the fact that the harness plugin is loaded means the server is ready.
        /// </summary>
        private void OnClientConnected()
        {
            Logger.Log("[TestHarness] Client connected; sending ready message.");
            var readyMsg = new HarnessReadyMessage
            {
                ServerVersion = Provider.APP_VERSION,
                MapName = Level.info?.name ?? "Unknown",
                MaxPlayers = Provider.maxPlayers,
                HarnessPort = Configuration.Instance.Port
            };
            TcpServer?.SendMessageAsync(readyMsg);
        }

        private async void OnMessageReceived(TestMessage message)
        {
            try
            {
                await Dispatcher.DispatchAsync(message);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[TestHarness] Dispatch error");
            }
        }
    }

    /// <summary>
    /// Configuration for the test harness plugin.
    /// </summary>
    public class TestHarnessConfig : IRocketPluginConfiguration
    {
        /// <summary>TCP port the harness listens on for test host connections.</summary>
        public int Port { get; set; } = 27099;

        /// <inheritdoc />
        public void LoadDefaults()
        {
            Port = 27099;
        }
    }
}
