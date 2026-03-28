using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Assertions
{
    /// <summary>
    /// Monitors events of type <typeparamref name="TEvent"/> on the remote server.
    /// Provides async wait methods and assertion helpers for event-driven testing.
    /// </summary>
    /// <typeparam name="TEvent">The event type to monitor. Must be a reference type.</typeparam>
    /// <remarks>
    /// Obtain an instance via <see cref="EventTestExtensions.MonitorEventAsync{TEvent}"/>
    /// or the convenience methods on the test base class.
    /// </remarks>
    public class EventMonitor<TEvent> where TEvent : class
    {
        private readonly TestBridgeClient _bridge;
        private readonly Guid _monitorId;
        private readonly ConcurrentQueue<TEvent> _received = new ConcurrentQueue<TEvent>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultNegativeTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new <see cref="EventMonitor{TEvent}"/>.
        /// </summary>
        /// <param name="bridge">The connected bridge client for communicating with the harness.</param>
        /// <param name="monitorId">
        /// The unique monitor identifier returned when monitoring was registered on the server.
        /// </param>
        internal EventMonitor(TestBridgeClient bridge, Guid monitorId)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _monitorId = monitorId;
        }

        /// <summary>Gets the monitor identifier used on the server side.</summary>
        public Guid MonitorId => _monitorId;

        /// <summary>
        /// Waits for a single event matching the optional predicate to occur on the server.
        /// </summary>
        /// <param name="predicate">
        /// An optional filter. If <c>null</c>, the first event of type <typeparamref name="TEvent"/> is returned.
        /// </param>
        /// <param name="timeout">
        /// Maximum time to wait. Defaults to 10 seconds if not specified.
        /// </param>
        /// <returns>The deserialized event that matched the predicate.</returns>
        /// <exception cref="TimeoutException">If no matching event occurs within the timeout.</exception>
        public async Task<TEvent> WaitForAsync(
            Func<TEvent, bool> predicate = null,
            TimeSpan? timeout = null)
        {
            TimeSpan effectiveTimeout = timeout ?? DefaultTimeout;

            // First check the local buffer for already-received events.
            TEvent buffered = TryDequeueMatching(predicate);
            if (buffered != null)
                return buffered;

            // Ask the server to wait for the event.
            var request = new WaitForEventRequest
            {
                MonitorId = _monitorId,
                TimeoutMs = (int)effectiveTimeout.TotalMilliseconds
            };

            using (var cts = new CancellationTokenSource(effectiveTimeout.Add(TimeSpan.FromSeconds(5))))
            {
                EventOccurredResponse response = await _bridge
                    .SendAndWaitAsync<EventOccurredResponse>(request, cts.Token)
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(response.SerializedEvent))
                {
                    throw new TimeoutException(
                        $"Timed out waiting for event '{typeof(TEvent).Name}' " +
                        $"after {effectiveTimeout.TotalSeconds:F1}s.");
                }

                TEvent evt = JsonConvert.DeserializeObject<TEvent>(response.SerializedEvent);

                if (predicate != null && !predicate(evt))
                {
                    // Event didn't match the host-side predicate; buffer it and keep waiting.
                    _received.Enqueue(evt);
                    return await WaitForAsync(predicate, timeout).ConfigureAwait(false);
                }

                return evt;
            }
        }

        /// <summary>
        /// Waits for <paramref name="count"/> events matching the optional predicate.
        /// </summary>
        /// <param name="count">The number of matching events to collect.</param>
        /// <param name="predicate">
        /// An optional filter applied to each event. If <c>null</c>, all events match.
        /// </param>
        /// <param name="timeout">
        /// Maximum total time to wait for all events. Defaults to 10 seconds if not specified.
        /// </param>
        /// <returns>A read-only list of the collected events in the order they were received.</returns>
        /// <exception cref="TimeoutException">
        /// If fewer than <paramref name="count"/> matching events occur within the timeout.
        /// </exception>
        public async Task<IReadOnlyList<TEvent>> WaitForCountAsync(
            int count,
            Func<TEvent, bool> predicate = null,
            TimeSpan? timeout = null)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");

            TimeSpan effectiveTimeout = timeout ?? DefaultTimeout;
            var deadline = DateTime.UtcNow + effectiveTimeout;
            var results = new List<TEvent>(count);

            while (results.Count < count)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for {count} event(s) of type '{typeof(TEvent).Name}'. " +
                        $"Only received {results.Count} within {effectiveTimeout.TotalSeconds:F1}s.");
                }

                TEvent evt = await WaitForAsync(predicate, remaining).ConfigureAwait(false);
                results.Add(evt);
            }

            return results.AsReadOnly();
        }

        /// <summary>
        /// Asserts that no event matching the optional predicate occurs within the given timeout.
        /// </summary>
        /// <param name="predicate">
        /// An optional filter. If <c>null</c>, asserts that no event of the type occurs at all.
        /// </param>
        /// <param name="timeout">
        /// How long to wait before concluding no event occurred. Defaults to 2 seconds if not specified.
        /// </param>
        /// <exception cref="XunitException">If a matching event occurs within the timeout.</exception>
        public async Task ShouldNotOccurAsync(
            Func<TEvent, bool> predicate = null,
            TimeSpan? timeout = null)
        {
            TimeSpan effectiveTimeout = timeout ?? DefaultNegativeTimeout;

            try
            {
                TEvent evt = await WaitForAsync(predicate, effectiveTimeout).ConfigureAwait(false);

                // If we get here, an event occurred when it should not have.
                string detail = evt != null ? JsonConvert.SerializeObject(evt) : "(null)";
                throw new XunitException(
                    $"Expected no event of type '{typeof(TEvent).Name}' to occur, " +
                    $"but received: {detail}");
            }
            catch (TimeoutException)
            {
                // Good -- no event occurred within the window.
            }
        }

        /// <summary>
        /// Attempts to dequeue a previously buffered event matching the predicate.
        /// </summary>
        private TEvent TryDequeueMatching(Func<TEvent, bool> predicate)
        {
            if (_received.IsEmpty)
                return null;

            var overflow = new List<TEvent>();
            TEvent match = null;

            while (_received.TryDequeue(out TEvent candidate))
            {
                if (match == null && (predicate == null || predicate(candidate)))
                {
                    match = candidate;
                }
                else
                {
                    overflow.Add(candidate);
                }
            }

            foreach (TEvent item in overflow)
            {
                _received.Enqueue(item);
            }

            return match;
        }
    }
}
