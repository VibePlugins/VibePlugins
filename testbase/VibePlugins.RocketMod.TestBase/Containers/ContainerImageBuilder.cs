using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Builds the Docker image for the Unturned + RocketMod test server
    /// by extracting the embedded Containerfile and shelling out to the Docker CLI.
    /// </summary>
    public static class ContainerImageBuilder
    {
        public const string DefaultTag = "vibeplugins:rocketmod";

        private const string EmbeddedResourceName =
            "VibePlugins.RocketMod.TestBase.Resources.Containerfile";

        /// <summary>
        /// Ensures the Docker image exists locally. Builds it if it does not.
        /// </summary>
        public static async Task EnsureImageExistsAsync(
            string tag = DefaultTag,
            CancellationToken cancellationToken = default)
        {
            if (await ImageExistsAsync(tag, cancellationToken).ConfigureAwait(false))
            {
                Console.WriteLine($"[ContainerImageBuilder] Image '{tag}' already exists.");
                return;
            }

            Console.WriteLine($"[ContainerImageBuilder] Image '{tag}' not found. Building...");
            await BuildImageAsync(tag, force: false, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds the Docker image. When <paramref name="force"/> is true the image is rebuilt
        /// even if it already exists locally.
        /// </summary>
        public static async Task BuildImageAsync(
            string tag = DefaultTag,
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            if (!force && await ImageExistsAsync(tag, cancellationToken).ConfigureAwait(false))
            {
                Console.WriteLine($"[ContainerImageBuilder] Image '{tag}' already exists. Use force=true to rebuild.");
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "vibeplugins-rocketmod-build-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            try
            {
                string containerfilePath = Path.Combine(tempDir, "Containerfile");
                ExtractEmbeddedContainerfile(containerfilePath);

                Console.WriteLine($"[ContainerImageBuilder] Building image '{tag}' from {tempDir}...");

                int exitCode = await RunDockerAsync(
                    $"build -t {tag} -f Containerfile .",
                    tempDir,
                    cancellationToken).ConfigureAwait(false);

                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Docker build failed with exit code {exitCode}.");
                }

                Console.WriteLine($"[ContainerImageBuilder] Image '{tag}' built successfully.");
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ContainerImageBuilder] Warning: failed to clean up temp directory '{tempDir}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks whether a Docker image with the given tag exists locally.
        /// </summary>
        private static async Task<bool> ImageExistsAsync(
            string tag,
            CancellationToken cancellationToken)
        {
            int exitCode = await RunDockerAsync(
                $"image inspect {tag}",
                workingDirectory: null,
                cancellationToken,
                quiet: true).ConfigureAwait(false);

            return exitCode == 0;
        }

        /// <summary>
        /// Extracts the embedded Containerfile resource to the specified path on disk.
        /// </summary>
        private static void ExtractEmbeddedContainerfile(string destinationPath)
        {
            Assembly assembly = typeof(ContainerImageBuilder).Assembly;

            using (Stream resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName))
            {
                if (resourceStream == null)
                {
                    throw new InvalidOperationException(
                        $"Embedded resource '{EmbeddedResourceName}' not found in assembly '{assembly.FullName}'. " +
                        "Ensure the Containerfile is included as an EmbeddedResource in the project.");
                }

                using (FileStream fileStream = File.Create(destinationPath))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
        }

        /// <summary>
        /// Runs a docker CLI command asynchronously and streams output to the console.
        /// </summary>
        private static async Task<int> RunDockerAsync(
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            bool quiet = false)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (workingDirectory != null)
            {
                psi.WorkingDirectory = workingDirectory;
            }

            using (Process process = new Process { StartInfo = psi })
            {
                TaskCompletionSource<bool> tcsExit = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcsExit.TrySetResult(true);

                if (!quiet)
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) Console.WriteLine(e.Data);
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) Console.Error.WriteLine(e.Data);
                    };
                }

                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start docker process.");
                }

                if (!quiet)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                using (cancellationToken.Register(() =>
                {
                    try { process.Kill(); } catch { /* process may have already exited */ }
                }))
                {
                    await tcsExit.Task.ConfigureAwait(false);
                }

                return process.ExitCode;
            }
        }
    }
}
