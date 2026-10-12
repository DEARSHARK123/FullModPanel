namespace FullModPanel.Core;

/// <summary>
/// Language of the panel's own text. Simplified Chinese and English are the only
/// languages shipped; any other game locale uses English.
/// </summary>
public enum PanelLanguage
{
    Chinese = 0,
    English = 1,
}

/// <summary>
/// Every string the panel shows or returns as an error. Chinese is the default.
/// The driver sets <see cref="Language"/> from the game locale.
/// </summary>
public static class PanelCopy
{
    private static PanelLanguage _language = PanelLanguage.Chinese;

    public static PanelLanguage Language
    {
        get => _language;
        set => _language = value;
    }

    private static string Pick(string chinese, string english) =>
        _language == PanelLanguage.English ? english : chinese;

    public static string SearchPlaceholder => Pick("搜索模组名称或工坊标题...", "Search mod name or Workshop title...");

    public static string SelectAll => Pick("全选", "Select all");

    public static string Invert => Pick("反选", "Invert");

    public static string GroupScopeHint => Pick("只作用于本组", "Only affects this group");

    public static string VisibleScopeHint => Pick("只作用于当前搜索结果", "Only affects the current search results");

    public static string ExtensionsLabel => Pick("（{0} 个扩展）", "({0} extension(s))");

    public static string ExtendsFormat => Pick("扩展自 {0}", "Extends {0}");

    public static string NoHits => Pick("未找到匹配的模组", "No matching mods found");

    public static string Loading => Pick("正在加载创意工坊信息…", "Loading Workshop info…");

    public static string LibraryGroup => Pick("依赖库", "Libraries");

    public static string GameplayGroup => Pick("角色 / 玩法 / 内容", "Characters / Gameplay / Content");

    public static string LocalOnlyGroup => Pick("皮肤", "Cosmetics");

    public static string ReasonKnownLibrary => Pick("已知依赖库", "Known library");

    public static string ReasonLooksLikeLibrary =>
        Pick("名称或 id 像依赖库，且没有内容类工坊标签", "Name or id looks like a library, and it has no content Workshop tag");

    public static string ReasonDependedUponTools =>
        Pick("有其他模组依赖它，且带有 Tools & APIs", "Other mods depend on it, and it has the Tools & APIs tag");

    public static string ReasonAffectsGameplay => Pick("affects_gameplay 未关闭", "affects_gameplay is not turned off");

    public static string ReasonNoAffectsGameplay => Pick("affects_gameplay 为 false", "affects_gameplay is false");

    public static string ModNotFound => Pick("找不到这个模组。", "Mod not found.");

    public static string MissingManifestId =>
        Pick("该模组缺少 Manifest ID，无法保存设置。", "This mod has no Manifest ID, so its settings can't be saved.");

    public static string SkippedMissingManifestId =>
        Pick("有模组缺少 manifest id，已跳过。", "Some mods have no manifest id and were skipped.");

    public static string SaveFailed => Pick("保存设置失败，请重试。", "Couldn't save settings. Please try again.");

    public static string GameSettingsNotReady =>
        Pick("游戏设置尚未就绪，请稍后重试。", "Game settings aren't ready yet. Please try again.");

    public static string InvalidModId => Pick("模组标识符无效。", "Invalid mod identifier.");

    /// <summary>Format with the mod id.</summary>
    public static string ModIdNotFound => Pick("未找到模组 {0}。", "Mod {0} not found.");
}
