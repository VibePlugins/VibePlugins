using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Containers;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestBase
{
    /// <summary>
    /// Manages the lifecycle of a single Unturned server container and its TCP bridge connection.
    /// Sessions are cached per configuration key to allow container reuse across tests.
    /// </summary>
    public class TestSession : IAsyncDisposable
    {
        private static readonly ConcurrentDictionary<string, TestSession> Sessions =
            new ConcurrentDictionary<string, TestSession>();

        private static readonly SemaphoreSlim CreateLock = new SemaphoreSlim(1, 1);

        private readonly UnturnedServerContainer _container;
        private TestBridgeClient _bridge;
        private bool _disposed;

        internal TestSession(UnturnedServerContainer container, TestBridgeClient bridge)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>
        /// Gets the connected TCP bridge client for communicating with the harness.
        /// </summary>
        public TestBridgeClient Bridge => _bridge;

        /// <summary>
        /// Gets the underlying Unturned server container.
        /// </summary>
        public UnturnedServerContainer Container => _container;

        /// <summary>
        /// Gets the MySQL connection string if a MySQL sidecar is configured, otherwise <c>null</c>.
        /// </summary>
        public string MySqlConnectionString => _container.MySqlConnectionString;

        /// <summary>
        /// Gets whether a MySQL sidecar is available.
        /// </summary>
        public bool HasMySql => _container.HasMySql;

        /// <summary>
        /// Gets or creates a <see cref="TestSession"/> for the given container configuration.
        /// Sessions with the same configuration are reused (singleton per config key).
        /// </summary>
        /// <param name="configure">Optional action to configure the container builder.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A started and connected <see cref="TestSession"/>.</returns>
        public static async Task<TestSession> GetOrCreateAsync(
            Action<UnturnedContainerBuilder> configure = null,
            CancellationToken ct = default)
        {
            // Use configuration identity as the cache key.
            // For simplicity, use a hash of the configure delegate's method info,
            // or "default" if no configuration is provided.
            string key = configure?.Method.ToString() ?? "default";

            if (Sessions.TryGetValue(key, out TestSession existing) && !existing._disposed)
                return existing;

            await CreateLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (Sessions.TryGetValue(key, out existing) && !existing._disposed)
                    return existing;

                var builder = new UnturnedContainerBuilder();
                configure?.Invoke(builder);

                UnturnedServerContainer container = builder.Build();
                await container.StartAsync(ct).ConfigureAwait(false);

                var bridge = new TestBridgeClient();
                var session = new TestSession(container, bridge);

                Sessions[key] = session;
                return session;
            }
            finally
            {
                CreateLock.Release();
            }
        }

        /// <summary>
        /// Deletes the contents of the Plugins/ and Libraries/ directories in the Rocket mount,
        /// preparing for a fresh plugin deployment.
        /// </summary>
        public Task CleanPluginDirectoriesAsync()
        {
            string pluginsDir = Path.Combine(_container.RocketDirectory, "Plugins");
            string librariesDir = Path.Combine(_container.RocketDirectory, "Libraries");

            CleanDirectory(pluginsDir);
            CleanDirectory(librariesDir);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Deploys a plugin assembly and its dependencies to the Rocket Plugins/ and Libraries/ directories.
        /// Also deploys the TestHarness plugin assembly.
        /// </summary>
        /// <param name="pluginAssembly">The assembly containing the RocketMod plugin to deploy.</param>
        public Task DeployPluginAsync(Assembly pluginAssembly)
        {
            if (pluginAssembly == null) throw new ArgumentNullException(nameof(pluginAssembly));

            string pluginsDir = Path.Combine(_container.RocketDirectory, "Plugins");
            string librariesDir = Path.Combine(_container.RocketDirectory, "Libraries");

            Directory.CreateDirectory(pluginsDir);
            Directory.CreateDirectory(librariesDir);

            // Copy the plugin assembly to Plugins/
            string pluginSource = pluginAssembly.Location;
            if (!string.IsNullOrEmpty(pluginSource) && File.Exists(pluginSource))
            {
                string pluginDest = Path.Combine(pluginsDir, Path.GetFileName(pluginSource));
                File.Copy(pluginSource, pluginDest, overwrite: true);

                // Copy dependencies to Libraries/
                string pluginDir = Path.GetDirectoryName(pluginSource);
                if (pluginDir != null)
                {
                    foreach (string dllFile in Directory.GetFiles(pluginDir, "*.dll"))
                    {
                        string fileName = Path.GetFileName(dllFile);
                        // Skip the plugin itself — it goes in Plugins/
                        if (string.Equals(fileName, Path.GetFileName(pluginSource), StringComparison.OrdinalIgnoreCase))
                            continue;

                        string dest = Path.Combine(librariesDir, fileName);
                        File.Copy(dllFile, dest, overwrite: true);
                    }
                }
            }

            // Deploy the TestHarness plugin (the server-side component with TCP server + Harmony patches)
            Assembly harnessAssembly = typeof(VibePlugins.RocketMod.TestHarness.TestHarnessPlugin).Assembly;
            string harnessSource = harnessAssembly.Location;
            if (!string.IsNullOrEmpty(harnessSource) && File.Exists(harnessSource))
            {
                string harnessDest = Path.Combine(pluginsDir, Path.GetFileName(harnessSource));
                File.Copy(harnessSource, harnessDest, overwrite: true);
            }

            // Deploy the Shared protocol assembly
            Assembly sharedAssembly = typeof(TestMessage).Assembly;
            string sharedSource = sharedAssembly.Location;
            if (!string.IsNullOrEmpty(sharedSource) && File.Exists(sharedSource))
            {
                string sharedDest = Path.Combine(pluginsDir, Path.GetFileName(sharedSource));
                File.Copy(sharedSource, sharedDest, overwrite: true);
            }

            // Copy all required dependencies to Libraries/
            // The test plugin's build output dir has all transitive deps including 0Harmony.dll.
            // However, Assembly.Location may point to shadow copy paths that lack sibling files.
            // Use the CodeBase/codebase URI which points to the original on-disk location.
            // Copy required runtime dependencies (0Harmony, Newtonsoft.Json) to Libraries/
            // Use AppDomain.CurrentDomain.BaseDirectory which points to the test runner's
            // output directory where all transitive dependencies are present.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (baseDir != null)
            {
                // Copy ALL non-framework DLLs from the plugin output dir to Libraries/
                // This ensures 0Harmony.dll and other transitive deps are available
                foreach (string dllFile in Directory.GetFiles(baseDir, "*.dll"))
                {
                    string fileName = Path.GetFileName(dllFile);
                    // Skip plugin DLLs (already in Plugins/) and framework assemblies
                    if (fileName.StartsWith("ExamplePlugin", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("VibePlugins", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("Rocket.", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("SDG.", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string dest = Path.Combine(librariesDir, fileName);
                    File.Copy(dllFile, dest, overwrite: true);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Connects the TCP bridge and waits for the <see cref="HarnessReadyMessage"/> from the server.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for the harness to become ready.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The <see cref="HarnessReadyMessage"/> received from the harness.</returns>
        /// <exception cref="ServerStartupFailedException">If the harness does not become ready within the timeout.</exception>
        public async Task<HarnessReadyMessage> WaitForHarnessReadyAsync(
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                try
                {
                    // Retry the entire connect + wait-for-ready cycle.
                    // The server may still be booting (connection refused) or may accept
                    // the connection but close it before the harness plugin loads (stream error).
                    const int retryDelayMs = 2000;

                    while (true)
                    {
                        linkedCts.Token.ThrowIfCancellationRequested();
                        try
                        {
                            // Create a fresh bridge for each attempt
                            if (_bridge != null && !_bridge.IsConnected)
                            {
                                try { await _bridge.DisposeAsync().ConfigureAwait(false); } catch { }
                                _bridge = new Protocol.TestBridgeClient();
                            }

                            await _bridge.ConnectAsync(
                                _container.Hostname,
                                _container.BridgePort,
                                linkedCts.Token).ConfigureAwait(false);

                            return await _bridge.WaitForMessageAsync<HarnessReadyMessage>(linkedCts.Token)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) when (
                            !linkedCts.Token.IsCancellationRequested &&
                            (ex is BridgeConnectionFailedException || ex is System.IO.IOException || ex is System.Net.Sockets.SocketException))
                        {
                            // Connection refused or stream closed — server still booting, retry
                            await Task.Delay(retryDelayMs, linkedCts.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new ServerStartupFailedException(
                        $"Harness did not become ready within {timeout.TotalSeconds}s.");
                }
            }
        }

        /// <summary>
        /// Waits for a <see cref="PluginLoadedMessage"/> matching the specified plugin name.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to wait for.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The <see cref="PluginLoadedMessage"/> for the loaded plugin.</returns>
        /// <exception cref="PluginLoadFailedException">If the plugin fails to load or times out.</exception>
        public async Task<PluginLoadedMessage> WaitForPluginLoadAsync(
            string pluginName,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name must not be null or empty.", nameof(pluginName));

            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                try
                {
                    while (true)
                    {
                        linkedCts.Token.ThrowIfCancellationRequested();

                        PluginLoadedMessage msg = await _bridge
                            .WaitForMessageAsync<PluginLoadedMessage>(linkedCts.Token)
                            .ConfigureAwait(false);

                        if (string.Equals(msg.PluginName, pluginName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!msg.Success)
                            {
                                throw new PluginLoadFailedException(
                                    $"Plugin '{pluginName}' failed to load: {msg.Error}");
                            }

                            return msg;
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new PluginLoadFailedException(
                        $"Plugin '{pluginName}' did not load within {timeout.TotalSeconds}s.");
                }
            }
        }

        /// <summary>
        /// Restarts the Unturned server container via Docker restart (faster than recreating).
        /// The bridge client is disconnected and must be reconnected after restart.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task RestartServerAsync(CancellationToken ct = default)
        {
            // Disconnect the old bridge if connected
            if (_bridge != null)
            {
                try { await _bridge.DisposeAsync().ConfigureAwait(false); }
                catch { /* may already be disconnected */ }
            }

            // Use docker restart via Process instead of Testcontainers Stop/Start,
            // because Testcontainers StopAsync removes the container entirely.
            var containerId = _container.ServerContainerId;
            var psi = new System.Diagnostics.ProcessStartInfo("docker", $"restart {containerId}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                await Task.Run(() => proc.WaitForExit(), ct).ConfigureAwait(false);
                if (proc.ExitCode != 0)
                {
                    var stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new ServerStartupFailedException(
                        $"docker restart failed (exit {proc.ExitCode}): {stderr}");
                }
            }

            // Create a fresh bridge client for the restarted container
            _bridge = new TestBridgeClient();
        }

        /// <summary>
        /// Disposes the session, stopping all containers and cleaning up resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            await _bridge.DisposeAsync().ConfigureAwait(false);
            await _container.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes all files and subdirectories within a directory, creating it if it does not exist.
        /// </summary>
        private static void CleanDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    try { File.Delete(file); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[TestSession] Failed to delete '{file}': {ex.Message}");
                    }
                }

                foreach (string dir in Directory.GetDirectories(path))
                {
                    try { Directory.Delete(dir, recursive: true); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[TestSession] Failed to delete directory '{dir}': {ex.Message}");
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
