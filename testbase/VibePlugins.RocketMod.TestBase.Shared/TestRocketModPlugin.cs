using System;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;

namespace VibePlugins.RocketMod.TestBase.Shared
{
    /// <summary>
    /// A default RocketMod plugin used by the test framework when no custom plugin is required.
    /// This provides a minimal plugin context for executing commands and running test code on the server.
    /// </summary>
    public class TestRocketModPlugin : RocketPlugin<TestRocketModPluginConfiguration>
    {
        /// <summary>
        /// Gets the singleton instance of the test plugin, set during <see cref="Load"/>.
        /// </summary>
        public static TestRocketModPlugin Instance { get; private set; }

        /// <summary>
        /// Called by RocketMod when the plugin is loaded.
        /// </summary>
        protected override void Load()
        {
            Instance = this;
            Logger.Log($"[TestRocketModPlugin] Loaded (v{Assembly.GetName().Version})");
        }

        /// <summary>
        /// Called by RocketMod when the plugin is unloaded.
        /// </summary>
        protected override void Unload()
        {
            Logger.Log("[TestRocketModPlugin] Unloaded");
            Instance = null;
        }

        /// <summary>
        /// Returns the default translations for this plugin. No translations are needed.
        /// </summary>
        public override TranslationList DefaultTranslations => new TranslationList();
    }

    /// <summary>
    /// Configuration class for <see cref="TestRocketModPlugin"/>.
    /// Contains no configuration values by default.
    /// </summary>
    public class TestRocketModPluginConfiguration : IRocketPluginConfiguration
    {
        /// <summary>
        /// Loads default configuration values. No-op for the test plugin.
        /// </summary>
        public void LoadDefaults()
        {
            // No configuration required for the test plugin.
        }
    }
}
