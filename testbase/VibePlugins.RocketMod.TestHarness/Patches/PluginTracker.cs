using System;
using System.Collections.Concurrent;
using Rocket.Core.Plugins;
using RocketPlugin = Rocket.Core.Plugins.RocketPlugin;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Patches
{
    /// <summary>
    /// Tracks plugin activation/deactivation lifecycle events and sends
    /// <see cref="PluginLoadedMessage"/> notifications to the connected test host.
    /// </summary>
    public static class PluginTracker
    {
        /// <summary>
        /// Tracks the load status of each known plugin by name.
        /// </summary>
        private static readonly ConcurrentDictionary<string, PluginStatus> PluginStatuses =
            new ConcurrentDictionary<string, PluginStatus>(StringComparer.OrdinalIgnoreCase);

        private static Func<HarnessTcpServer> _serverAccessor;

        /// <summary>
        /// Initializes the tracker with a function that provides access to the TCP server.
        /// Called once during <see cref="TestHarnessPlugin"/> activation.
        /// </summary>
        /// <param name="serverAccessor">A function returning the current <see cref="HarnessTcpServer"/>.</param>
        public static void Initialize(Func<HarnessTcpServer> serverAccessor)
        {
            _serverAccessor = serverAccessor;
        }

        /// <summary>
        /// Called when a plugin has finished activating (successfully or not).
        /// Sends a <see cref="PluginLoadedMessage"/> to the test host.
        /// </summary>
        /// <param name="plugin">The plugin instance.</param>
        /// <param name="success">Whether activation succeeded.</param>
        /// <param name="exception">The exception if activation failed, or <c>null</c>.</param>
        public static void OnPluginActivated(RocketPlugin plugin, bool success, Exception exception)
        {
            string pluginName = plugin?.GetType().Name ?? "Unknown";
            string assemblyName = plugin?.GetType().Assembly?.FullName;

            PluginStatuses[pluginName] = new PluginStatus
            {
                Name = pluginName,
                Loaded = success,
                Error = exception?.Message
            };

            SendPluginLoadedMessage(pluginName, assemblyName, success, exception?.ToString());
        }

        /// <summary>
        /// Called when a plugin fails to load (exception during activation).
        /// Sends a failure <see cref="PluginLoadedMessage"/> to the test host.
        /// </summary>
        /// <param name="plugin">The plugin instance that failed.</param>
        /// <param name="exception">The exception thrown during activation.</param>
        public static void OnPluginLoadFailed(RocketPlugin plugin, Exception exception)
        {
            string pluginName = plugin?.GetType().Name ?? "Unknown";
            string assemblyName = plugin?.GetType().Assembly?.FullName;

            PluginStatuses[pluginName] = new PluginStatus
            {
                Name = pluginName,
                Loaded = false,
                Error = exception?.Message
            };

            SendPluginLoadedMessage(pluginName, assemblyName, success: false, exception?.ToString());
        }

        /// <summary>
        /// Checks whether a plugin with the given name has been successfully loaded.
        /// </summary>
        /// <param name="name">The plugin type name to check.</param>
        /// <returns><c>true</c> if the plugin is tracked and loaded; otherwise <c>false</c>.</returns>
        public static bool IsPluginLoaded(string name)
        {
            return PluginStatuses.TryGetValue(name, out var status) && status.Loaded;
        }

        private static void SendPluginLoadedMessage(
            string pluginName, string assembly, bool success, string error)
        {
            var server = _serverAccessor?.Invoke();
            if (server == null || !server.IsClientConnected)
                return;

            var message = new PluginLoadedMessage
            {
                PluginName = pluginName,
                Assembly = assembly,
                Success = success,
                Error = error
            };

            // Fire-and-forget; errors are logged by the TCP server.
            server.SendMessageAsync(message);
        }

        /// <summary>
        /// Internal status record for a tracked plugin.
        /// </summary>
        private sealed class PluginStatus
        {
            public string Name { get; set; }
            public bool Loaded { get; set; }
            public string Error { get; set; }
        }
    }
}
