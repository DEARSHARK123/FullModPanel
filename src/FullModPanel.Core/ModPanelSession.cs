namespace FullModPanel.Core;

public enum BulkAction
{
    SelectAll,
    Invert,
}

public sealed record RowSnapshot(string Key, bool Enabled, bool CanPersist, string InGameName, string? WorkshopTitle, string SwatchHex);

public sealed record CategoryBlockSnapshot(
    ModCategory Category,
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
    IReadOnlyList<CategoryBlockSnapshot> Blocks);

public sealed class ModPanelSession
{
    private readonly IModEnableStore _store;
    private List<ModFacts> _mods;

    private ModPanelSession(IReadOnlyList<ModFacts> mods, IModEnableStore store, string query)
    {
        _mods = mods.ToList();
        _store = store;
        Query = query;
    }

    public string Query { get; private set; }

    public static ModPanelSession Create(IReadOnlyList<ModFacts> mods, IModEnableStore store) =>
        new(mods, store, "");

    public void SetQuery(string? query) => Query = query ?? "";

    public PanelSnapshot Snapshot()
    {
        var classified = ModClassifier.ClassifyAll(_mods);
        var visible = new List<(ClassifiedMod Mod, string? Workshop)>();
        foreach (var item in classified)
        {
            var workshop = ModClassifier.SecondaryWorkshopName(item.Facts.InGameName, item.Facts.WorkshopTitle);
            if (!ModClassifier.MatchesQuery(Query, item.Facts.InGameName, item.Facts.WorkshopTitle))
            {
                continue;
            }

            visible.Add((item, workshop));
        }

        var blocks = new List<CategoryBlockSnapshot>();
        foreach (var category in ModCategoryDisplay.Order)
        {
            var rows = new List<RowSnapshot>();
            foreach (var (mod, workshop) in visible)
            {
                if (mod.Category != category)
                {
                    continue;
                }

                rows.Add(new RowSnapshot(
                    mod.Facts.Key,
                    mod.Facts.Enabled,
                    mod.Facts.CanPersist,
                    mod.Facts.InGameName,
                    workshop,
                    ModCategoryDisplay.ColorHex(category)));
            }

            if (rows.Count == 0)
            {
                continue;
            }

            var selected = 0;
            foreach (var row in rows)
            {
                if (row.Enabled)
                {
                    selected++;
                }
            }

            blocks.Add(new CategoryBlockSnapshot(
                category,
                ModCategoryDisplay.Name(category),
                ModCategoryDisplay.ColorHex(category),
                selected,
                rows.Count,
                rows.Count > 0,
                rows));
        }

        var any = blocks.Count > 0;
        return new PanelSnapshot(Query, any, !any, blocks);
    }

    public string? TrySet(string key, bool enabled)
    {
        var mod = Find(key);
        if (mod is null)
        {
            return "找不到这个模组。";
        }

        if (!mod.CanPersist)
        {
            return "该模组缺少 Manifest ID，无法保存设置。";
        }

        if (mod.Enabled == enabled)
        {
            return null;
        }

        return Commit([new ModEnableChange(key, enabled)]);
    }

    public string? TryBulk(BulkAction action, ModCategory? category)
    {
        var snapshot = Snapshot();
        var rows = new List<RowSnapshot>();
        foreach (var block in snapshot.Blocks)
        {
            if (category is not null && block.Category != category.Value)
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
}
