using System.Text.RegularExpressions;

namespace FullModPanel.Core;

public sealed record ClassifiedMod(ModFacts Facts, ModCategory Category);

/// <summary>
/// Classifies STS2 mods. Low confidence and conflicting signals become <see cref="ModCategory.Unknown"/>.
/// Nothing is dropped: <see cref="ClassifyAll"/> returns one result per input.
/// </summary>
public static partial class ModClassifier
{
    private static readonly HashSet<string> KnownLibraryIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseLib",
        "RitsuLib",
        "ModManagerSettings",
        "ModConfig",
    };

    [Flags]
    private enum Signal
    {
        None = 0,
        Content = 1,
        Qol = 2,
        Balance = 4,
        Debug = 8,
    }

    private readonly record struct Term(Signal Signal, string Text, bool AsciiWord, bool Weak);

    private static readonly Term[] Terms =
    [
        new(Signal.Content, "卡牌", false, false),
        new(Signal.Content, "角色", false, false),
        new(Signal.Content, "遗物", false, false),
        new(Signal.Content, "药水", false, false),
        new(Signal.Content, "敌人", false, false),
        new(Signal.Content, "事件", false, false),
        new(Signal.Content, "character", true, false),
        new(Signal.Content, "relic", true, false),
        new(Signal.Content, "potion", true, false),
        new(Signal.Content, "encounter", true, false),
        new(Signal.Content, "enemy", true, false),
        new(Signal.Content, "card", true, false),
        new(Signal.Qol, "界面", false, false),
        new(Signal.Qol, "显示", false, false),
        new(Signal.Qol, "外观", false, false),
        new(Signal.Qol, "qol", true, false),
        new(Signal.Qol, "hud", true, false),
        new(Signal.Qol, "tooltip", true, false),
        new(Signal.Qol, "cosmetic", true, false),
        new(Signal.Qol, "ui", true, false),
        new(Signal.Qol, "quality of life", false, false),
        new(Signal.Balance, "balance", true, false),
        new(Signal.Balance, "平衡", false, false),
        new(Signal.Balance, "削弱", false, false),
        new(Signal.Balance, "加强", false, false),
        new(Signal.Balance, "nerf", true, false),
        new(Signal.Balance, "buff", true, false),
        new(Signal.Balance, "rebalance", true, false),
        new(Signal.Balance, "rework", true, false),
        new(Signal.Debug, "调试", false, false),
        new(Signal.Debug, "debug", true, false),
        new(Signal.Debug, "console", true, false),
        new(Signal.Debug, "logger", true, false),
        new(Signal.Debug, "inspector", true, false),
        new(Signal.Debug, "工具", false, true),
        new(Signal.Debug, "tool", true, true),
        new(Signal.Debug, "utility", true, true),
    ];

    public static IReadOnlyList<ClassifiedMod> ClassifyAll(IReadOnlyList<ModFacts> mods)
    {
        var dependents = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            foreach (var dependencyId in mod.DependencyIds)
            {
                if (string.IsNullOrWhiteSpace(dependencyId))
                {
                    continue;
                }

                if (string.Equals(dependencyId, mod.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dependents.TryGetValue(dependencyId, out var count);
                dependents[dependencyId] = count + 1;
            }
        }

        var classified = new List<ClassifiedMod>(mods.Count);
        foreach (var mod in mods)
        {
            dependents.TryGetValue(mod.Id, out var dependedUponBy);
            classified.Add(new ClassifiedMod(mod, Classify(mod, dependedUponBy)));
        }

        return classified;
    }

    public static ModCategory Classify(ModFacts mod, int dependedUponBy)
    {
        if (KnownLibraryIds.Contains(mod.Id))
        {
            return ModCategory.DependencyLibrary;
        }

        var text = Join(mod.Name, mod.Description);
        var strong = Collect(text, includeWeak: false);
        var libraryLike = LooksLikeLibrary(mod.Id, mod.Name, mod.Description);
        if (libraryLike && strong != Signal.None)
        {
            return ModCategory.Unknown;
        }

        if (libraryLike)
        {
            return ModCategory.DependencyLibrary;
        }

        var contentOrBalance = (strong & (Signal.Content | Signal.Balance)) != 0;
        if (dependedUponBy >= 2 && contentOrBalance)
        {
            return ModCategory.Unknown;
        }

        if (dependedUponBy >= 2)
        {
            return ModCategory.DependencyLibrary;
        }

        var named = Decide(Collect(text, includeWeak: !mod.AffectsGameplay), mod.AffectsGameplay);
        if (named == Decision.Conflict)
        {
            return ModCategory.Unknown;
        }

        if (named != Decision.None)
        {
            return named.Category;
        }

        var fromWorkshop = WorkshopDecision(mod);
        return fromWorkshop == Decision.None || fromWorkshop == Decision.Conflict
            ? ModCategory.Unknown
            : fromWorkshop.Category;
    }

    public static string? SecondaryWorkshopName(string inGameName, string? workshopTitle)
    {
        if (string.IsNullOrWhiteSpace(workshopTitle))
        {
            return null;
        }

        var trimmed = workshopTitle.Trim();
        return string.Equals(trimmed, inGameName.Trim(), StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    public static bool MatchesQuery(string? query, string inGameName, string? workshopTitle)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var needle = query.Trim();
        if (inGameName.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(workshopTitle)
            && workshopTitle.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static Decision WorkshopDecision(ModFacts mod)
    {
        var tagSignal = Signal.None;
        var libraryTag = false;
        foreach (var tag in mod.WorkshopTags)
        {
            foreach (var piece in SplitTag(tag))
            {
                if (IsLibraryTag(piece))
                {
                    libraryTag = true;
                    continue;
                }

                tagSignal |= Collect(piece, includeWeak: true);
            }
        }

        var titleSignal = Collect(mod.WorkshopTitle, includeWeak: false);
        if (tagSignal != Signal.None && titleSignal != Signal.None && tagSignal != titleSignal)
        {
            return Decision.Conflict;
        }

        var combined = tagSignal | titleSignal;
        if (libraryTag && combined != Signal.None)
        {
            return Decision.Conflict;
        }

        if (libraryTag)
        {
            return Decision.Of(ModCategory.DependencyLibrary);
        }

        if (combined == Signal.None)
        {
            return Decision.None;
        }

        return Decide(combined, mod.AffectsGameplay);
    }

    private static IEnumerable<string> SplitTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            yield break;
        }

        foreach (var piece in tag.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return piece;
        }
    }

    private static bool IsLibraryTag(string tag) =>
        tag.Equals("library", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("framework", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("dependency", StringComparison.OrdinalIgnoreCase)
        || tag.Equals("依赖库", StringComparison.Ordinal);

    private readonly record struct Decision(ModCategory Category, bool IsSet, bool IsConflict)
    {
        public static Decision None => default;

        public static Decision Conflict => new(ModCategory.Unknown, false, true);

        public static Decision Of(ModCategory category) => new(category, true, false);
    }

    private static Decision Decide(Signal signal, bool affectsGameplay)
    {
        if (signal == Signal.None)
        {
            return Decision.None;
        }

        if (!ExactlyOne(signal))
        {
            return Decision.Conflict;
        }

        if ((signal & Signal.Content) != 0)
        {
            return affectsGameplay ? Decision.Of(ModCategory.Content) : Decision.Conflict;
        }

        if ((signal & Signal.Balance) != 0)
        {
            return affectsGameplay ? Decision.Of(ModCategory.Balance) : Decision.Conflict;
        }

        if ((signal & Signal.Qol) != 0)
        {
            return affectsGameplay ? Decision.Conflict : Decision.Of(ModCategory.QolUi);
        }

        if ((signal & Signal.Debug) != 0)
        {
            return Decision.Of(ModCategory.DebugTool);
        }

        return Decision.None;
    }

    private static bool ExactlyOne(Signal signal)
    {
        var value = (int)signal;
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static Signal Collect(string? text, bool includeWeak)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Signal.None;
        }

        var found = Signal.None;
        foreach (var term in Terms)
        {
            if (term.Weak && !includeWeak)
            {
                continue;
            }

            var hit = term.AsciiWord
                ? ContainsAsciiWord(text, term.Text)
                : text.Contains(term.Text, StringComparison.OrdinalIgnoreCase);
            if (hit)
            {
                found |= term.Signal;
            }
        }

        return found;
    }

    private static bool ContainsAsciiWord(string text, string word) =>
        Regex.IsMatch(text, $@"(?<![A-Za-z0-9]){Regex.Escape(word)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool LooksLikeLibrary(string id, string? name, string? description)
    {
        if (LibraryToken().IsMatch(id) || (name is not null && LibraryToken().IsMatch(name)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Contains("依赖库", StringComparison.Ordinal)
            || description.Contains("library", StringComparison.OrdinalIgnoreCase)
            || description.Contains("framework", StringComparison.OrdinalIgnoreCase)
            || description.Contains("modding api", StringComparison.OrdinalIgnoreCase);
    }

    private static string Join(string? name, string? description) => $"{name}\n{description}";

    [GeneratedRegex(@"(?i)(?:(?:^|[^A-Za-z0-9])(?:lib|library|framework)(?:[^A-Za-z0-9]|$)|(?:lib|library|framework)$)", RegexOptions.CultureInvariant)]
    private static partial Regex LibraryToken();
}
