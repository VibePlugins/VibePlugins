using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace ExamplePlugin.Commands
{
    public class AnnounceCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "announce";

        public string Help => "Broadcasts a message to all players.";

        public string Syntax => "/announce <message>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "announce" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller,
                    ExamplePlugin.Instance.Translate("announce_usage"),
                    Color.red);
                return;
            }

            string message = string.Join(" ", command);
            UnturnedChat.Say(message, Color.yellow);
        }
    }
}
