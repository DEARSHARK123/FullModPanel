using FullModPanel.Core;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;

namespace FullModPanel;

/// <summary>
/// Reads the game's locale and maps it to the panel's language. Simplified Chinese
/// (<c>zhs</c>) gets Chinese; every other locale, or one that cannot be read, gets English.
/// </summary>
internal static class Sts2Language
{
    private const string SimplifiedChinese = "zhs";

    public static PanelLanguage Current()
    {
        try
        {
            return Map(LocManager.Instance?.Language);
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not read the game language: {ex.Message}");
            return PanelLanguage.English;
        }
    }

    public static PanelLanguage Map(string? code) =>
        string.Equals(code, SimplifiedChinese, StringComparison.OrdinalIgnoreCase)
            ? PanelLanguage.Chinese
            : PanelLanguage.English;

    /// <summary>Subscribes to locale changes. Keep the returned delegate for <see cref="Unsubscribe"/>.</summary>
    public static LocManager.LocaleChangeCallback? Subscribe(Action onChange)
    {
        try
        {
            var manager = LocManager.Instance;
            if (manager is null)
            {
                return null;
            }

            LocManager.LocaleChangeCallback callback = () => onChange();
            manager.SubscribeToLocaleChange(callback);
            return callback;
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not subscribe to locale changes: {ex.Message}");
            return null;
        }
    }

    public static void Unsubscribe(LocManager.LocaleChangeCallback? callback)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            LocManager.Instance?.UnsubscribeToLocaleChange(callback);
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not unsubscribe from locale changes: {ex.Message}");
        }
    }
}
