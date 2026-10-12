using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace FullModPanel;

internal static class ModdingScreenPatches
{
    public static void Apply()
    {
        var harmony = new Harmony("fullmodpanel.selector");
        Patch(harmony, "_Ready", nameof(AfterReady));
        Patch(harmony, "OnNewModDetected", nameof(AfterDetected));
        Patch(harmony, nameof(NModdingScreen.OnSubmenuOpened), nameof(AfterOpened));
        Patch(harmony, nameof(NModdingScreen.OnModEnabledOrDisabled), nameof(AfterEnabled));
        Patch(harmony, nameof(NModdingScreen.OnRowSelected), nameof(AfterRowSelected));
    }

    public static void AfterReady(NModdingScreen __instance) => ModSelectorInstaller.TryInstall(__instance);

    public static void AfterDetected(NModdingScreen __instance) =>
        __instance.GetNodeOrNull<VanillaModListDriver>("FullModPanelDriver")?.Relayout();

    public static void AfterOpened(NModdingScreen __instance) => ModSelectorInstaller.TryInstall(__instance);

    public static void AfterEnabled(NModdingScreen __instance) =>
        __instance.GetNodeOrNull<VanillaModListDriver>("FullModPanelDriver")?.Relayout();

    public static void AfterRowSelected(NModdingScreen __instance, NModMenuRow row) =>
        VanillaModListDriver.NoteSelection(__instance, row);

    private static void Patch(Harmony harmony, string methodName, string postfixName)
    {
        var method = AccessTools.Method(typeof(NModdingScreen), methodName);
        var postfix = AccessTools.Method(typeof(ModdingScreenPatches), postfixName);
        if (method is null || postfix is null)
        {
            Log.Error($"FullModPanel could not find NModdingScreen.{methodName}. The vanilla mod list stays in place.");
            return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
    }
}
