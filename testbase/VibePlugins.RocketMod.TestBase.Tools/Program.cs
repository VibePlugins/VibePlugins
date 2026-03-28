using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VibePlugins.RocketMod.TestBase.Tools;

public static class Program
{
    private const string DefaultTag = "vibeplugins:rocketmod";

    // The Containerfile content is embedded as a string constant because this net8.0
    // CLI tool cannot reference the net48 TestBase project or its embedded resources.
    private const string ContainerfileContent = """
        FROM steamcmd/steamcmd:ubuntu-24

        # Install Unturned Dedicated Server directly into /opt/unturned
        RUN steamcmd +force_install_dir /opt/unturned \
            +login anonymous \
            +app_update 1110390 validate \
            +quit

        # Copy RocketMod from Extras into Modules
        RUN cp -r /opt/unturned/Extras/Rocket.Unturned /opt/unturned/Modules/Rocket.Unturned

        # Make the headless binary executable
        RUN chmod +x /opt/unturned/Unturned_Headless.x86_64

        # Expose TCP bridge port
        EXPOSE 27099

        WORKDIR /opt/unturned

        ENTRYPOINT ["./Unturned_Headless.x86_64", "-nographics", "-batchmode"]
        CMD ["-SkipAssets"]
        """;

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("VibePlugins RocketMod test framework CLI");

        var buildImageCommand = new Command("build-image", "Build the Docker image for the Unturned + RocketMod test server");

        var tagOption = new Option<string>(
            "--tag",
            getDefaultValue: () => DefaultTag,
            description: "Docker image tag");

        var forceOption = new Option<bool>(
            "--force",
            getDefaultValue: () => false,
            description: "Force rebuild even if the image already exists");

        buildImageCommand.AddOption(tagOption);
        buildImageCommand.AddOption(forceOption);

        buildImageCommand.SetHandler(async (InvocationContext context) =>
        {
            string tag = context.ParseResult.GetValueForOption(tagOption)!;
            bool force = context.ParseResult.GetValueForOption(forceOption);
            CancellationToken ct = context.GetCancellationToken();

            try
            {
                await BuildImageAsync(tag, force, ct);
                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });

        rootCommand.AddCommand(buildImageCommand);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task BuildImageAsync(string tag, bool force, CancellationToken cancellationToken)
    {
        if (!force)
        {
            Console.WriteLine($"Checking if image '{tag}' already exists...");
            int inspectExit = await RunDockerAsync($"image inspect {tag}", workingDirectory: null, cancellationToken, quiet: true);
            if (inspectExit == 0)
            {
                Console.WriteLine($"Image '{tag}' already exists. Use --force to rebuild.");
                return;
            }
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"vibeplugins-rocketmod-build-{Guid.NewGuid():N}"[..48]);
        Directory.CreateDirectory(tempDir);

        try
        {
            string containerfilePath = Path.Combine(tempDir, "Containerfile");
            await File.WriteAllTextAsync(containerfilePath, ContainerfileContent, cancellationToken);

            Console.WriteLine($"Building image '{tag}'...");

            int exitCode = await RunDockerAsync(
                $"build -t {tag} -f Containerfile .",
                tempDir,
                cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Docker build failed with exit code {exitCode}.");
            }

            Console.WriteLine($"Image '{tag}' built successfully.");
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: failed to clean up temp directory: {ex.Message}");
            }
        }
    }

    private static async Task<int> RunDockerAsync(
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        bool quiet = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (workingDirectory is not null)
        {
            psi.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcsExit = new TaskCompletionSource<bool>();

        process.Exited += (_, _) => tcsExit.TrySetResult(true);

        if (!quiet)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) Console.Error.WriteLine(e.Data);
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

        await using (cancellationToken.Register(() =>
        {
            try { process.Kill(); } catch { /* already exited */ }
        }))
        {
            await tcsExit.Task;
        }

        return process.ExitCode;
    }
}
