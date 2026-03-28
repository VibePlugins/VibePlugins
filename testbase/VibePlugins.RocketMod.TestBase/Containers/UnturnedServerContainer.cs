using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;

namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Wraps a Testcontainers <see cref="IContainer"/> for the Unturned server,
    /// managing the server container and optional sidecar containers (MySQL, Redis, custom).
    /// </summary>
    public class UnturnedServerContainer : IAsyncDisposable
    {
        private readonly IContainer _serverContainer;
        private readonly IContainer _mySqlContainer;
        private readonly MySqlSidecarOptions _mySqlOptions;
        private readonly IContainer _redisContainer;
        private readonly Dictionary<string, IContainer> _additionalContainers;
        private readonly List<Func<Task>> _cleanupCallbacks;
        private readonly string _rocketDirectory;
        private readonly int _bridgePort;
        private bool _disposed;
        private Task? _logForwardingTask;

        /// <summary>
        /// Initializes a new instance of <see cref="UnturnedServerContainer"/>.
        /// This constructor is intended to be called by <see cref="UnturnedContainerBuilder.Build"/>.
        /// </summary>
        /// <param name="serverContainer">The main Unturned server container.</param>
        /// <param name="rocketDirectory">Host-side temp directory bind-mounted to the Rocket directory.</param>
        /// <param name="bridgePort">The container-side TCP bridge port.</param>
        /// <param name="mySqlContainer">Optional MySQL sidecar container.</param>
        /// <param name="mySqlOptions">Optional MySQL configuration options.</param>
        /// <param name="redisContainer">Optional Redis sidecar container.</param>
        /// <param name="additionalContainers">Additional named sidecar containers.</param>
        /// <param name="cleanupCallbacks">Callbacks to invoke during disposal.</param>
        internal UnturnedServerContainer(
            IContainer serverContainer,
            string rocketDirectory,
            int bridgePort,
            IContainer mySqlContainer,
            MySqlSidecarOptions mySqlOptions,
            IContainer redisContainer,
            Dictionary<string, IContainer> additionalContainers,
            List<Func<Task>> cleanupCallbacks)
        {
            _serverContainer = serverContainer ?? throw new ArgumentNullException(nameof(serverContainer));
            _rocketDirectory = rocketDirectory ?? throw new ArgumentNullException(nameof(rocketDirectory));
            _bridgePort = bridgePort;
            _mySqlContainer = mySqlContainer;
            _mySqlOptions = mySqlOptions;
            _redisContainer = redisContainer;
            _additionalContainers = additionalContainers ?? new Dictionary<string, IContainer>();
            _cleanupCallbacks = cleanupCallbacks ?? new List<Func<Task>>();
        }

        /// <summary>
        /// Gets the host-side temp directory path that is bind-mounted to the Rocket directory inside the container.
        /// </summary>
        public string RocketDirectory => _rocketDirectory;

        /// <summary>Gets the Docker container ID for direct docker CLI operations.</summary>
        public string ServerContainerId => _serverContainer.Id;

        /// <summary>
        /// Gets the mapped host port for the TCP bridge (container port 27099).
        /// After a docker restart, the cached Testcontainers port may be stale,
        /// so this queries docker directly via CLI when a cached value fails.
        /// </summary>
        public int BridgePort
        {
            get
            {
                // After docker restart, port mappings can change.
                // Query docker port directly for the current mapping.
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("docker", $"port {_serverContainer.Id} {_bridgePort}")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        // Output format: 0.0.0.0:53803 or [::]:53803
                        if (!string.IsNullOrEmpty(output))
                        {
                            var lastColon = output.LastIndexOf(':');
                            if (lastColon >= 0 && int.TryParse(output.Substring(lastColon + 1), out int port))
                                return port;
                        }
                    }
                }
                catch { /* fall back to Testcontainers */ }

                return _serverContainer.GetMappedPublicPort(_bridgePort);
            }
        }

        /// <summary>
        /// Gets the hostname for connecting to the server container from the host.
        /// </summary>
        public string Hostname => _serverContainer.Hostname;

        /// <summary>
        /// Gets the underlying Testcontainers <see cref="IContainer"/> for the Unturned server.
        /// </summary>
        public IContainer ServerContainer => _serverContainer;

        /// <summary>
        /// Gets whether a MySQL sidecar is configured and available.
        /// </summary>
        public bool HasMySql => _mySqlContainer != null;

        /// <summary>
        /// Gets whether a Redis sidecar is configured and available.
        /// </summary>
        public bool HasRedis => _redisContainer != null;

        /// <summary>
        /// Gets the MySQL connection string for the sidecar database, or <c>null</c> if MySQL is not configured.
        /// Only valid after the container has been started.
        /// </summary>
        public string MySqlConnectionString
        {
            get
            {
                if (_mySqlContainer == null || _mySqlOptions == null)
                    return null;

                int mappedPort = _mySqlContainer.GetMappedPublicPort(_mySqlOptions.Port);
                string host = _mySqlContainer.Hostname;
                return $"Server={host};Port={mappedPort};Database={_mySqlOptions.Database};" +
                       $"Uid={_mySqlOptions.Username};Pwd={_mySqlOptions.Password};";
            }
        }

        /// <summary>
        /// Starts all sidecar containers first, then starts the Unturned server container.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that completes when all containers are running.</returns>
        /// <exception cref="ServerStartupFailedException">If any container fails to start.</exception>
        public async Task StartAsync(CancellationToken ct = default)
        {
            try
            {
                // Start sidecars in parallel
                var sidecarTasks = new List<Task>();

                if (_mySqlContainer != null)
                    sidecarTasks.Add(_mySqlContainer.StartAsync(ct));

                if (_redisContainer != null)
                    sidecarTasks.Add(_redisContainer.StartAsync(ct));

                foreach (var kvp in _additionalContainers)
                    sidecarTasks.Add(kvp.Value.StartAsync(ct));

                if (sidecarTasks.Count > 0)
                    await Task.WhenAll(sidecarTasks).ConfigureAwait(false);

                // Start the main server container
                await _serverContainer.StartAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new ServerStartupFailedException(
                    "Failed to start the Unturned server container or one of its sidecars.", ex);
            }
        }

        /// <summary>
        /// Stops the Unturned server container first, then stops all sidecar containers.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that completes when all containers are stopped.</returns>
        public async Task StopAsync(CancellationToken ct = default)
        {
            // Stop server first
            await _serverContainer.StopAsync(ct).ConfigureAwait(false);

            // Stop sidecars in parallel
            var sidecarTasks = new List<Task>();

            if (_mySqlContainer != null)
                sidecarTasks.Add(_mySqlContainer.StopAsync(ct));

            if (_redisContainer != null)
                sidecarTasks.Add(_redisContainer.StopAsync(ct));

            foreach (var kvp in _additionalContainers)
                sidecarTasks.Add(kvp.Value.StopAsync(ct));

            if (sidecarTasks.Count > 0)
                await Task.WhenAll(sidecarTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies a file or directory from the host to a path inside the server container.
        /// </summary>
        /// <param name="hostPath">The path on the host to copy from.</param>
        /// <param name="containerPath">The destination path inside the container.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that completes when the copy is finished.</returns>
        public async Task CopyToServerAsync(string hostPath, string containerPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(hostPath))
                throw new ArgumentException("Host path must not be null or empty.", nameof(hostPath));
            if (string.IsNullOrWhiteSpace(containerPath))
                throw new ArgumentException("Container path must not be null or empty.", nameof(containerPath));

            byte[] fileBytes = File.ReadAllBytes(hostPath);
            await _serverContainer.CopyAsync(fileBytes, containerPath).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a command inside the Unturned server container.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The exit code and combined stdout/stderr output.</returns>
        public async Task<ExecResult> ExecAsync(string command, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command must not be null or empty.", nameof(command));

            return await _serverContainer.ExecAsync(
                new[] { "/bin/sh", "-c", command }, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the stdout and stderr logs from the Unturned server container.
        /// </summary>
        /// <param name="since">Optional UTC timestamp to get logs since.</param>
        /// <param name="until">Optional UTC timestamp to get logs until. Defaults to now.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A tuple of (stdout, stderr) log content.</returns>
        public async Task<(string Stdout, string Stderr)> GetLogsAsync(
            DateTime? since = null,
            DateTime? until = null,
            CancellationToken ct = default)
        {
            DateTime sinceValue = since ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime untilValue = until ?? DateTime.UtcNow;
            var (stdout, stderr) = await _serverContainer.GetLogsAsync(sinceValue, untilValue, false, ct)
                .ConfigureAwait(false);
            return (stdout, stderr);
        }

        /// <summary>
        /// Starts consuming container stdout/stderr and forwarding each line to the callback.
        /// Call this after the container has been started.
        /// </summary>
        public void StartLogForwarding(Action<string> onLog, CancellationToken ct = default)
        {
            if (_logForwardingTask != null) return;
            _logForwardingTask = Task.Run(async () =>
            {
                try
                {
                    var since = DateTime.UtcNow;
                    while (!ct.IsCancellationRequested && !_disposed)
                    {
                        await Task.Delay(2000, ct).ConfigureAwait(false);
                        try
                        {
                            var (stdout, stderr) = await GetLogsAsync(
                                since: since,
                                until: DateTime.UtcNow,
                                ct: ct).ConfigureAwait(false);
                            since = DateTime.UtcNow;

                            if (!string.IsNullOrEmpty(stdout))
                            {
                                foreach (var line in stdout.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    onLog?.Invoke($"[Unturned] {line.TrimEnd()}");
                            }
                            if (!string.IsNullOrEmpty(stderr))
                            {
                                foreach (var line in stderr.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    onLog?.Invoke($"[Unturned:ERR] {line.TrimEnd()}");
                            }
                        }
                        catch (Exception) when (!ct.IsCancellationRequested)
                        {
                            // Container may have stopped — ignore
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, ct);
        }

        /// <summary>
        /// Disposes all containers and cleans up temporary directories.
        /// Runs registered cleanup callbacks before disposing containers.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Run cleanup callbacks
            foreach (Func<Task> callback in _cleanupCallbacks)
            {
                try
                {
                    await callback().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[UnturnedServerContainer] Cleanup callback failed: {ex.Message}");
                }
            }

            // Dispose containers
            var disposeTasks = new List<Task>();

            disposeTasks.Add(DisposeContainerAsync(_serverContainer));

            if (_mySqlContainer != null)
                disposeTasks.Add(DisposeContainerAsync(_mySqlContainer));

            if (_redisContainer != null)
                disposeTasks.Add(DisposeContainerAsync(_redisContainer));

            foreach (var kvp in _additionalContainers)
                disposeTasks.Add(DisposeContainerAsync(kvp.Value));

            await Task.WhenAll(disposeTasks).ConfigureAwait(false);

            // Clean up temp directory
            try
            {
                if (Directory.Exists(_rocketDirectory))
                    Directory.Delete(_rocketDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[UnturnedServerContainer] Failed to delete temp directory '{_rocketDirectory}': {ex.Message}");
            }
        }

        /// <summary>
        /// Safely disposes a single container, catching and logging any errors.
        /// </summary>
        private static async Task DisposeContainerAsync(IContainer container)
        {
            try
            {
                await container.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UnturnedServerContainer] Failed to dispose container: {ex.Message}");
            }
        }
    }
}
