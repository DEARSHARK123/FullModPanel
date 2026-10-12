using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace FullModPanel;

[ModInitializer(nameof(Initialize))]
public static class FullModPanelInitializer
{
    public static void Initialize()
    {
        try
        {
            ModdingScreenPatches.Apply();
            Log.Info("FullModPanel hooked NModdingScreen.");
        }
        catch (Exception ex)
        {
            Log.Error($"FullModPanel failed to start: {ex}");
        }
    }
}
