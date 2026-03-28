using System;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestBase.Assertions
{
    /// <summary>
    /// Fluent builder for configuring and executing a RocketMod command on the remote
    /// Unturned server. Obtain an instance via
    /// <see cref="RocketModPluginTestBase{TPlugin}.ExecuteCommand(string)"/>.
    /// </summary>
    /// <remarks>
    /// Usage example:
    /// <code>
    /// var result = await ExecuteCommand("heal")
    ///     .AsPlayer("Alice", 76561198000000001, isAdmin: true)
    ///     .WithArgs("100")
    ///     .RunAsync();
    ///
    /// result.ShouldSucceed().ShouldContainMessage("healed");
    /// </code>
    /// </remarks>
    public class CommandTestBuilder
    {
        private readonly TestBridgeClient _bridge;
        private readonly string _commandName;

        private string[] _args = Array.Empty<string>();
        private ulong _callerSteamId;
        private string _callerName = "Console";
        private bool _isAdmin = true;
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new <see cref="CommandTestBuilder"/> for the specified command.
        /// </summary>
        /// <param name="bridge">The connected bridge client.</param>
        /// <param name="commandName">The RocketMod command name to execute.</param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="bridge"/> or <paramref name="commandName"/> is <c>null</c>.
        /// </exception>
        internal CommandTestBuilder(TestBridgeClient bridge, string commandName)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _commandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
        }

        /// <summary>
        /// Sets the caller to a mock player identified by a previously created handle ID.
        /// The server will resolve the player's name, Steam ID, and admin status from the mock.
        /// </summary>
        /// <param name="playerHandleId">
        /// The <see cref="CreateMockResponse.HandleId"/> returned when the mock player was created.
        /// </param>
        /// <returns>This builder for fluent chaining.</returns>
        public CommandTestBuilder AsPlayer(Guid playerHandleId)
        {
            // Use the handle ID as the Steam ID placeholder; the harness resolves it.
            // We encode the GUID into the caller name so the harness can look it up.
            _callerSteamId = 0;
            _callerName = $"__mock:{playerHandleId}";
            _isAdmin = false;
            return this;
        }

        /// <summary>
        /// Sets the caller to an inline (ad-hoc) player with the given identity.
        /// </summary>
        /// <param name="playerName">The display name of the simulated player.</param>
        /// <param name="steamId">The Steam64 ID of the simulated player.</param>
        /// <param name="isAdmin">Whether the simulated player has admin privileges.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public CommandTestBuilder AsPlayer(string playerName, ulong steamId, bool isAdmin = false)
        {
            _callerName = playerName ?? throw new ArgumentNullException(nameof(playerName));
            _callerSteamId = steamId;
            _isAdmin = isAdmin;
            return this;
        }

        /// <summary>
        /// Sets the caller to the server console (Steam ID 0, admin by default).
        /// This is the default if no caller is specified.
        /// </summary>
        /// <returns>This builder for fluent chaining.</returns>
        public CommandTestBuilder AsConsole()
        {
            _callerSteamId = 0;
            _callerName = "Console";
            _isAdmin = true;
            return this;
        }

        /// <summary>
        /// Sets the arguments to pass to the command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public CommandTestBuilder WithArgs(params string[] args)
        {
            _args = args ?? Array.Empty<string>();
            return this;
        }

        /// <summary>
        /// Overrides the default timeout for the command execution request.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public CommandTestBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Sends the command to the server and waits for the response.
        /// </summary>
        /// <returns>
        /// A <see cref="CommandTestResult"/> wrapping the response, with fluent assertion methods.
        /// </returns>
        /// <exception cref="OperationCanceledException">If the request times out.</exception>
        public async Task<CommandTestResult> RunAsync()
        {
            var request = new ExecuteCommandRequest
            {
                CommandName = _commandName,
                CallerSteamId = _callerSteamId,
                CallerName = _callerName,
                IsAdmin = _isAdmin,
                Args = _args
            };

            using (var cts = new CancellationTokenSource(_timeout))
            {
                ExecuteCommandResponse response = await _bridge
                    .SendAndWaitAsync<ExecuteCommandResponse>(request, cts.Token)
                    .ConfigureAwait(false);

                return new CommandTestResult(response);
            }
        }
    }
}
