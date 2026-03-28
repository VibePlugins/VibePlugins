using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VibePlugins.RocketMod.TestBase.Shared.Protocol
{
    /// <summary>
    /// Handles serialization and deserialization of <see cref="TestMessage"/> instances
    /// using length-prefixed JSON over streams (TCP).
    /// </summary>
    /// <remarks>
    /// Wire format: [4 bytes big-endian length][UTF-8 JSON payload].
    /// The JSON payload includes a <c>messageType</c> discriminator used to
    /// resolve the correct concrete type during deserialization.
    /// </remarks>
    public static class MessageSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None
        };

        private static readonly Dictionary<TestMessageType, Type> TypeMap = new Dictionary<TestMessageType, Type>
        {
            { TestMessageType.HarnessReady, typeof(HarnessReadyMessage) },
            { TestMessageType.PluginLoaded, typeof(PluginLoadedMessage) },
            { TestMessageType.ExecuteCommand, typeof(ExecuteCommandRequest) },
            { TestMessageType.ExecuteCommandResult, typeof(ExecuteCommandResponse) },
            { TestMessageType.RunCode, typeof(RunCodeRequest) },
            { TestMessageType.RunCodeResult, typeof(RunCodeResponse) },
            { TestMessageType.CreateMock, typeof(CreateMockRequest) },
            { TestMessageType.CreateMockResult, typeof(CreateMockResponse) },
            { TestMessageType.WaitTicks, typeof(WaitTicksRequest) },
            { TestMessageType.WaitTicksResult, typeof(WaitTicksResponse) },
            { TestMessageType.MonitorEvent, typeof(MonitorEventRequest) },
            { TestMessageType.WaitForEvent, typeof(WaitForEventRequest) },
            { TestMessageType.EventOccurred, typeof(EventOccurredResponse) },
            { TestMessageType.RunCleanup, typeof(RunCleanupRequest) },
            { TestMessageType.RunCleanupResult, typeof(RunCleanupResponse) },
            { TestMessageType.DestroyAllMocks, typeof(DestroyAllMocksRequest) },
            { TestMessageType.DestroyAllMocksResult, typeof(DestroyAllMocksResponse) },
            { TestMessageType.ClearEventCaptures, typeof(ClearEventCapturesRequest) },
            { TestMessageType.ClearEventCapturesResult, typeof(ClearEventCapturesResponse) },
            { TestMessageType.Heartbeat, typeof(HeartbeatMessage) },
            { TestMessageType.Shutdown, typeof(ShutdownMessage) }
        };

        /// <summary>
        /// Serializes a <see cref="TestMessage"/> to a length-prefixed byte array.
        /// </summary>
        /// <param name="message">The message to serialize.</param>
        /// <returns>A byte array containing [4-byte big-endian length][UTF-8 JSON].</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
        public static byte[] Serialize(TestMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            string json = JsonConvert.SerializeObject(message, Settings);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] result = new byte[4 + jsonBytes.Length];

            // Write length as big-endian 32-bit integer
            result[0] = (byte)((jsonBytes.Length >> 24) & 0xFF);
            result[1] = (byte)((jsonBytes.Length >> 16) & 0xFF);
            result[2] = (byte)((jsonBytes.Length >> 8) & 0xFF);
            result[3] = (byte)(jsonBytes.Length & 0xFF);

            Buffer.BlockCopy(jsonBytes, 0, result, 4, jsonBytes.Length);
            return result;
        }

        /// <summary>
        /// Deserializes a JSON byte array (without the length prefix) into the correct
        /// <see cref="TestMessage"/> subclass.
        /// </summary>
        /// <param name="data">UTF-8 encoded JSON payload.</param>
        /// <returns>The deserialized message.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The message type is unknown or missing.</exception>
        public static TestMessage Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string json = Encoding.UTF8.GetString(data);
            JObject obj = JObject.Parse(json);

            JToken typeToken = obj["messageType"];
            if (typeToken == null)
            {
                throw new InvalidOperationException("Message does not contain a 'messageType' discriminator.");
            }

            var messageType = typeToken.ToObject<TestMessageType>();

            if (!TypeMap.TryGetValue(messageType, out Type targetType))
            {
                throw new InvalidOperationException($"Unknown message type: {messageType}");
            }

            return (TestMessage)JsonConvert.DeserializeObject(json, targetType, Settings);
        }

        /// <summary>
        /// Writes a length-prefixed message to the given stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A task that completes when the message has been written.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="message"/> is <c>null</c>.</exception>
        public static async Task WriteMessageAsync(Stream stream, TestMessage message)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (message == null) throw new ArgumentNullException(nameof(message));

            byte[] data = Serialize(message);
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a single length-prefixed message from the given stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The deserialized message.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="EndOfStreamException">The stream ended before a complete message could be read.</exception>
        public static async Task<TestMessage> ReadMessageAsync(Stream stream, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            // Read the 4-byte length prefix
            byte[] lengthBuffer = await ReadExactAsync(stream, 4, ct).ConfigureAwait(false);
            int length = (lengthBuffer[0] << 24)
                       | (lengthBuffer[1] << 16)
                       | (lengthBuffer[2] << 8)
                       | lengthBuffer[3];

            if (length <= 0 || length > 16 * 1024 * 1024) // 16 MB safety limit
            {
                throw new InvalidOperationException($"Invalid message length: {length}");
            }

            // Read the JSON payload
            byte[] payload = await ReadExactAsync(stream, length, ct).ConfigureAwait(false);
            return Deserialize(payload);
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from the stream,
        /// throwing <see cref="EndOfStreamException"/> if the stream ends prematurely.
        /// </summary>
        private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken ct)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                ct.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, offset, count - offset, ct).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException(
                        $"Connection closed after reading {offset} of {count} expected bytes.");
                }
                offset += read;
            }

            return buffer;
        }
    }
}
