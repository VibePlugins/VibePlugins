using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VibePlugins.RocketMod.TestBase.Shared.Protocol
{
    /// <summary>
    /// Discriminator for all protocol messages exchanged between the test host and server harness.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TestMessageType
    {
        /// <summary>Harness is loaded and ready to accept commands.</summary>
        HarnessReady,
        /// <summary>A plugin has been loaded (or failed to load).</summary>
        PluginLoaded,
        /// <summary>Request to execute an in-game command.</summary>
        ExecuteCommand,
        /// <summary>Result of an executed command.</summary>
        ExecuteCommandResult,
        /// <summary>Request to invoke arbitrary code on the server.</summary>
        RunCode,
        /// <summary>Result of a RunCode invocation.</summary>
        RunCodeResult,
        /// <summary>Request to create a mock entity.</summary>
        CreateMock,
        /// <summary>Result of a CreateMock request.</summary>
        CreateMockResult,
        /// <summary>Request to wait a number of game ticks.</summary>
        WaitTicks,
        /// <summary>Acknowledgement that ticks have elapsed.</summary>
        WaitTicksResult,
        /// <summary>Request to begin monitoring a specific event type.</summary>
        MonitorEvent,
        /// <summary>Request to wait for a monitored event to occur.</summary>
        WaitForEvent,
        /// <summary>Notification that a monitored event has occurred.</summary>
        EventOccurred,
        /// <summary>Request to run cleanup routines.</summary>
        RunCleanup,
        /// <summary>Result of a cleanup run.</summary>
        RunCleanupResult,
        /// <summary>Request to destroy all mock entities.</summary>
        DestroyAllMocks,
        /// <summary>Result of destroying all mocks.</summary>
        DestroyAllMocksResult,
        /// <summary>Request to clear captured events.</summary>
        ClearEventCaptures,
        /// <summary>Result of clearing event captures.</summary>
        ClearEventCapturesResult,
        /// <summary>Keep-alive heartbeat.</summary>
        Heartbeat,
        /// <summary>Request the harness to shut down.</summary>
        Shutdown
    }

    /// <summary>
    /// Outcome of an executed command.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommandStatus
    {
        /// <summary>Command executed successfully.</summary>
        Success,
        /// <summary>Command was not found.</summary>
        NotFound,
        /// <summary>Command threw an exception.</summary>
        Exception,
        /// <summary>Caller lacks permission to execute the command.</summary>
        PermissionDenied
    }

    /// <summary>
    /// Types of mock entities that can be spawned.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MockEntityType
    {
        /// <summary>A mock player.</summary>
        Player,
        /// <summary>A mock zombie.</summary>
        Zombie,
        /// <summary>A mock animal.</summary>
        Animal
    }

    /// <summary>
    /// Unity update loop phases.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TickType
    {
        /// <summary>Standard Update tick.</summary>
        Update,
        /// <summary>FixedUpdate tick (physics).</summary>
        FixedUpdate,
        /// <summary>LateUpdate tick.</summary>
        LateUpdate
    }

    // ───────────────────────────────────────────────────────────────
    // Supporting data types
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a chat message captured during command execution.
    /// </summary>
    public class ChatMessageInfo
    {
        /// <summary>The text content of the chat message.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        /// <summary>The color of the message (hex string, e.g. "#FFFFFF").</summary>
        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>The display name of the player who sent the message, if any.</summary>
        [JsonProperty("fromPlayerName")]
        public string FromPlayerName { get; set; }

        /// <summary>The display name of the intended recipient, if any.</summary>
        [JsonProperty("toPlayerName")]
        public string ToPlayerName { get; set; }

        /// <summary>The chat mode (e.g. "Global", "Local", "Group").</summary>
        [JsonProperty("mode")]
        public string Mode { get; set; }
    }

    /// <summary>
    /// A JSON-safe representation of an exception, including nested inner exceptions.
    /// </summary>
    public class SerializableExceptionInfo
    {
        /// <summary>The full type name of the exception.</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>The exception message.</summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>The stack trace at the point the exception was thrown.</summary>
        [JsonProperty("stackTrace")]
        public string StackTrace { get; set; }

        /// <summary>The inner exception, if any.</summary>
        [JsonProperty("innerException")]
        public SerializableExceptionInfo InnerException { get; set; }

        /// <summary>
        /// Creates a <see cref="SerializableExceptionInfo"/> from a .NET <see cref="Exception"/>.
        /// </summary>
        /// <param name="ex">The exception to convert.</param>
        /// <returns>A serializable representation, or <c>null</c> if <paramref name="ex"/> is <c>null</c>.</returns>
        public static SerializableExceptionInfo FromException(Exception ex)
        {
            if (ex == null) return null;

            return new SerializableExceptionInfo
            {
                Type = ex.GetType().FullName,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = FromException(ex.InnerException)
            };
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Base message
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Base class for all protocol messages. The <see cref="MessageType"/> property
    /// serves as a discriminator for deserialization.
    /// </summary>
    public abstract class TestMessage
    {
        /// <summary>The type discriminator for this message.</summary>
        [JsonProperty("messageType")]
        public abstract TestMessageType MessageType { get; }

        /// <summary>Unique identifier for correlating request/response pairs.</summary>
        [JsonProperty("correlationId")]
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
    }

    // ───────────────────────────────────────────────────────────────
    // Harness lifecycle
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sent by the harness when it has finished initialising and is ready to accept commands.
    /// </summary>
    public class HarnessReadyMessage : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.HarnessReady;

        /// <summary>Human-readable server version string.</summary>
        [JsonProperty("serverVersion")]
        public string ServerVersion { get; set; }

        /// <summary>Name of the Unturned map currently loaded.</summary>
        [JsonProperty("mapName")]
        public string MapName { get; set; }

        /// <summary>Maximum number of players the server supports.</summary>
        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        /// <summary>The TCP port the harness is listening on.</summary>
        [JsonProperty("harnessPort")]
        public int HarnessPort { get; set; }
    }

    /// <summary>
    /// Sent by the harness when a plugin has been loaded (or failed to load).
    /// </summary>
    public class PluginLoadedMessage : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.PluginLoaded;

        /// <summary>Name of the plugin.</summary>
        [JsonProperty("pluginName")]
        public string PluginName { get; set; }

        /// <summary>Assembly-qualified name of the plugin assembly.</summary>
        [JsonProperty("assembly")]
        public string Assembly { get; set; }

        /// <summary>Whether the plugin loaded successfully.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>Error message if loading failed.</summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Command execution
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to execute a RocketMod command on the server.
    /// </summary>
    public class ExecuteCommandRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.ExecuteCommand;

        /// <summary>The name of the command to execute.</summary>
        [JsonProperty("commandName")]
        public string CommandName { get; set; }

        /// <summary>The Steam64 ID of the simulated caller.</summary>
        [JsonProperty("callerSteamId")]
        public ulong CallerSteamId { get; set; }

        /// <summary>The display name of the simulated caller.</summary>
        [JsonProperty("callerName")]
        public string CallerName { get; set; }

        /// <summary>Whether the simulated caller has admin privileges.</summary>
        [JsonProperty("isAdmin")]
        public bool IsAdmin { get; set; }

        /// <summary>Arguments to pass to the command.</summary>
        [JsonProperty("args")]
        public string[] Args { get; set; }
    }

    /// <summary>
    /// Result of executing a command on the server.
    /// </summary>
    public class ExecuteCommandResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.ExecuteCommandResult;

        /// <summary>The outcome status of the command.</summary>
        [JsonProperty("status")]
        public CommandStatus Status { get; set; }

        /// <summary>Chat messages produced during execution.</summary>
        [JsonProperty("chatMessages")]
        public List<ChatMessageInfo> ChatMessages { get; set; } = new List<ChatMessageInfo>();

        /// <summary>Exception info if the command threw.</summary>
        [JsonProperty("exceptionInfo")]
        public SerializableExceptionInfo ExceptionInfo { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Arbitrary code execution
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to invoke a static method on the server.
    /// </summary>
    public class RunCodeRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.RunCode;

        /// <summary>Assembly-qualified type name containing the method.</summary>
        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        /// <summary>Name of the static method to invoke.</summary>
        [JsonProperty("methodName")]
        public string MethodName { get; set; }

        /// <summary>JSON-serialized arguments for the method.</summary>
        [JsonProperty("serializedArgs")]
        public string[] SerializedArgs { get; set; }
    }

    /// <summary>
    /// Result of a RunCode invocation.
    /// </summary>
    public class RunCodeResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.RunCodeResult;

        /// <summary>JSON-serialized return value, or <c>null</c> for void methods.</summary>
        [JsonProperty("serializedResult")]
        public string SerializedResult { get; set; }

        /// <summary>Exception info if the invocation threw.</summary>
        [JsonProperty("exceptionInfo")]
        public SerializableExceptionInfo ExceptionInfo { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Mock entities
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to create a mock entity on the server.
    /// </summary>
    public class CreateMockRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.CreateMock;

        /// <summary>The type of entity to create.</summary>
        [JsonProperty("entityType")]
        public MockEntityType EntityType { get; set; }

        /// <summary>JSON-serialized options for the entity (PlayerOptions, ZombieOptions, etc.).</summary>
        [JsonProperty("optionsJson")]
        public string OptionsJson { get; set; }
    }

    /// <summary>
    /// Result of creating a mock entity.
    /// </summary>
    public class CreateMockResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.CreateMockResult;

        /// <summary>Unique handle identifying the created mock entity.</summary>
        [JsonProperty("handleId")]
        public Guid HandleId { get; set; }

        /// <summary>Whether the mock was created successfully.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>Error message if creation failed.</summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Tick waiting
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to wait for a specified number of game ticks before continuing.
    /// </summary>
    public class WaitTicksRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.WaitTicks;

        /// <summary>Number of ticks to wait.</summary>
        [JsonProperty("count")]
        public int Count { get; set; }

        /// <summary>Which update phase to count ticks in.</summary>
        [JsonProperty("tickType")]
        public TickType TickType { get; set; } = TickType.Update;
    }

    /// <summary>
    /// Acknowledgement that the requested ticks have elapsed.
    /// </summary>
    public class WaitTicksResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.WaitTicksResult;
    }

    // ───────────────────────────────────────────────────────────────
    // Event monitoring
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to start monitoring occurrences of a specific event type.
    /// </summary>
    public class MonitorEventRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.MonitorEvent;

        /// <summary>Assembly-qualified type name of the event to monitor.</summary>
        [JsonProperty("eventTypeName")]
        public string EventTypeName { get; set; }

        /// <summary>Identifier for this monitor, used to correlate with wait/occurred messages.</summary>
        [JsonProperty("monitorId")]
        public Guid MonitorId { get; set; } = Guid.NewGuid();
    }

    /// <summary>
    /// Request to wait until a monitored event fires (optionally matching a predicate).
    /// </summary>
    public class WaitForEventRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.WaitForEvent;

        /// <summary>The monitor to wait on.</summary>
        [JsonProperty("monitorId")]
        public Guid MonitorId { get; set; }

        /// <summary>Maximum time to wait in milliseconds before timing out.</summary>
        [JsonProperty("timeoutMs")]
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>Assembly containing the predicate type, if any.</summary>
        [JsonProperty("predicateAssembly")]
        public string PredicateAssembly { get; set; }

        /// <summary>Type containing the predicate method, if any.</summary>
        [JsonProperty("predicateType")]
        public string PredicateType { get; set; }

        /// <summary>Static method name to use as a predicate filter, if any.</summary>
        [JsonProperty("predicateMethod")]
        public string PredicateMethod { get; set; }
    }

    /// <summary>
    /// Notification that a monitored event has occurred.
    /// </summary>
    public class EventOccurredResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.EventOccurred;

        /// <summary>The monitor that captured this event.</summary>
        [JsonProperty("monitorId")]
        public Guid MonitorId { get; set; }

        /// <summary>JSON-serialized event arguments.</summary>
        [JsonProperty("serializedEvent")]
        public string SerializedEvent { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Cleanup
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to run all registered cleanup routines on the server.
    /// </summary>
    public class RunCleanupRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.RunCleanup;
    }

    /// <summary>
    /// Result of running cleanup routines.
    /// </summary>
    public class RunCleanupResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.RunCleanupResult;

        /// <summary>Whether cleanup completed without errors.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>Error message if cleanup failed.</summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// Request to destroy all mock entities on the server.
    /// </summary>
    public class DestroyAllMocksRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.DestroyAllMocks;
    }

    /// <summary>
    /// Result of destroying all mock entities.
    /// </summary>
    public class DestroyAllMocksResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.DestroyAllMocksResult;

        /// <summary>Whether all mocks were destroyed successfully.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>Error message if destruction failed.</summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// Request to clear all captured event data.
    /// </summary>
    public class ClearEventCapturesRequest : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.ClearEventCaptures;
    }

    /// <summary>
    /// Result of clearing event captures.
    /// </summary>
    public class ClearEventCapturesResponse : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.ClearEventCapturesResult;

        /// <summary>Whether captures were cleared successfully.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    // ───────────────────────────────────────────────────────────────
    // Heartbeat & Shutdown
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Keep-alive message sent periodically to detect broken connections.
    /// </summary>
    public class HeartbeatMessage : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.Heartbeat;

        /// <summary>UTC timestamp when the heartbeat was sent.</summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request the harness to shut down the server gracefully.
    /// </summary>
    public class ShutdownMessage : TestMessage
    {
        /// <inheritdoc />
        public override TestMessageType MessageType => TestMessageType.Shutdown;

        /// <summary>Optional reason for the shutdown.</summary>
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
}
