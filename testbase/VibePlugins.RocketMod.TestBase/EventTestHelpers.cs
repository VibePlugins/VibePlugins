using System;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Assertions;
using VibePlugins.RocketMod.TestBase.Protocol;

namespace VibePlugins.RocketMod.TestBase
{
    /// <summary>
    /// Convenience factory for creating typed <see cref="EventMonitor{TEvent}"/> instances
    /// from a <see cref="TestBridgeClient"/>. Provides a simpler API surface for tests that
    /// do not need the full assertion/extension method infrastructure in
    /// <see cref="Assertions.EventTestExtensions"/>.
    /// </summary>
    public static class EventMonitorFactory
    {
        /// <summary>
        /// Creates and immediately starts a new <see cref="EventMonitor{TEvent}"/>
        /// for the specified event type.
        /// </summary>
        /// <typeparam name="TEvent">
        /// The event DTO type to monitor. Must match a type known to the server-side
        /// <c>EventCapture</c> system.
        /// </typeparam>
        /// <param name="bridge">The connected bridge client.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A started <see cref="EventMonitor{TEvent}"/> ready for
        /// <see cref="EventMonitor{TEvent}.WaitForAsync"/> calls.
        /// </returns>
        public static Task<EventMonitor<TEvent>> StartAsync<TEvent>(
            TestBridgeClient bridge,
            CancellationToken ct = default) where TEvent : class
        {
            return bridge.MonitorEventAsync<TEvent>();
        }

        /// <summary>
        /// Creates and immediately starts a new <see cref="EventMonitor{TEvent}"/>
        /// using the <c>Server</c> bridge from a <see cref="RocketModPluginTestBase"/> test.
        /// </summary>
        /// <typeparam name="TEvent">The event DTO type to monitor.</typeparam>
        /// <param name="bridge">The connected bridge client (typically <c>Server</c>).</param>
        /// <returns>
        /// A started <see cref="EventMonitor{TEvent}"/> ready for
        /// <see cref="EventMonitor{TEvent}.WaitForAsync"/> calls.
        /// </returns>
        public static Task<EventMonitor<TEvent>> MonitorAsync<TEvent>(
            TestBridgeClient bridge) where TEvent : class
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            return bridge.MonitorEventAsync<TEvent>();
        }
    }
}
