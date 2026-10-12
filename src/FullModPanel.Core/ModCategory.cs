namespace FullModPanel.Core;

/// <summary>
/// Stable category set. Display names are the contract: add a member later, never rename an existing label.
/// </summary>
public enum ModCategory
{
    DependencyLibrary = 0,
    Content = 1,
    QolUi = 2,
    Balance = 3,
    DebugTool = 4,
    Unknown = 5,
}

public enum ModOrigin
{
    Unknown = 0,
    ModsDirectory = 1,
    SteamWorkshop = 2,
}

public static class ModCategoryDisplay
{
    public static IReadOnlyList<ModCategory> Order { get; } =
    [
        ModCategory.DependencyLibrary,
        ModCategory.Content,
        ModCategory.QolUi,
        ModCategory.Balance,
        ModCategory.DebugTool,
        ModCategory.Unknown,
    ];

    public static string Name(ModCategory category) => category switch
    {
        ModCategory.DependencyLibrary => "前置 / API",
        ModCategory.Content => "角色与扩展",
        ModCategory.QolUi => "体验优化 · QoL",
        ModCategory.Balance => "玩法机制 · 调整",
        ModCategory.DebugTool => "开发与调试",
        ModCategory.Unknown => "未分类",
    };

    public static string ColorHex(ModCategory category) => category switch
    {
        ModCategory.DependencyLibrary => "#7EB6FF",
        ModCategory.Content => "#F0C14A",
        ModCategory.QolUi => "#6FCFC4",
        ModCategory.Balance => "#F0997B",
        ModCategory.DebugTool => "#C9A0FF",
        ModCategory.Unknown => "#A8A8A8",
    };
}

public static class ModKeys
{
    public static string Create(ModOrigin origin, string id) => $"{(int)origin}:{id}";

    public static bool TryParse(string key, out ModOrigin origin, out string id)
    {
        origin = ModOrigin.Unknown;
        id = "";
        var split = key.IndexOf(':');
        if (split <= 0 || split == key.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(key[..split], out var raw) || !Enum.IsDefined(typeof(ModOrigin), raw))
        {
            return false;
        }

        origin = (ModOrigin)raw;
        id = key[(split + 1)..];
        return id.Length > 0;
    }
}
