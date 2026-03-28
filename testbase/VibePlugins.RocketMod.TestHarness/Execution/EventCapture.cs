using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Captures events fired inside the game (e.g. chat messages, custom events) and
    /// provides a wait mechanism for the test host to block until a specific event occurs.
    /// Also supports scoped chat-message capture for command execution.
    /// </summary>
    public static class EventCapture
    {
        // ── Monitor tracking ───────────────────────────────────────

        private static readonly ConcurrentDictionary<Guid, EventMonitor> Monitors =
            new ConcurrentDictionary<Guid, EventMonitor>();

        // ── Chat capture (per-thread for command execution) ────────

        [ThreadStatic]
        private static List<ChatMessageInfo> _chatCaptureTarget;

        /// <summary>
        /// Begins capturing chat messages into the provided list.
        /// Call <see cref="EndChatCapture"/> when done.
        /// </summary>
        /// <param name="target">The list to accumulate captured messages into.</param>
        public static void BeginChatCapture(List<ChatMessageInfo> target)
        {
            _chatCaptureTarget = target;
        }

        /// <summary>
        /// Stops capturing chat messages for the current scope.
        /// </summary>
        public static void EndChatCapture()
        {
            _chatCaptureTarget = null;
        }

        /// <summary>
        /// Records a chat message. If a chat capture is active, the message is added
        /// to the capture list. The message is also dispatched to any monitors watching
        /// the <see cref="ChatMessageInfo"/> event type.
        /// </summary>
        /// <param name="info">The chat message to record.</param>
        public static void RecordChat(ChatMessageInfo info)
        {
            _chatCaptureTarget?.Add(info);

            // Also dispatch to monitors that are watching ChatMessageInfo.
            Record(typeof(ChatMessageInfo).FullName, JsonConvert.SerializeObject(info));
        }

        // ── Generic event recording ───────────────────────────────

        /// <summary>
        /// Records an event occurrence, waking any monitors watching the given event type.
        /// </summary>
        /// <param name="eventTypeName">The full type name of the event.</param>
        /// <param name="serializedEvent">JSON-serialized event data.</param>
        public static void Record(string eventTypeName, string serializedEvent)
        {
            foreach (var kvp in Monitors)
            {
                if (kvp.Value.EventTypeName == eventTypeName)
                {
                    kvp.Value.Enqueue(serializedEvent);
                }
            }
        }

        // ── Monitor management ────────────────────────────────────

        /// <summary>
        /// Starts monitoring for events of the specified type.
        /// </summary>
        /// <param name="monitorId">Unique identifier for this monitor.</param>
        /// <param name="eventTypeName">The assembly-qualified or full type name of the event to watch.</param>
        public static void StartMonitor(Guid monitorId, string eventTypeName)
        {
            var monitor = new EventMonitor(eventTypeName);
            Monitors[monitorId] = monitor;
        }

        /// <summary>
        /// Waits for a matching event to occur on the specified monitor, with an optional
        /// predicate filter and timeout.
        /// </summary>
        /// <param name="request">The wait request containing monitor ID, timeout, and optional predicate.</param>
        /// <returns>An <see cref="EventOccurredResponse"/> containing the serialized event data.</returns>
        /// <exception cref="TimeoutException">Thrown if no matching event occurs within the timeout period.</exception>
        public static async Task<EventOccurredResponse> WaitForAsync(WaitForEventRequest request)
        {
            if (!Monitors.TryGetValue(request.MonitorId, out var monitor))
            {
                throw new InvalidOperationException(
                    $"No monitor found with ID '{request.MonitorId}'.");
            }

            // Resolve the optional predicate.
            Func<string, bool> predicate = null;
            if (!string.IsNullOrEmpty(request.PredicateType) &&
                !string.IsNullOrEmpty(request.PredicateMethod))
            {
                predicate = BuildPredicate(
                    request.PredicateAssembly,
                    request.PredicateType,
                    request.PredicateMethod);
            }

            using (var cts = new CancellationTokenSource(request.TimeoutMs))
            {
                string serializedEvent = await monitor.WaitForAsync(predicate, cts.Token)
                    .ConfigureAwait(false);

                return new EventOccurredResponse
                {
                    MonitorId = request.MonitorId,
                    SerializedEvent = serializedEvent
                };
            }
        }

        /// <summary>
        /// Clears all monitors and their captured data.
        /// </summary>
        public static void ClearAll()
        {
            Monitors.Clear();
        }

        // ── Predicate resolution ──────────────────────────────────

        private static Func<string, bool> BuildPredicate(
            string assemblyName, string typeName, string methodName)
        {
            Type predicateType = null;

            if (!string.IsNullOrEmpty(assemblyName))
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                predicateType = assembly?.GetType(typeName);
            }

            if (predicateType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    predicateType = asm.GetType(typeName);
                    if (predicateType != null) break;
                }
            }

            if (predicateType == null)
            {
                throw new TypeLoadException($"Could not resolve predicate type '{typeName}'.");
            }

            MethodInfo method = predicateType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new MissingMethodException(
                    $"Could not find static method '{methodName}' on type '{typeName}'.");
            }

            return (serialized) => (bool)method.Invoke(null, new object[] { serialized });
        }

        // ── Inner monitor class ───────────────────────────────────

        /// <summary>
        /// Tracks a single event-type monitor, buffering captured events and
        /// providing async wait support.
        /// </summary>
        private sealed class EventMonitor
        {
            public string EventTypeName { get; }

            private readonly ConcurrentQueue<string> _buffer = new ConcurrentQueue<string>();
            private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

            public EventMonitor(string eventTypeName)
            {
                EventTypeName = eventTypeName;
            }

            public void Enqueue(string serializedEvent)
            {
                _buffer.Enqueue(serializedEvent);
                _signal.Release();
            }

            public async Task<string> WaitForAsync(Func<string, bool> predicate, CancellationToken ct)
            {
                // First check items already in the buffer.
                var overflow = new List<string>();

                while (_buffer.TryDequeue(out string existing))
                {
                    if (predicate == null || predicate(existing))
                    {
                        // Re-enqueue overflow items.
                        foreach (var item in overflow)
                        {
                            _buffer.Enqueue(item);
                        }
                        return existing;
                    }

                    overflow.Add(existing);
                }

                // Re-enqueue non-matching items.
                foreach (var item in overflow)
                {
                    _buffer.Enqueue(item);
                }

                // Wait for new events.
                while (true)
                {
                    await _signal.WaitAsync(ct).ConfigureAwait(false);

                    if (_buffer.TryDequeue(out string serialized))
                    {
                        if (predicate == null || predicate(serialized))
                        {
                            return serialized;
                        }

                        // Non-matching; put it back and keep waiting.
                        _buffer.Enqueue(serialized);
                    }
                }
            }
        }
    }
}
