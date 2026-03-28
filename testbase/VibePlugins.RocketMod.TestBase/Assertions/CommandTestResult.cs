using System;
using System.Collections.Generic;
using System.Linq;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Assertions
{
    /// <summary>
    /// Wraps an <see cref="ExecuteCommandResponse"/> with assertion-friendly properties and
    /// fluent assertion methods that throw <see cref="XunitException"/> on failure.
    /// </summary>
    public class CommandTestResult
    {
        private readonly ExecuteCommandResponse _response;

        /// <summary>
        /// Initializes a new <see cref="CommandTestResult"/> from the raw response.
        /// </summary>
        /// <param name="response">The command execution response from the server.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="response"/> is <c>null</c>.</exception>
        internal CommandTestResult(ExecuteCommandResponse response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
        }

        /// <summary>Gets the command execution status.</summary>
        public CommandStatus Status => _response.Status;

        /// <summary>Gets the chat messages produced during execution.</summary>
        public IReadOnlyList<ChatMessageInfo> ChatMessages => _response.ChatMessages;

        /// <summary>Gets exception info if the command threw, or <c>null</c>.</summary>
        public SerializableExceptionInfo Exception => _response.ExceptionInfo;

        /// <summary>Gets the underlying response message.</summary>
        public ExecuteCommandResponse Response => _response;

        /// <summary>
        /// Asserts that the command executed successfully.
        /// </summary>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If the status is not <see cref="CommandStatus.Success"/>.</exception>
        public CommandTestResult ShouldSucceed()
        {
            if (_response.Status != CommandStatus.Success)
            {
                string detail = _response.ExceptionInfo != null
                    ? $" Exception: {_response.ExceptionInfo.Type}: {_response.ExceptionInfo.Message}"
                    : string.Empty;
                throw new XunitException(
                    $"Expected command to succeed, but status was {_response.Status}.{detail}");
            }
            return this;
        }

        /// <summary>
        /// Asserts that the command failed (any status other than <see cref="CommandStatus.Success"/>).
        /// </summary>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If the status is <see cref="CommandStatus.Success"/>.</exception>
        public CommandTestResult ShouldFail()
        {
            if (_response.Status == CommandStatus.Success)
            {
                throw new XunitException(
                    "Expected command to fail, but it succeeded.");
            }
            return this;
        }

        /// <summary>
        /// Asserts that the command completed with the specified status.
        /// </summary>
        /// <param name="expected">The expected <see cref="CommandStatus"/>.</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If the actual status does not match.</exception>
        public CommandTestResult ShouldHaveStatus(CommandStatus expected)
        {
            if (_response.Status != expected)
            {
                throw new XunitException(
                    $"Expected command status {expected}, but was {_response.Status}.");
            }
            return this;
        }

        /// <summary>
        /// Asserts that the command threw an exception of the specified type.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">
        /// If no exception was thrown or the exception type does not match.
        /// </exception>
        public CommandTestResult ShouldThrow<TException>() where TException : Exception
        {
            return ShouldThrow(typeof(TException).FullName);
        }

        /// <summary>
        /// Asserts that the command threw an exception whose full type name contains
        /// <paramref name="exceptionTypeName"/>.
        /// </summary>
        /// <param name="exceptionTypeName">
        /// A substring to match against the exception's full type name.
        /// </param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">
        /// If no exception was thrown or the type name does not match.
        /// </exception>
        public CommandTestResult ShouldThrow(string exceptionTypeName)
        {
            if (_response.ExceptionInfo == null)
            {
                throw new XunitException(
                    $"Expected command to throw an exception containing '{exceptionTypeName}', " +
                    "but no exception was thrown.");
            }

            if (_response.ExceptionInfo.Type == null ||
                !_response.ExceptionInfo.Type.Contains(exceptionTypeName))
            {
                throw new XunitException(
                    $"Expected exception type containing '{exceptionTypeName}', " +
                    $"but was '{_response.ExceptionInfo.Type}'.");
            }
            return this;
        }

        /// <summary>
        /// Asserts that at least one chat message contains the specified substring.
        /// </summary>
        /// <param name="substring">The text to search for in chat messages.</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If no chat message contains the substring.</exception>
        public CommandTestResult ShouldContainMessage(string substring)
        {
            if (substring == null) throw new ArgumentNullException(nameof(substring));

            if (_response.ChatMessages == null ||
                !_response.ChatMessages.Any(m => m.Text != null && m.Text.Contains(substring)))
            {
                string messages = _response.ChatMessages == null || _response.ChatMessages.Count == 0
                    ? "(none)"
                    : string.Join(", ", _response.ChatMessages.Select(m => $"\"{m.Text}\""));
                throw new XunitException(
                    $"Expected a chat message containing '{substring}', " +
                    $"but messages were: {messages}");
            }
            return this;
        }

        /// <summary>
        /// Asserts that at least one chat message matches the specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test each chat message.</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If no chat message matches the predicate.</exception>
        public CommandTestResult ShouldContainMessage(Func<ChatMessageInfo, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            if (_response.ChatMessages == null ||
                !_response.ChatMessages.Any(predicate))
            {
                int count = _response.ChatMessages?.Count ?? 0;
                throw new XunitException(
                    $"Expected a chat message matching the predicate, " +
                    $"but none of the {count} message(s) matched.");
            }
            return this;
        }

        /// <summary>
        /// Asserts that no chat message contains the specified substring.
        /// </summary>
        /// <param name="substring">The text that should not appear in any chat message.</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If a chat message contains the substring.</exception>
        public CommandTestResult ShouldNotContainMessage(string substring)
        {
            if (substring == null) throw new ArgumentNullException(nameof(substring));

            if (_response.ChatMessages != null)
            {
                ChatMessageInfo match = _response.ChatMessages
                    .FirstOrDefault(m => m.Text != null && m.Text.Contains(substring));
                if (match != null)
                {
                    throw new XunitException(
                        $"Expected no chat message containing '{substring}', " +
                        $"but found: \"{match.Text}\"");
                }
            }
            return this;
        }

        /// <summary>
        /// Asserts that exactly <paramref name="expected"/> chat messages were produced.
        /// </summary>
        /// <param name="expected">The expected number of chat messages.</param>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="XunitException">If the actual count does not match.</exception>
        public CommandTestResult ShouldHaveMessageCount(int expected)
        {
            int actual = _response.ChatMessages?.Count ?? 0;
            if (actual != expected)
            {
                throw new XunitException(
                    $"Expected {expected} chat message(s), but found {actual}.");
            }
            return this;
        }
    }
}
