using System;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestBase.Assertions
{
    /// <summary>
    /// Extension methods on <see cref="TestBridgeClient"/> for starting event monitors.
    /// </summary>
    public static class EventTestExtensions
    {
        private static readonly TimeSpan MonitorSetupTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Begins monitoring events of type <typeparamref name="TEvent"/> on the remote server.
        /// The monitor captures all events of the specified type until the test session is cleaned up.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event type to monitor. Must match a type known to the server-side
        /// <c>EventCapture</c> system (e.g. <see cref="ChatMessageEvent"/>,
        /// <see cref="PlayerDeathEvent"/>).
        /// </typeparam>
        /// <param name="bridge">The connected bridge client.</param>
        /// <returns>
        /// An <see cref="EventMonitor{TEvent}"/> that can be used to wait for and assert on events.
        /// </returns>
        public static async Task<EventMonitor<TEvent>> MonitorEventAsync<TEvent>(
            this TestBridgeClient bridge) where TEvent : class
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));

            var monitorId = Guid.NewGuid();
            string eventTypeName = typeof(TEvent).FullName;

            var request = new MonitorEventRequest
            {
                EventTypeName = eventTypeName,
                MonitorId = monitorId
            };

            // MonitorEvent is fire-and-forget on the server side; just send and proceed.
            using (var cts = new CancellationTokenSource(MonitorSetupTimeout))
            {
                await bridge.SendAsync(request, cts.Token).ConfigureAwait(false);
            }

            return new EventMonitor<TEvent>(bridge, monitorId);
        }

        /// <summary>
        /// Begins monitoring <see cref="ChatMessageEvent"/> occurrences on the server.
        /// Convenience shorthand for <c>MonitorEventAsync&lt;ChatMessageEvent&gt;()</c>.
        /// </summary>
        /// <param name="bridge">The connected bridge client.</param>
        /// <returns>An event monitor for chat messages.</returns>
        public static Task<EventMonitor<ChatMessageEvent>> MonitorChatAsync(
            this TestBridgeClient bridge)
        {
            return bridge.MonitorEventAsync<ChatMessageEvent>();
        }

        /// <summary>
        /// Begins monitoring <see cref="PlayerDeathEvent"/> occurrences on the server.
        /// Convenience shorthand for <c>MonitorEventAsync&lt;PlayerDeathEvent&gt;()</c>.
        /// </summary>
        /// <param name="bridge">The connected bridge client.</param>
        /// <returns>An event monitor for player deaths.</returns>
        public static Task<EventMonitor<PlayerDeathEvent>> MonitorPlayerDeathAsync(
            this TestBridgeClient bridge)
        {
            return bridge.MonitorEventAsync<PlayerDeathEvent>();
        }

        /// <summary>
        /// Begins monitoring <see cref="PlayerConnectedEvent"/> occurrences on the server.
        /// Convenience shorthand for <c>MonitorEventAsync&lt;PlayerConnectedEvent&gt;()</c>.
        /// </summary>
        /// <param name="bridge">The connected bridge client.</param>
        /// <returns>An event monitor for player connections.</returns>
        public static Task<EventMonitor<PlayerConnectedEvent>> MonitorPlayerConnectedAsync(
            this TestBridgeClient bridge)
        {
            return bridge.MonitorEventAsync<PlayerConnectedEvent>();
        }
    }
}
