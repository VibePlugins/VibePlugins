using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Executes RocketMod commands on behalf of the test host, capturing any chat
    /// messages produced during execution.
    /// </summary>
    public static class CommandExecutor
    {
        /// <summary>
        /// Executes a RocketMod command specified by the request and returns the response
        /// including captured chat messages.
        /// </summary>
        /// <param name="request">The command execution request.</param>
        /// <returns>An <see cref="ExecuteCommandResponse"/> describing the outcome.</returns>
        /// <remarks>Must be called on the Unity main thread.</remarks>
        public static ExecuteCommandResponse Execute(ExecuteCommandRequest request)
        {
            var response = new ExecuteCommandResponse();

            try
            {
                // Build the full command string: "commandName arg1 arg2 ..."
                string commandLine = request.CommandName;
                if (request.Args != null && request.Args.Length > 0)
                {
                    commandLine += " " + string.Join(" ", request.Args);
                }

                // Start capturing chat messages for this execution.
                var capturedMessages = new List<ChatMessageInfo>();
                EventCapture.BeginChatCapture(capturedMessages);

                try
                {
                    // Execute via the RocketMod command manager.
                    bool executed = R.Commands.Execute(
                        new ConsolePlayer(request.CallerSteamId, request.CallerName, request.IsAdmin),
                        commandLine);

                    response.Status = executed ? CommandStatus.Success : CommandStatus.NotFound;
                }
                finally
                {
                    EventCapture.EndChatCapture();
                }

                response.ChatMessages = capturedMessages;
            }
            catch (Exception ex)
            {
                response.Status = CommandStatus.Exception;
                response.ExceptionInfo = SerializableExceptionInfo.FromException(ex);
            }

            return response;
        }
    }

    /// <summary>
    /// An <see cref="IRocketPlayer"/> implementation representing a console/simulated caller
    /// with configurable identity and admin status.
    /// </summary>
    internal sealed class ConsolePlayer : IRocketPlayer
    {
        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string DisplayName { get; }

        /// <summary>Whether this simulated player has admin privileges.</summary>
        public bool IsAdmin { get; }

        /// <summary>
        /// Initializes a new <see cref="ConsolePlayer"/>.
        /// </summary>
        /// <param name="steamId">The Steam64 ID of the simulated caller.</param>
        /// <param name="name">The display name.</param>
        /// <param name="isAdmin">Whether the caller has admin privileges.</param>
        public ConsolePlayer(ulong steamId, string name, bool isAdmin)
        {
            Id = steamId.ToString();
            DisplayName = name ?? "TestCaller";
            IsAdmin = isAdmin;
        }

        /// <inheritdoc />
        public int CompareTo(object obj)
        {
            if (obj is IRocketPlayer other)
                return string.Compare(Id, other.Id, StringComparison.Ordinal);
            return 1;
        }
    }
}
