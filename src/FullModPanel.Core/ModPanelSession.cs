namespace FullModPanel.Core;

public enum BulkAction
{
    SelectAll,
    Invert,
}

public sealed record RowSnapshot(
    string Key,
    bool Enabled,
    bool CanPersist,
    string Title,
    string Subtitle,
    ModGroup Group,
    bool SyncRequired,
    ModKind Kind,
    int Depth,
    string? ParentKey,
    string? ExtendsTitle,
    IReadOnlyList<string> Reasons,
    int ExtensionCount,
    int ExtensionsEnabled);

public sealed record GroupBlockSnapshot(
    ModGroup Group,
    string DisplayName,
    string SwatchHex,
    int Selected,
    int Total,
    bool BulkEnabled,
    IReadOnlyList<RowSnapshot> Rows);

public sealed record PanelSnapshot(
    string Query,
    bool BulkEnabled,
    bool NoHits,
    IReadOnlyList<GroupBlockSnapshot> Blocks);

public sealed class ModPanelSession
{
    private readonly IModEnableStore _store;
    private readonly IReadOnlyList<ModOverride> _overrides;
    private List<ModFacts> _mods;

    private ModPanelSession(
        IReadOnlyList<ModFacts> mods,
        IModEnableStore store,
        IReadOnlyList<ModOverride>? overrides,
        string query)
    {
        _mods = mods.ToList();
        _store = store;
        _overrides = overrides ?? [];
        Query = query;
    }

    public string Query { get; private set; }

    public static ModPanelSession Create(
        IReadOnlyList<ModFacts> mods,
        IModEnableStore store,
        IReadOnlyList<ModOverride>? overrides = null) =>
        new(mods, store, overrides, "");

    public void SetQuery(string? query) => Query = query ?? "";

    public PanelSnapshot Snapshot() => Build(Query);

    public string? TrySet(string key, bool enabled)
    {
        if (Find(key) is null)
        {
            return "找不到这个模组。";
        }

        var rows = Build("").Blocks.SelectMany(block => block.Rows).ToList();
        var row = rows.FirstOrDefault(candidate => candidate.Key == key);
        if (row is null)
        {
            return "找不到这个模组。";
        }

        if (!row.CanPersist)
        {
            return "该模组缺少 Manifest ID，无法保存设置。";
        }

        var targets = new List<RowSnapshot> { row };
        if (row.ExtensionCount > 0)
        {
            targets.AddRange(rows.Where(candidate => candidate.Depth > 0 && candidate.ParentKey == key));
        }

        var changes = new List<ModEnableChange>();
        foreach (var target in targets)
        {
            if (!target.CanPersist || target.Enabled == enabled)
            {
                continue;
            }

            changes.Add(new ModEnableChange(target.Key, enabled));
        }

        return Commit(changes);
    }

    public string? TryBulk(BulkAction action, ModGroup? group)
    {
        var rows = new List<RowSnapshot>();
        foreach (var block in Snapshot().Blocks)
        {
            if (group is not null && block.Group != group.Value)
            {
                continue;
            }

            rows.AddRange(block.Rows);
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var skipped = 0;
        var changes = new List<ModEnableChange>();
        foreach (var row in rows)
        {
            var next = action switch
            {
                BulkAction.SelectAll => true,
                BulkAction.Invert => !row.Enabled,
            };
            if (!row.CanPersist)
            {
                skipped++;
                continue;
            }

            if (next != row.Enabled)
            {
                changes.Add(new ModEnableChange(row.Key, next));
            }
        }

        var error = Commit(changes);
        if (error is not null)
        {
            return error;
        }

        return skipped > 0 ? "有模组缺少 manifest id，已跳过。" : null;
    }

    private PanelSnapshot Build(string query)
    {
        var classified = ModClassifier.ClassifyAll(_mods, _overrides);
        var visible = new List<ClassifiedMod>();
        foreach (var item in classified)
        {
            if (ModClassifier.MatchesQuery(query, item.Facts))
            {
                visible.Add(item);
            }
        }

        var visibleKeys = visible.Select(item => item.Facts.Key).ToHashSet(StringComparer.Ordinal);
        var placed = new List<Placed>(visible.Count);
        foreach (var item in visible)
        {
            var parentVisible = item.ParentKey is not null && visibleKeys.Contains(item.ParentKey);
            var depth = parentVisible ? item.Depth : 0;
            placed.Add(new Placed(item, depth, depth > 0 ? item.ParentKey : null));
        }

        var blocks = new List<GroupBlockSnapshot>();
        foreach (var group in ModGroupDisplay.Order)
        {
            var members = new List<Placed>();
            for (var index = 0; index < placed.Count; index++)
            {
                if (placed[index].Item.Group == group)
                {
                    members.Add(placed[index]);
                }
            }

            if (members.Count == 0)
            {
                continue;
            }

            var ordered = group == ModGroup.Library
                ? members
                    .Select((member, index) => (member, index))
                    .OrderByDescending(pair => pair.member.Item.SyncRequired)
                    .ThenBy(pair => pair.index)
                    .Select(pair => pair.member)
                    .ToList()
                : TreeOrder(members);
            var counts = new Dictionary<string, (int Count, int Enabled)>(StringComparer.Ordinal);
            foreach (var member in ordered)
            {
                if (member.Depth == 0 || member.ParentKey is null)
                {
                    continue;
                }

                counts.TryGetValue(member.ParentKey, out var current);
                counts[member.ParentKey] = (current.Count + 1, current.Enabled + (member.Item.Facts.Enabled ? 1 : 0));
            }

            var rows = new List<RowSnapshot>(ordered.Count);
            foreach (var member in ordered)
            {
                counts.TryGetValue(member.Item.Facts.Key, out var extensions);
                rows.Add(new RowSnapshot(
                    member.Item.Facts.Key,
                    member.Item.Facts.Enabled,
                    member.Item.Facts.CanPersist,
                    member.Item.Title,
                    member.Item.Subtitle,
                    member.Item.Group,
                    member.Item.SyncRequired,
                    member.Item.Kind,
                    member.Depth,
                    member.ParentKey,
                    member.Item.ExtendsTitle,
                    member.Item.Reasons,
                    extensions.Count,
                    extensions.Enabled));
            }

            var selected = 0;
            foreach (var row in rows)
            {
                if (row.Enabled)
                {
                    selected++;
                }
            }

            blocks.Add(new GroupBlockSnapshot(
                group,
                ModGroupDisplay.Name(group),
                ModGroupDisplay.ColorHex(group),
                selected,
                rows.Count,
                rows.Count > 0,
                rows));
        }

        var any = blocks.Count > 0;
        return new PanelSnapshot(query, any, !any, blocks);
    }

    private static List<Placed> TreeOrder(List<Placed> members)
    {
        var children = new Dictionary<string, List<Placed>>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (member.Depth == 0 || member.ParentKey is null)
            {
                continue;
            }

            if (!children.TryGetValue(member.ParentKey, out var list))
            {
                list = [];
                children[member.ParentKey] = list;
            }

            list.Add(member);
        }

        var ordered = new List<Placed>(members.Count);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (member.Depth > 0)
            {
                continue;
            }

            Emit(member);
        }

        foreach (var member in members)
        {
            Emit(member);
        }

        return ordered;

        void Emit(Placed member)
        {
            if (!emitted.Add(member.Item.Facts.Key))
            {
                return;
            }

            ordered.Add(member);
            if (children.TryGetValue(member.Item.Facts.Key, out var kids))
            {
                foreach (var kid in kids)
                {
                    Emit(kid);
                }
            }
        }
    }

    private string? Commit(IReadOnlyList<ModEnableChange> changes)
    {
        if (changes.Count == 0)
        {
            return null;
        }

        if (!_store.TryApply(changes, out var error))
        {
            return error ?? "保存设置失败，请重试。";
        }

        var updated = changes.ToDictionary(change => change.Key, change => change.Enabled);
        _mods = _mods.Select(mod => updated.TryGetValue(mod.Key, out var enabled) ? mod with { Enabled = enabled } : mod).ToList();
        return null;
    }

    private ModFacts? Find(string key)
    {
        foreach (var mod in _mods)
        {
            if (mod.Key == key)
            {
                return mod;
            }
        }

        return null;
    }

    private readonly record struct Placed(ClassifiedMod Item, int Depth, string? ParentKey);
}
