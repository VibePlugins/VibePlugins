using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Fluent builder for configuring an Unturned server container with RocketMod,
    /// including Unturned CLI flags and optional sidecar containers (MySQL, Redis, etc.).
    /// </summary>
    public class UnturnedContainerBuilder
    {
        /// <summary>Default Docker image tag for the Unturned + RocketMod server.</summary>
        public const string DefaultImage = "vibeplugins:rocketmod";

        /// <summary>Default TCP port the test bridge harness listens on inside the container.</summary>
        public const int DefaultBridgePort = 27099;

        /// <summary>Default server name used for the Unturned server directory (matches the map name).</summary>
        public const string DefaultServerName = "PEI";

        private string _image = DefaultImage;
        private int _bridgePort = DefaultBridgePort;
        private string _serverName = DefaultServerName;
        private string _mapName = "PEI";
        private bool _skipAssets;
        private bool _offlineOnly;
        private bool _noWebRequests;
        private string? _gameplayConfigFile;
        private int? _maxPlayersLimit;
        private bool _logGameplayConfig;
        private bool _constNetEvents;
        private readonly List<string> _customFlags = new List<string>();
        private readonly List<Func<Task>> _cleanupCallbacks = new List<Func<Task>>();
        private readonly Dictionary<string, IContainer> _additionalContainers = new Dictionary<string, IContainer>();

        private MySqlSidecarOptions? _mySqlOptions;
        private bool _useRedis;

        private string? _rocketDirectory;

        /// <summary>
        /// Gets the server name used for the Unturned server directory.
        /// </summary>
        public string ServerName => _serverName;

        /// <summary>
        /// Gets the configured bridge port.
        /// </summary>
        public int BridgePort => _bridgePort;

        /// <summary>
        /// Gets the host-side temp directory that is bind-mounted to the Rocket directory inside the container.
        /// Created lazily during <see cref="Build"/>.
        /// </summary>
        public string RocketDirectory => _rocketDirectory;

        /// <summary>
        /// Gets the MySQL sidecar options, or <c>null</c> if MySQL is not configured.
        /// </summary>
        public MySqlSidecarOptions MySqlOptions => _mySqlOptions;

        /// <summary>
        /// Gets whether a Redis sidecar is configured.
        /// </summary>
        public bool UseRedis => _useRedis;

        /// <summary>
        /// Gets the additional sidecar containers.
        /// </summary>
        public IReadOnlyDictionary<string, IContainer> AdditionalContainers => _additionalContainers;

        /// <summary>
        /// Gets the registered cleanup callbacks.
        /// </summary>
        internal IReadOnlyList<Func<Task>> CleanupCallbacks => _cleanupCallbacks;

        /// <summary>Sets the Docker image to use for the Unturned server.</summary>
        /// <param name="image">The Docker image tag.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithImage(string image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            return this;
        }

        /// <summary>Sets the TCP bridge port exposed by the container.</summary>
        /// <param name="port">The port number.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithBridgePort(int port)
        {
            _bridgePort = port;
            return this;
        }

        /// <summary>Sets the Unturned server name (directory name under /opt/unturned/Servers/).</summary>
        /// <param name="serverName">The server name.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithServerName(string serverName)
        {
            _serverName = serverName ?? throw new ArgumentNullException(nameof(serverName));
            return this;
        }

        // ───────────────────────────────────────────────────────────────
        // Unturned CLI flag extension methods
        // ───────────────────────────────────────────────────────────────

        /// <summary>Enables or disables the <c>-SkipAssets</c> flag to skip loading asset bundles.</summary>
        /// <param name="enabled">Whether to enable the flag.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithSkipAssets(bool enabled = true)
        {
            _skipAssets = enabled;
            return this;
        }

        /// <summary>Enables the <c>-OfflineOnly</c> flag to run without Steam networking.</summary>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithOfflineOnly()
        {
            _offlineOnly = true;
            return this;
        }

        /// <summary>Enables the <c>-NoWebRequests</c> flag to prevent outbound HTTP requests.</summary>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithNoWebRequests()
        {
            _noWebRequests = true;
            return this;
        }

        /// <summary>Sets the <c>-GameplayConfigFile</c> flag with the specified config file path.</summary>
        /// <param name="path">Path to the gameplay config file inside the container.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithGameplayConfigFile(string path)
        {
            _gameplayConfigFile = path ?? throw new ArgumentNullException(nameof(path));
            return this;
        }

        /// <summary>Sets the <c>-MaxPlayersLimit</c> flag to cap the maximum number of players.</summary>
        /// <param name="limit">The maximum player count.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithMaxPlayersLimit(int limit)
        {
            _maxPlayersLimit = limit;
            return this;
        }

        /// <summary>Enables the <c>-LogGameplayConfig</c> flag to log gameplay configuration on startup.</summary>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithLogGameplayConfig()
        {
            _logGameplayConfig = true;
            return this;
        }

        /// <summary>Enables the <c>-ConstNetEvents</c> flag for deterministic networking events.</summary>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithConstNetEvents()
        {
            _constNetEvents = true;
            return this;
        }

        /// <summary>Sets the map name for the <c>+InternetServer/&lt;mapName&gt;</c> launch argument.</summary>
        /// <param name="mapName">The Unturned map name.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithMap(string mapName)
        {
            _mapName = mapName ?? throw new ArgumentNullException(nameof(mapName));
            _serverName = _mapName; // Unturned uses the map name as the server instance directory
            return this;
        }

        /// <summary>Adds an arbitrary custom CLI flag to the Unturned server launch command.</summary>
        /// <param name="flag">The custom flag (e.g. <c>"-MyFlag"</c>).</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithCustomFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                throw new ArgumentException("Flag must not be null or empty.", nameof(flag));

            _customFlags.Add(flag);
            return this;
        }

        // ───────────────────────────────────────────────────────────────
        // Sidecar containers
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a MySQL sidecar container with optional configuration.
        /// </summary>
        /// <param name="configure">Optional action to configure MySQL options.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithMySql(Action<MySqlSidecarOptions> configure = null)
        {
            _mySqlOptions = new MySqlSidecarOptions();
            configure?.Invoke(_mySqlOptions);
            return this;
        }

        /// <summary>
        /// Adds a Redis sidecar container.
        /// </summary>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithRedis()
        {
            _useRedis = true;
            return this;
        }

        /// <summary>
        /// Adds an arbitrary sidecar container that will be started alongside the Unturned server.
        /// </summary>
        /// <param name="name">A unique name for the sidecar.</param>
        /// <param name="container">The pre-built container instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithAdditionalContainer(string name, IContainer container)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be null or empty.", nameof(name));

            _additionalContainers[name] = container ?? throw new ArgumentNullException(nameof(container));
            return this;
        }

        /// <summary>
        /// Registers a cleanup callback that will be invoked when the container is disposed.
        /// </summary>
        /// <param name="callback">The async cleanup callback.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UnturnedContainerBuilder WithCleanupCallback(Func<Task> callback)
        {
            _cleanupCallbacks.Add(callback ?? throw new ArgumentNullException(nameof(callback)));
            return this;
        }

        // ───────────────────────────────────────────────────────────────
        // Build
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the Unturned server container configuration and returns an <see cref="UnturnedServerContainer"/>.
        /// Creates a unique temporary directory for the Rocket bind mount.
        /// </summary>
        /// <returns>A fully configured <see cref="UnturnedServerContainer"/> ready to be started.</returns>
        public UnturnedServerContainer Build()
        {
            _rocketDirectory = Path.Combine(
                Path.GetTempPath(),
                "vibeplugins-test-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(_rocketDirectory);

            // Build the CMD array for Unturned launch arguments
            List<string> cmdArgs = BuildCommandArgs();

            string containerRocketPath = $"/opt/unturned/Servers/{_serverName}/Rocket/";

            // Note: we do NOT use a WaitStrategy for port 27099 here because the
            // TestHarness plugin (which opens the TCP listener) hasn't been deployed yet.
            // The port wait is handled by TestSession.WaitForHarnessReadyAsync() after
            // the harness DLL is copied into Rocket/Plugins/ and the server is restarted.
            IContainer serverContainer = new ContainerBuilder()
                .WithImage(_image)
                .WithPortBinding(_bridgePort, true)
                .WithBindMount(_rocketDirectory, containerRocketPath)
                .WithCommand(cmdArgs.ToArray())
                .Build();

            // Build MySQL sidecar if configured
            IContainer mySqlContainer = null;
            if (_mySqlOptions != null)
            {
                mySqlContainer = new ContainerBuilder()
                    .WithImage("mysql:8.0")
                    .WithPortBinding(_mySqlOptions.Port, true)
                    .WithEnvironment("MYSQL_ROOT_PASSWORD", _mySqlOptions.Password)
                    .WithEnvironment("MYSQL_DATABASE", _mySqlOptions.Database)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(_mySqlOptions.Port))
                    .Build();
            }

            // Build Redis sidecar if configured
            IContainer redisContainer = null;
            if (_useRedis)
            {
                redisContainer = new ContainerBuilder()
                    .WithImage("redis:7-alpine")
                    .WithPortBinding(6379, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
                    .Build();
            }

            return new UnturnedServerContainer(
                serverContainer: serverContainer,
                rocketDirectory: _rocketDirectory,
                bridgePort: _bridgePort,
                mySqlContainer: mySqlContainer,
                mySqlOptions: _mySqlOptions,
                redisContainer: redisContainer,
                additionalContainers: new Dictionary<string, IContainer>(_additionalContainers),
                cleanupCallbacks: new List<Func<Task>>(_cleanupCallbacks));
        }

        /// <summary>
        /// Builds the CMD array for the Unturned server process.
        /// </summary>
        private List<string> BuildCommandArgs()
        {
            var args = new List<string>();

            // The main server launch command
            args.Add($"+InternetServer/{_mapName}");

            if (_skipAssets)
                args.Add("-SkipAssets");

            if (_offlineOnly)
                args.Add("-OfflineOnly");

            if (_noWebRequests)
                args.Add("-NoWebRequests");

            if (!string.IsNullOrEmpty(_gameplayConfigFile))
            {
                args.Add("-GameplayConfigFile");
                args.Add(_gameplayConfigFile);
            }

            if (_maxPlayersLimit.HasValue)
            {
                args.Add("-MaxPlayersLimit");
                args.Add(_maxPlayersLimit.Value.ToString());
            }

            if (_logGameplayConfig)
                args.Add("-LogGameplayConfig");

            if (_constNetEvents)
                args.Add("-ConstNetEvents");

            foreach (string flag in _customFlags)
            {
                args.Add(flag);
            }

            return args;
        }
    }
}
