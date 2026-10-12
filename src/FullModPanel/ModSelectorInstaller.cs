using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace FullModPanel;

internal static class ModSelectorInstaller
{
    public static void TryInstall(NModdingScreen screen)
    {
        try
        {
            var driver = screen.GetNodeOrNull<VanillaModListDriver>("FullModPanelDriver");
            if (driver is null)
            {
                driver = new VanillaModListDriver { Name = "FullModPanelDriver" };
                screen.AddChild(driver);
            }

            driver.Install(screen);
        }
        catch (Exception ex)
        {
            Log.Error($"FullModPanel failed to install into NModdingScreen: {ex}");
        }
    }
}
