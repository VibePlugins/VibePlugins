using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using UnityEngine;

namespace ExamplePlugin.Commands
{
    public class HealCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "heal";

        public string Help => "Heals a target player or yourself.";

        public string Syntax => "/heal [player] [amount]";

        public List<string> Aliases => new List<string> { "h" };

        public List<string> Permissions => new List<string> { "heal" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            byte amount = ExamplePlugin.Instance.Configuration.Instance.DefaultHealAmount;
            UnturnedPlayer target = null;

            if (command.Length >= 1)
            {
                target = UnturnedPlayer.FromName(command[0]);

                if (target == null)
                {
                    UnturnedChat.Say(caller,
                        ExamplePlugin.Instance.Translate("heal_player_not_found", command[0]),
                        Color.red);
                    return;
                }
            }

            if (command.Length >= 2 && byte.TryParse(command[1], out byte parsedAmount))
            {
                amount = parsedAmount;
            }

            if (target != null)
            {
                target.Heal(amount);
                UnturnedChat.Say(caller,
                    ExamplePlugin.Instance.Translate("heal_success", target.DisplayName, amount),
                    Color.green);
            }
            else
            {
                // Heal self
                UnturnedPlayer callerPlayer = caller as UnturnedPlayer;
                if (callerPlayer == null)
                {
                    UnturnedChat.Say(caller, "You must specify a player when running from console.", Color.red);
                    return;
                }

                callerPlayer.Heal(amount);
                UnturnedChat.Say(caller,
                    ExamplePlugin.Instance.Translate("heal_self", amount),
                    Color.green);
            }
        }
    }
}
