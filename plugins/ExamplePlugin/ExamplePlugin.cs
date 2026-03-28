using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace ExamplePlugin
{
    public class ExamplePlugin : RocketPlugin<ExamplePluginConfiguration>
    {
        public static ExamplePlugin? Instance { get; private set; }

        protected override void Load()
        {
            Instance = this;
            U.Events.OnPlayerConnected += OnPlayerConnected;

            Rocket.Core.Logging.Logger.Log($"ExamplePlugin loaded. Welcome message: {Configuration.Instance.WelcomeMessage}");
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            Instance = null;
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            string message = Configuration.Instance.WelcomeMessage
                .Replace("{player}", player.DisplayName);

            UnturnedChat.Say(player, message, Color.green);
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "heal_success", "Healed {0} for {1} HP." },
            { "heal_self", "You healed yourself for {0} HP." },
            { "heal_player_not_found", "Player \"{0}\" was not found." },
            { "announce_usage", "Usage: /announce <message>" }
        };
    }
}
