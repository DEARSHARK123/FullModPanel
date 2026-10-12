namespace FullModPanel.Core.Tests;

public class PanelSessionTests
{
    [Fact]
    public void SearchMatchesWorkshopTitleAndHidesEmptyBlocks()
    {
        var session = Session(
            Mod("Local", "本地界面", ModOrigin.ModsDirectory, affectsGameplay: false),
            Mod("WorkshopBody", "另一只", ModOrigin.SteamWorkshop, workshopTitle: "创意工坊卡牌包", affectsGameplay: true));

        session.SetQuery("卡牌包");
        var snapshot = session.Snapshot();

        Assert.False(snapshot.NoHits);
        var block = Assert.Single(snapshot.Blocks);
        Assert.Equal(ModCategory.Content, block.Category);
        var row = Assert.Single(block.Rows);
        Assert.Equal("另一只", row.InGameName);
        Assert.Equal("创意工坊卡牌包", row.WorkshopTitle);
    }

    [Fact]
    public void IdenticalWorkshopTitleIsHiddenButStillSearchable()
    {
        var session = Session(Mod("Same", "同名模组", ModOrigin.SteamWorkshop, workshopTitle: "同名模组", affectsGameplay: false, description: "界面"));
        var row = Assert.Single(Assert.Single(session.Snapshot().Blocks).Rows);
        Assert.Null(row.WorkshopTitle);

        session.SetQuery("同名");
        Assert.False(session.Snapshot().NoHits);
    }

    [Fact]
    public void NoHitsDisablesBulkActions()
    {
        var session = Session(Mod("One", "界面助手", ModOrigin.ModsDirectory, affectsGameplay: false));
        session.SetQuery("不存在的名字");
        var snapshot = session.Snapshot();

        Assert.True(snapshot.NoHits);
        Assert.False(snapshot.BulkEnabled);
        Assert.Empty(snapshot.Blocks);
        Assert.Equal("未找到匹配的模组", PanelCopy.NoHits);
        Assert.Equal("搜索模组名称或工坊标题...", PanelCopy.SearchPlaceholder);
        Assert.Equal("全选当前", PanelCopy.SelectAllVisible);
        Assert.Equal("反选当前", PanelCopy.InvertVisible);
        Assert.Equal("全选本类", PanelCopy.SelectCategory);
        Assert.Equal("反选本类", PanelCopy.InvertCategory);
        Assert.Equal("更改将在完全退出并重新启动游戏后生效。", PanelCopy.RestartNotice);
    }

    [Fact]
    public void SelectAllAndInvertStayInsideTheVisibleScope()
    {
        var store = new RecordingStore();
        var hidden = Mod("Hidden", "隐藏界面", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: false);
        var visibleOff = Mod("ShownOff", "可见界面", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: false);
        var visibleOn = Mod("ShownOn", "可见显示", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: true);
        var session = ModPanelSession.Create([hidden, visibleOff, visibleOn], store);
        session.SetQuery("可见");

        Assert.Null(session.TryBulk(BulkAction.SelectAll, category: null));
        Assert.Equal([(visibleOff.Key, true)], store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());

        store.Writes.Clear();
        Assert.Null(session.TryBulk(BulkAction.Invert, category: ModCategory.QolUi));
        Assert.Equal(
            [(visibleOff.Key, false), (visibleOn.Key, false)],
            store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());

        session.SetQuery("");
        var hiddenRow = session.Snapshot().Blocks.SelectMany(block => block.Rows).Single(row => row.Key == hidden.Key);
        Assert.False(hiddenRow.Enabled);
    }

    [Fact]
    public void CategoryCountsFollowTheCurrentFilter()
    {
        var session = Session(
            Mod("A", "界面甲", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: true),
            Mod("B", "界面乙", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: false),
            Mod("C", "卡牌丙", ModOrigin.ModsDirectory, affectsGameplay: true, enabled: true));
        session.SetQuery("界面");
        var block = Assert.Single(session.Snapshot().Blocks);

        Assert.Equal("体验优化 · QoL", block.DisplayName);
        Assert.Equal(1, block.Selected);
        Assert.Equal(2, block.Total);
        Assert.True(block.BulkEnabled);
    }

    [Fact]
    public void FailedSaveDoesNotChangeLocalState()
    {
        var store = new RecordingStore { Fail = true };
        var mod = Mod("A", "界面甲", ModOrigin.ModsDirectory, affectsGameplay: false, enabled: false);
        var session = ModPanelSession.Create([mod], store);

        Assert.Equal("磁盘拒绝", session.TrySet(mod.Key, true));
        Assert.False(Assert.Single(Assert.Single(session.Snapshot().Blocks).Rows).Enabled);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void UnknownBucketIsRenderedWhenPresentAndOmittedWhenFilteredOut()
    {
        var session = Session(
            Mod("Mystery", "Mystery", ModOrigin.ModsDirectory),
            Mod("Ui", "界面", ModOrigin.ModsDirectory, affectsGameplay: false));
        var all = session.Snapshot();
        Assert.Equal(ModCategory.Unknown, all.Blocks[^1].Category);
        Assert.Equal("未分类", all.Blocks[^1].DisplayName);

        session.SetQuery("界面");
        Assert.DoesNotContain(session.Snapshot().Blocks, block => block.Category == ModCategory.Unknown);
    }

    [Fact]
    public void ModKeysRoundTripIdsThatContainColons()
    {
        var key = ModKeys.Create(ModOrigin.SteamWorkshop, "author:pack");
        Assert.True(ModKeys.TryParse(key, out var origin, out var id));
        Assert.Equal(ModOrigin.SteamWorkshop, origin);
        Assert.Equal("author:pack", id);
    }

    private static ModPanelSession Session(params ModFacts[] mods) =>
        ModPanelSession.Create(mods, new RecordingStore());

    private static ModFacts Mod(
        string id,
        string name,
        ModOrigin origin,
        bool affectsGameplay = true,
        bool enabled = false,
        string? workshopTitle = null,
        string? description = null) => new()
    {
        Key = ModKeys.Create(origin, id),
        Id = id,
        Origin = origin,
        Name = name,
        Description = description ?? name,
        AffectsGameplay = affectsGameplay,
        Enabled = enabled,
        WorkshopTitle = workshopTitle,
        CanPersist = true,
    };

    private sealed class RecordingStore : IModEnableStore
    {
        public List<ModEnableChange> Writes { get; } = [];

        public bool Fail { get; set; }

        public bool TryApply(IReadOnlyList<ModEnableChange> changes, out string? error)
        {
            if (Fail)
            {
                error = "磁盘拒绝";
                return false;
            }

            Writes.AddRange(changes);
            error = null;
            return true;
        }
    }
}
