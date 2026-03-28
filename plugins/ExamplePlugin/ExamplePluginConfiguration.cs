using Rocket.API;

namespace ExamplePlugin
{
    public class ExamplePluginConfiguration : IRocketPluginConfiguration
    {
        public string WelcomeMessage { get; set; }
        public byte DefaultHealAmount { get; set; }

        public void LoadDefaults()
        {
            WelcomeMessage = "Welcome to the server, {player}!";
            DefaultHealAmount = 100;
        }
    }
}
