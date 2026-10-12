namespace FullModPanel.Core;

public sealed record ModFacts
{
    public required string Key { get; init; }

    public required string Id { get; init; }

    public required ModOrigin Origin { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public bool AffectsGameplay { get; init; } = true;

    public IReadOnlyList<string> DependencyIds { get; init; } = [];

    public string? WorkshopTitle { get; init; }

    public IReadOnlyList<string> WorkshopTags { get; init; } = [];

    public ulong? WorkshopId { get; init; }

    public bool Enabled { get; init; }

    /// <summary>
    /// False when the manifest has no id. The row is still shown; toggles cannot be written.
    /// </summary>
    public bool CanPersist { get; init; } = true;

    public string InGameName => string.IsNullOrWhiteSpace(Name) ? Id : Name.Trim();
}

public readonly record struct ModEnableChange(string Key, bool Enabled);

public interface IModEnableStore
{
    /// <summary>
    /// Persist every change, or leave storage untouched and return false.
    /// </summary>
    bool TryApply(IReadOnlyList<ModEnableChange> changes, out string? error);
}

public static class PanelCopy
{
    public const string SearchPlaceholder = "搜索模组名称或工坊标题...";

    public const string SelectAllVisible = "全选当前";

    public const string InvertVisible = "反选当前";

    public const string SelectCategory = "全选本类";

    public const string InvertCategory = "反选本类";

    public const string NoHits = "未找到匹配的模组";

    public const string RestartNotice = "更改将在完全退出并重新启动游戏后生效。";
}
