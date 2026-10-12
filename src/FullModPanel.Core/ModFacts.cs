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

    public const string SelectAll = "全选";

    public const string Invert = "反选";

    public const string GroupScopeHint = "只作用于本组";

    public const string VisibleScopeHint = "只作用于当前搜索结果";

    public const string ExtensionsLabel = "{0} 个扩展";

    public const string ExtendsFormat = "扩展自 {0}";

    public const string NoHits = "未找到匹配的模组";
}

/// <summary>
/// A user override for one manifest id. Group is never overridable.
/// <see cref="ParentSpecified"/> with a null <see cref="ParentId"/> clears attachment
/// and does not fall through to dependencies or prefixes.
/// </summary>
public sealed record ModOverride(string Id, bool ParentSpecified, string? ParentId, ModKind? Subtype);
