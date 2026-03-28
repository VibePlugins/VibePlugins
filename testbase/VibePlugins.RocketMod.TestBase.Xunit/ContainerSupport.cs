using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VibePlugins.RocketMod.TestBase.Xunit
{
    /// <summary>
    /// Provides static helpers for determining whether Docker container support is available
    /// in the current environment. Used by the test framework to skip container-dependent
    /// tests when Docker is not accessible.
    /// </summary>
    public static class ContainerSupport
    {
        private const string EnvVar = "VIBEPLUGINS_ENABLE_CONTAINERS";

        private static bool? _cachedResult;
        private static readonly object CacheLock = new object();

        /// <summary>
        /// Gets whether container support is available. First checks the
        /// <c>VIBEPLUGINS_ENABLE_CONTAINERS</c> environment variable (values "true"/"1" enable,
        /// "false"/"0" disable). If the variable is not set, falls back to probing Docker.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                lock (CacheLock)
                {
                    if (_cachedResult.HasValue)
                        return _cachedResult.Value;

                    string envValue = Environment.GetEnvironmentVariable(EnvVar);
                    if (!string.IsNullOrEmpty(envValue))
                    {
                        bool enabled = envValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                                       || envValue == "1";
                        _cachedResult = enabled;
                        return enabled;
                    }

                    // Fall back to probing Docker
                    _cachedResult = CheckDockerSync();
                    return _cachedResult.Value;
                }
            }
        }

        /// <summary>
        /// Asynchronously checks whether Docker is running and accessible by executing
        /// <c>docker info</c>.
        /// </summary>
        /// <returns><c>true</c> if Docker responded successfully; otherwise <c>false</c>.</returns>
        public static async Task<bool> CheckDockerAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;

                    // Consume output to prevent deadlocks
                    Task stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task stderrTask = process.StandardError.ReadToEndAsync();

                    bool exited = process.WaitForExit(10_000);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { /* best effort */ }
                        return false;
                    }

                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Synchronous Docker probe used for the cached <see cref="IsAvailable"/> property.
        /// </summary>
        private static bool CheckDockerSync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;

                    bool exited = process.WaitForExit(10_000);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { /* best effort */ }
                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
