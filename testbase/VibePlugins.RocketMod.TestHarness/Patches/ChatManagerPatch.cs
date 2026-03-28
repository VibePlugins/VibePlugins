using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using VibePlugins.RocketMod.TestHarness.Execution;

namespace VibePlugins.RocketMod.TestHarness.Patches
{
    /// <summary>
    /// Harmony postfix patch on <see cref="ChatManager.serverSendMessage"/> that captures
    /// every outgoing chat message for event monitoring and command-execution capture.
    /// </summary>
    [HarmonyPatch(typeof(SDG.Unturned.ChatManager))]
    [HarmonyPatch("serverSendMessage")]
    internal static class ChatManagerPatch
    {
        /// <summary>
        /// Postfix that records the chat message details into <see cref="EventCapture"/>.
        /// </summary>
        /// <param name="text">The chat message text.</param>
        /// <param name="color">The message color.</param>
        /// <param name="fromPlayer">The sender, or <c>null</c> for server messages.</param>
        /// <param name="toPlayer">The recipient, or <c>null</c> for broadcast.</param>
        /// <param name="mode">The chat mode (Global, Local, Group, etc.).</param>
        /// <param name="iconURL">Icon URL associated with the message.</param>
        /// <param name="useRichTextFormatting">Whether the message uses rich text.</param>
        [HarmonyPostfix]
        static void Postfix(
            string text,
            Color color,
            SteamPlayer fromPlayer,
            SteamPlayer toPlayer,
            EChatMode mode,
            string iconURL,
            bool useRichTextFormatting)
        {
            var info = new ChatMessageInfo
            {
                Text = text,
                Color = ColorToHex(color),
                FromPlayerName = fromPlayer?.playerID?.playerName,
                ToPlayerName = toPlayer?.playerID?.playerName,
                Mode = mode.ToString()
            };

            EventCapture.RecordChat(info);
        }

        /// <summary>
        /// Converts a Unity <see cref="Color"/> to a hex string (e.g. "#FF00FF").
        /// </summary>
        private static string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
        }
    }
}
