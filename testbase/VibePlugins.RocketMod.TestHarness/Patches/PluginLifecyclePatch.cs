using System;
using HarmonyLib;
using Rocket.Core.Plugins;

namespace VibePlugins.RocketMod.TestHarness.Patches
{
    /// <summary>
    /// Harmony patches on <c>RocketPlugin.Load</c> and <c>RocketPlugin.Unload</c> that
    /// track plugin lifecycle events and report them to the test host via TCP.
    /// </summary>
    [HarmonyPatch]
    internal static class PluginLifecyclePatch
    {
        /// <summary>
        /// Targets the internal <c>loadPlugin</c> method on <c>RocketPluginManager</c>,
        /// or alternatively the <c>Load</c> override on any <c>RocketPlugin</c>.
        /// We use a manual patch targeting the non-generic base.
        /// </summary>
        [HarmonyPatch(typeof(RocketPlugin), "Load")]
        [HarmonyPostfix]
        static void LoadPostfix(RocketPlugin __instance)
        {
            if (__instance == null)
                return;

            // Do not track the harness itself.
            string typeName = __instance.GetType().Name;
            if (typeName == "TestHarnessPlugin")
                return;

            try
            {
                PluginTracker.OnPluginActivated(__instance, success: true, exception: null);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogException(ex,
                    "[TestHarness] Error in plugin lifecycle postfix");
            }
        }

        /// <summary>
        /// Finalizer that catches exceptions thrown during plugin loading and
        /// reports them to the <see cref="PluginTracker"/>.
        /// </summary>
        [HarmonyPatch(typeof(RocketPlugin), "Load")]
        [HarmonyFinalizer]
        static Exception LoadFinalizer(RocketPlugin __instance, Exception __exception)
        {
            if (__exception != null && __instance != null)
            {
                string typeName = __instance.GetType().Name;
                if (typeName != "TestHarnessPlugin")
                {
                    PluginTracker.OnPluginLoadFailed(__instance, __exception);
                }
            }

            // Return the original exception unchanged.
            return __exception;
        }
    }
}
