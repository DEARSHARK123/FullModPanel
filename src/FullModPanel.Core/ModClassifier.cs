using System.Text;
using System.Text.RegularExpressions;

namespace FullModPanel.Core;

public sealed record ClassifiedMod(
    ModFacts Facts,
    ModGroup Group,
    ModKind Kind,
    bool SyncRequired,
    string Title,
    string Subtitle,
    int Depth,
    string? ParentKey,
    string? ExtendsTitle,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Groups mods into 依赖库 / 角色 / 玩法 / 内容 / 皮肤 and attaches extensions.
/// A library is never a parent. Overrides may replace subtype and parent, never the group.
/// </summary>
public static partial class ModClassifier
{
    private static readonly HashSet<string> KnownLibraryIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseLib",
        "RitsuLib",
        "STS2-RitsuLib",
        "ModManagerSettings",
        "ModConfig",
    };

    private static readonly string[] ContentTags =
    [
        "Characters",
        "Cards",
        "Relics",
        "Potions",
        "Monsters",
        "Events",
        "Ancients",
        "Acts",
        "Rooms",
        "Expansion",
    ];

    private const string Separators = ":：-—·~ ";

    public static IReadOnlyList<ClassifiedMod> ClassifyAll(
        IReadOnlyList<ModFacts> mods,
        IReadOnlyList<ModOverride>? overrides = null)
    {
        var byId = new Dictionary<string, ModFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            if (!string.IsNullOrWhiteSpace(mod.Id))
            {
                byId.TryAdd(mod.Id, mod);
            }
        }

        var dependents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            foreach (var dependencyId in mod.DependencyIds)
            {
                if (string.IsNullOrWhiteSpace(dependencyId)
                    || dependencyId.Equals(mod.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dependents.Add(dependencyId);
            }
        }

        var overrideById = new Dictionary<string, ModOverride>(StringComparer.OrdinalIgnoreCase);
        if (overrides is not null)
        {
            foreach (var entry in overrides)
            {
                if (!string.IsNullOrWhiteSpace(entry.Id))
                {
                    overrideById[entry.Id] = entry;
                }
            }
        }

        var groups = new Dictionary<string, ModGroup>(StringComparer.Ordinal);
        var assigned = new List<(ModFacts Facts, ModGroup Group, ModKind Kind, string Reason)>(mods.Count);
        foreach (var mod in mods)
        {
            var (group, reason) = AssignGroup(mod, dependents.Contains(mod.Id));
            groups[mod.Key] = group;
            overrideById.TryGetValue(mod.Id, out var entry);
            var kind = entry?.Subtype ?? InferKind(mod, group);
            assigned.Add((mod, group, kind, reason));
        }

        var groupById = new Dictionary<string, ModGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var (facts, group, _, _) in assigned)
        {
            if (!string.IsNullOrWhiteSpace(facts.Id))
            {
                groupById.TryAdd(facts.Id, group);
            }
        }

        var parentKeyByChild = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (facts, group, _, _) in assigned)
        {
            parentKeyByChild[facts.Key] = ResolveParentKey(facts, group, mods, byId, groupById, overrideById);
        }

        var results = new List<ClassifiedMod>(assigned.Count);
        foreach (var (facts, group, kind, reason) in assigned)
        {
            var (title, subtitle) = DisplayLines(facts);
            var rootKey = RootKey(facts.Key, parentKeyByChild);
            ModFacts? root = null;
            if (rootKey is not null)
            {
                foreach (var candidate in mods)
                {
                    if (candidate.Key == rootKey)
                    {
                        root = candidate;
                        break;
                    }
                }
            }

            var sameGroup = root is not null && groups.TryGetValue(root.Key, out var rootGroup) && rootGroup == group;
            var (rootTitle, _) = root is null ? default : DisplayLines(root);
            results.Add(new ClassifiedMod(
                facts,
                group,
                kind,
                facts.AffectsGameplay,
                title,
                subtitle,
                sameGroup ? 1 : 0,
                sameGroup ? root!.Key : null,
                root is null ? null : rootTitle,
                [reason]));
        }

        return results;
    }

    public static (string Title, string Subtitle) DisplayLines(ModFacts mod)
    {
        var manifestName = string.IsNullOrWhiteSpace(mod.Name) ? null : mod.Name.Trim();
        var fallback = string.IsNullOrWhiteSpace(mod.Id) ? manifestName ?? "" : mod.Id;
        var bigWithoutWorkshop = manifestName ?? fallback;
        if (!string.IsNullOrWhiteSpace(mod.WorkshopTitle))
        {
            return (mod.WorkshopTitle.Trim(), manifestName ?? fallback);
        }

        return (bigWithoutWorkshop, fallback);
    }

    public static bool MatchesQuery(string? query, ModFacts facts) =>
        MatchesQuery(query, facts.InGameName, facts.Id, facts.WorkshopTitle);

    public static bool MatchesQuery(string? query, string inGameName, string? id, string? workshopTitle)
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

        if (!string.IsNullOrEmpty(id) && id.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(workshopTitle)
            && workshopTitle.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static (ModGroup Group, string Reason) AssignGroup(ModFacts mod, bool dependedUpon)
    {
        if (KnownLibraryIds.Contains(mod.Id))
        {
            return (ModGroup.Library, "已知依赖库");
        }

        if (LooksLikeLibrary(mod.Id, mod.Name) && !HasContentTag(mod))
        {
            return (ModGroup.Library, "名称或 id 像依赖库，且没有内容类工坊标签");
        }

        if (dependedUpon && HasToolsAndApis(mod))
        {
            return (ModGroup.Library, "有其他模组依赖它，且带有 Tools & APIs");
        }

        return mod.AffectsGameplay
            ? (ModGroup.Gameplay, "affects_gameplay 未关闭")
            : (ModGroup.LocalOnly, "affects_gameplay 为 false");
    }

    private static ModKind InferKind(ModFacts mod, ModGroup group)
    {
        if (HasTag(mod, "Characters"))
        {
            return group == ModGroup.LocalOnly ? ModKind.Cosmetic : ModKind.Character;
        }

        if (HasTag(mod, "Cosmetics"))
        {
            return ModKind.Cosmetic;
        }

        if (HasContentTag(mod))
        {
            return ModKind.Content;
        }

        if (HasTag(mod, "Modifiers"))
        {
            return ModKind.Mechanic;
        }

        if (HasTag(mod, "QoL"))
        {
            return ModKind.Interface;
        }

        if (HasToolsAndApis(mod))
        {
            return ModKind.Tool;
        }

        return ModKind.Other;
    }

    private static string? ResolveParentKey(
        ModFacts child,
        ModGroup childGroup,
        IReadOnlyList<ModFacts> mods,
        IReadOnlyDictionary<string, ModFacts> byId,
        IReadOnlyDictionary<string, ModGroup> groupById,
        IReadOnlyDictionary<string, ModOverride> overrideById)
    {
        if (overrideById.TryGetValue(child.Id, out var entry) && entry.ParentSpecified)
        {
            return AcceptParent(child, entry.ParentId, byId, groupById)?.Key;
        }

        if (childGroup == ModGroup.Library)
        {
            return null;
        }

        foreach (var dependencyId in child.DependencyIds)
        {
            var parent = AcceptParent(child, dependencyId, byId, groupById);
            if (parent is not null)
            {
                return parent.Key;
            }
        }

        var named = LongestNameParent(child, mods, groupById);
        if (named is not null)
        {
            return named.Key;
        }

        return LongestIdParent(child, mods, groupById)?.Key;
    }

    private static ModFacts? AcceptParent(
        ModFacts child,
        string? parentId,
        IReadOnlyDictionary<string, ModFacts> byId,
        IReadOnlyDictionary<string, ModGroup> groupById)
    {
        if (string.IsNullOrWhiteSpace(parentId)
            || parentId.Equals(child.Id, StringComparison.OrdinalIgnoreCase)
            || !byId.TryGetValue(parentId, out var parent)
            || parent.Key == child.Key)
        {
            return null;
        }

        return groupById.TryGetValue(parent.Id, out var group) && group == ModGroup.Library
            ? null
            : parent;
    }

    private static ModFacts? LongestNameParent(
        ModFacts child,
        IReadOnlyList<ModFacts> mods,
        IReadOnlyDictionary<string, ModGroup> groupById)
    {
        var childTexts = CandidateTexts(child);
        ModFacts? best = null;
        var bestLength = 0;
        foreach (var parent in mods)
        {
            if (parent.Key == child.Key || IsLibrary(parent, groupById))
            {
                continue;
            }

            foreach (var raw in CandidateTexts(parent))
            {
                var prefix = Clean(raw);
                if (!QualifiesAsPrefix(prefix) || prefix.Length <= bestLength)
                {
                    continue;
                }

                if (!childTexts.Any(text => StartsWithSeparatedPrefix(Clean(text), prefix)))
                {
                    continue;
                }

                best = parent;
                bestLength = prefix.Length;
            }
        }

        return best;
    }

    private static ModFacts? LongestIdParent(
        ModFacts child,
        IReadOnlyList<ModFacts> mods,
        IReadOnlyDictionary<string, ModGroup> groupById)
    {
        ModFacts? best = null;
        var bestLength = 0;
        foreach (var parent in mods)
        {
            if (parent.Key == child.Key
                || parent.Id.Length < 4
                || parent.Id.Length <= bestLength
                || IsLibrary(parent, groupById)
                || !IsIdExtension(child.Id, parent.Id))
            {
                continue;
            }

            best = parent;
            bestLength = parent.Id.Length;
        }

        return best;
    }

    private static bool IsLibrary(ModFacts mod, IReadOnlyDictionary<string, ModGroup> groupById) =>
        groupById.TryGetValue(mod.Id, out var group) && group == ModGroup.Library;

    private static IEnumerable<string> CandidateTexts(ModFacts mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.WorkshopTitle))
        {
            yield return mod.WorkshopTitle;
        }

        yield return mod.InGameName;
    }

    private static bool StartsWithSeparatedPrefix(string child, string prefix)
    {
        if (child.Length <= prefix.Length || !child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Separators.Contains(child[prefix.Length]);
    }

    private static bool IsIdExtension(string childId, string parentId)
    {
        if (string.IsNullOrEmpty(childId)
            || childId.Length <= parentId.Length
            || !childId.StartsWith(parentId, StringComparison.Ordinal))
        {
            return false;
        }

        var next = childId[parentId.Length];
        return char.IsUpper(next) || char.IsDigit(next) || next is '-' or '_' or '|';
    }

    private static bool QualifiesAsPrefix(string cleaned)
    {
        if (cleaned.Length == 0)
        {
            return false;
        }

        var cjk = 0;
        var ascii = 0;
        foreach (var ch in cleaned)
        {
            if (IsCjk(ch))
            {
                cjk++;
            }
            else if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                ascii++;
            }
        }

        return cjk >= 2 || ascii >= 4;
    }

    private static bool IsCjk(char ch) =>
        ch is (>= '\u4E00' and <= '\u9FFF') or (>= '\u3400' and <= '\u4DBF');

    internal static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var text = value.Normalize(NormalizationForm.FormKC);
        text = ZeroWidth().Replace(text, "");
        string previous;
        do
        {
            previous = text;
            text = Wrapped().Replace(text, "");
        }
        while (!string.Equals(text, previous, StringComparison.Ordinal));

        var pipe = text.IndexOf('|');
        if (pipe >= 0)
        {
            text = text[..pipe];
        }

        return text.Trim();
    }

    private static string? RootKey(string key, IReadOnlyDictionary<string, string?> parentKeyByChild)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = key;
        string? root = null;
        while (parentKeyByChild.TryGetValue(current, out var parent) && !string.IsNullOrEmpty(parent))
        {
            if (!seen.Add(current))
            {
                return null;
            }

            root = parent;
            current = parent;
        }

        return root;
    }

    private static bool HasContentTag(ModFacts mod)
    {
        foreach (var tag in ContentTags)
        {
            if (HasTag(mod, tag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasToolsAndApis(ModFacts mod) =>
        HasTag(mod, "Tools & APIs") || HasTag(mod, "Tools &amp; APIs");

    private static bool HasTag(ModFacts mod, string expected)
    {
        foreach (var tag in mod.WorkshopTags)
        {
            foreach (var piece in SplitTag(tag))
            {
                if (piece.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static bool LooksLikeLibrary(string id, string? name) =>
        LibraryToken().IsMatch(id) || (name is not null && LibraryToken().IsMatch(name));

    [GeneratedRegex(@"[\u200B\u200C\u200D\uFEFF\u2060]", RegexOptions.CultureInvariant)]
    private static partial Regex ZeroWidth();

    [GeneratedRegex(@"【[^】]*】|（[^）]*）|\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Wrapped();

    [GeneratedRegex(@"(?i)(?:(?:^|[^A-Za-z0-9])(?:lib|library|framework)(?:[^A-Za-z0-9]|$)|(?:lib|library|framework)$)", RegexOptions.CultureInvariant)]
    private static partial Regex LibraryToken();
}
