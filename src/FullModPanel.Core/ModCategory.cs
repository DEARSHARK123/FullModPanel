namespace FullModPanel.Core;

/// <summary>
/// Top-level list groups. Display names are the contract.
/// </summary>
public enum ModGroup
{
    Library = 0,
    Gameplay = 1,
    LocalOnly = 2,
}

/// <summary>
/// Optional subtype stored in overrides. The list does not draw it.
/// </summary>
public enum ModKind
{
    Character = 0,
    Content = 1,
    Mechanic = 2,
    Cosmetic = 3,
    Interface = 4,
    Tool = 5,
    Other = 6,
}

public enum ModOrigin
{
    Unknown = 0,
    ModsDirectory = 1,
    SteamWorkshop = 2,
}

public static class ModGroupDisplay
{
    public static IReadOnlyList<ModGroup> Order { get; } =
    [
        ModGroup.Library,
        ModGroup.Gameplay,
        ModGroup.LocalOnly,
    ];

    public static string Name(ModGroup group) => group switch
    {
        ModGroup.Library => PanelCopy.LibraryGroup,
        ModGroup.Gameplay => PanelCopy.GameplayGroup,
        ModGroup.LocalOnly => PanelCopy.LocalOnlyGroup,
    };

    public static string ColorHex(ModGroup group) => group switch
    {
        ModGroup.Library => "#7EB6FF",
        ModGroup.Gameplay => "#F0C14A",
        ModGroup.LocalOnly => "#6FCFC4",
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
