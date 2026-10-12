namespace FullModPanel.Core.Tests;

public class PanelSessionTests
{
    [Fact]
    public void BigLineIsTheWorkshopTitleAndTheSmallLineStaysWhenIdentical()
    {
        var session = Session(
            Mod("Local", "本地界面", affectsGameplay: false),
            Mod("WorkshopBody", "另一只", workshopTitle: "创意工坊卡牌包", tags: ["Cards"]));
        session.SetQuery("卡牌包");
        var snapshot = session.Snapshot();

        Assert.False(snapshot.NoHits);
        var block = Assert.Single(snapshot.Blocks);
        Assert.Equal(ModGroup.Gameplay, block.Group);
        var row = Assert.Single(block.Rows);
        Assert.Equal("创意工坊卡牌包", row.Title);
        Assert.Equal("另一只", row.Subtitle);

        var same = Session(Mod("Same", "同名模组", workshopTitle: "同名模组", affectsGameplay: false));
        var sameRow = Assert.Single(Assert.Single(same.Snapshot().Blocks).Rows);
        Assert.Equal("同名模组", sameRow.Title);
        Assert.Equal("同名模组", sameRow.Subtitle);
        same.SetQuery("同名");
        Assert.False(same.Snapshot().NoHits);
    }

    [Fact]
    public void SearchUsesVisibleLinesAndDropsDescriptionAndHiddenIdHits()
    {
        var session = Session(Mod("HextechRunesSponsorPack", "清单名", workshopTitle: "工坊标题", description: "稀有词"));
        session.SetQuery("稀有词");
        Assert.True(session.Snapshot().NoHits);

        // The id is hidden once a workshop title is shown.
        session.SetQuery("Sponsor");
        Assert.True(session.Snapshot().NoHits);

        session.SetQuery("工坊");
        var row = Assert.Single(Assert.Single(session.Snapshot().Blocks).Rows);
        Assert.Equal("HextechRunesSponsorPack", row.Key.Split(':', 2)[1]);
        Assert.Equal("搜索模组名称或工坊标题...", PanelCopy.SearchPlaceholder);
        Assert.Equal("全选", PanelCopy.SelectAll);
        Assert.Equal("反选", PanelCopy.Invert);
        Assert.Equal("只作用于本组", PanelCopy.GroupScopeHint);
        Assert.Equal("只作用于当前搜索结果", PanelCopy.VisibleScopeHint);
        Assert.Equal("（{0} 个扩展）", PanelCopy.ExtensionsLabel);
        Assert.Equal("扩展自 {0}", PanelCopy.ExtendsFormat);
        Assert.Equal("未找到匹配的模组", PanelCopy.NoHits);
    }

    [Fact]
    public void NoHitsDisablesBulkActions()
    {
        var session = Session(Mod("One", "界面助手", affectsGameplay: false));
        session.SetQuery("不存在的名字");
        var snapshot = session.Snapshot();

        Assert.True(snapshot.NoHits);
        Assert.False(snapshot.BulkEnabled);
        Assert.Empty(snapshot.Blocks);
    }

    [Fact]
    public void SelectAllAndInvertStayInsideTheVisibleScopeAndFlip()
    {
        var store = new RecordingStore();
        var hidden = Mod("Hidden", "隐藏界面", affectsGameplay: false, enabled: false);
        var visibleOff = Mod("ShownOff", "可见界面", affectsGameplay: false, enabled: false);
        var visibleOn = Mod("ShownOn", "可见显示", affectsGameplay: false, enabled: true);
        var session = ModPanelSession.Create([hidden, visibleOff, visibleOn], store);
        session.SetQuery("可见");

        Assert.Null(session.TryBulk(BulkAction.SelectAll, group: null));
        Assert.Equal([(visibleOff.Key, true)], store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());

        store.Writes.Clear();
        Assert.Null(session.TryBulk(BulkAction.Invert, group: ModGroup.LocalOnly));
        Assert.Equal(
            [(visibleOff.Key, false), (visibleOn.Key, false)],
            store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());

        session.SetQuery("");
        var hiddenRow = session.Snapshot().Blocks.SelectMany(block => block.Rows).Single(row => row.Key == hidden.Key);
        Assert.False(hiddenRow.Enabled);
    }

    [Fact]
    public void GroupCountsFollowTheCurrentFilter()
    {
        var session = Session(
            Mod("A", "界面甲", affectsGameplay: false, enabled: true),
            Mod("B", "界面乙", affectsGameplay: false, enabled: false),
            Mod("C", "卡牌丙", tags: ["Cards"], enabled: true));
        session.SetQuery("界面");
        var block = Assert.Single(session.Snapshot().Blocks);

        Assert.Equal("皮肤", block.DisplayName);
        Assert.Equal("#6FCFC4", block.SwatchHex);
        Assert.Equal(1, block.Selected);
        Assert.Equal(2, block.Total);
        Assert.True(block.BulkEnabled);
    }

    [Fact]
    public void LibrariesSortSyncRequiredFirst()
    {
        var session = Session(
            Mod("BaseLib", affectsGameplay: false),
            Mod("MomoLib", workshopTitle: "MomoLib", tags: ["mod"]),
            Mod("STS2-RitsuLib", "RitsuLib", affectsGameplay: false, tags: ["Tools & APIs", "QoL"]),
            Mod("MpLib", tags: ["Tools & APIs", "Utility"]));
        var rows = Assert.Single(session.Snapshot().Blocks).Rows;

        Assert.Equal(["MomoLib", "MpLib", "BaseLib", "STS2-RitsuLib"], rows.Select(row => row.Subtitle).ToArray());
        Assert.Equal([true, true, false, false], rows.Select(row => row.SyncRequired).ToArray());
        Assert.All(rows, row => Assert.Equal(0, row.ExtensionCount));
    }

    [Fact]
    public void CollapsedExtensionsStayInTheSnapshotAndFollowTheParentTick()
    {
        var store = new RecordingStore();
        var body = Mod("HextechRunes", "Hextech Mayhem / 海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Relics"], enabled: false);
        var pack = Mod("HextechRunesSponsorPack", "海克斯大乱斗：额外拓展包", workshopTitle: "海克斯大乱斗：额外拓展包", tags: ["Relics"], enabled: false);
        var session = ModPanelSession.Create([body, pack], store);
        var rows = Assert.Single(session.Snapshot().Blocks).Rows;

        Assert.Equal(["HextechRunes", "HextechRunesSponsorPack"], rows.Select(row => IdOf(row.Key)).ToArray());
        Assert.Equal(1, rows[0].ExtensionCount);
        Assert.Equal(0, rows[0].ExtensionsEnabled);
        Assert.Equal(1, rows[1].Depth);
        Assert.Equal(body.Key, rows[1].ParentKey);

        Assert.Null(session.TrySet(body.Key, true));
        Assert.Equal(
            [(body.Key, true), (pack.Key, true)],
            store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());
    }

    [Fact]
    public void BulkStillIncludesExtensionsAndDoesNotTouchOtherGroups()
    {
        var store = new RecordingStore();
        var body = Mod("HextechRunes", "海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Relics"], enabled: true);
        var pack = Mod("HextechRunesSponsorPack", "海克斯大乱斗：额外拓展包", workshopTitle: "海克斯大乱斗：额外拓展包", tags: ["Relics"], enabled: true);
        var skin = Mod("RegentFX", affectsGameplay: false, tags: ["Cosmetics"], enabled: true);
        var session = ModPanelSession.Create([body, pack, skin], store);

        Assert.Null(session.TryBulk(BulkAction.Invert, ModGroup.Gameplay));
        Assert.Equal(
            [(body.Key, false), (pack.Key, false)],
            store.Writes.Select(change => (change.Key, change.Enabled)).ToArray());
        Assert.True(session.Snapshot().Blocks.SelectMany(block => block.Rows).Single(row => row.Key == skin.Key).Enabled);
    }

    [Fact]
    public void SearchingAChildWithoutItsParentLeavesItUnindented()
    {
        var session = Session(
            Mod("HextechRunes", "Hextech Mayhem / 海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Relics"]),
            Mod("HextechRunesSponsorPack", "海克斯大乱斗：额外拓展包", workshopTitle: "海克斯大乱斗：额外拓展包", tags: ["Relics"]));
        session.SetQuery("额外拓展包");
        var row = Assert.Single(Assert.Single(session.Snapshot().Blocks).Rows);

        Assert.Equal(0, row.Depth);
        Assert.Null(row.ParentKey);
        Assert.Equal("海克斯大乱斗", row.ExtendsTitle);
        Assert.Equal(0, row.ExtensionCount);
    }

    [Fact]
    public void PinyinSearchKeepsTheHextechBodyAndItsPack()
    {
        var session = Session(
            Mod("HextechRunes", "Hextech Mayhem / 海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Relics"]),
            Mod("HextechRunesSponsorPack", "海克斯大乱斗：额外拓展包", workshopTitle: "海克斯大乱斗：额外拓展包", tags: ["Relics"]),
            Mod("MomoLib", workshopTitle: "MomoLib", tags: ["mod"]));

        session.SetQuery("haikesi");
        var rows = Assert.Single(session.Snapshot().Blocks).Rows;
        Assert.Equal(["HextechRunes", "HextechRunesSponsorPack"], rows.Select(row => IdOf(row.Key)).ToArray());

        session.SetQuery("hks");
        Assert.Equal(2, Assert.Single(session.Snapshot().Blocks).Rows.Count);
    }

    [Fact]
    public void GensokyoStaysBesideMomoLibAndChimeraStaysUnderAutoAnthony()
    {
        var session = Session(
            Mod("MomoLib", workshopTitle: "MomoLib", tags: ["mod"]),
            Mod("GensokyoSpire", workshopTitle: "幻想尖塔", tags: ["Characters", "Cards"], dependsOn: ["MomoLib"]),
            Mod("BaseLib", affectsGameplay: false),
            Mod("AutoAnthony", "Auto-Anthonyology", workshopTitle: "东尼算法 Auto-Anthonyology", tags: ["Cards"]),
            Mod("Chimera", "东尼算法：重生", workshopTitle: "东尼算法：重生", tags: ["Cards"], dependsOn: ["AutoAnthony", "BaseLib"]));
        var blocks = session.Snapshot().Blocks.ToDictionary(block => block.Group);

        Assert.Equal(["MomoLib", "BaseLib"], blocks[ModGroup.Library].Rows.Select(row => IdOf(row.Key)).ToArray());
        Assert.Equal(
            ["GensokyoSpire", "AutoAnthony", "Chimera"],
            blocks[ModGroup.Gameplay].Rows.Select(row => IdOf(row.Key)).ToArray());
        var chimera = blocks[ModGroup.Gameplay].Rows.Single(row => IdOf(row.Key) == "Chimera");
        Assert.Equal(1, chimera.Depth);
        Assert.Equal("东尼算法 Auto-Anthonyology", chimera.ExtendsTitle);
        Assert.Equal(0, blocks[ModGroup.Gameplay].Rows.Single(row => IdOf(row.Key) == "GensokyoSpire").Depth);
        Assert.Null(blocks[ModGroup.Gameplay].Rows.Single(row => IdOf(row.Key) == "GensokyoSpire").ExtendsTitle);
    }

    [Fact]
    public void OverrideChangesParentAndSubtypeWithoutChangingGroup()
    {
        var chimera = Mod("Chimera", tags: ["Cards"], dependsOn: ["AutoAnthony"]);
        var session = ModPanelSession.Create(
            [
                Mod("AutoAnthony", tags: ["Cards"]),
                Mod("Other", "另一个父级", workshopTitle: "另一个父级", tags: ["Cards"]),
                chimera,
            ],
            new RecordingStore(),
            ModOverrideParser.Parse(
                """
                {"mods":[{"id":"Chimera","parent":"Other","subtype":"Mechanic","group":"Library"}]}
                """));
        var rows = Assert.Single(session.Snapshot().Blocks).Rows;
        var child = rows.Single(row => row.Key == chimera.Key);

        Assert.Equal(ModGroup.Gameplay, child.Group);
        Assert.Equal(ModKind.Mechanic, child.Kind);
        Assert.Equal("另一个父级", child.ExtendsTitle);
        Assert.Equal(rows[1].Key, child.ParentKey);
    }

    [Fact]
    public void FailedSaveDoesNotChangeLocalState()
    {
        var store = new RecordingStore { Fail = true };
        var mod = Mod("A", "界面甲", affectsGameplay: false, enabled: false);
        var session = ModPanelSession.Create([mod], store);

        Assert.Equal("磁盘拒绝", session.TrySet(mod.Key, true));
        Assert.False(Assert.Single(Assert.Single(session.Snapshot().Blocks).Rows).Enabled);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void ModKeysRoundTripIdsThatContainColons()
    {
        var key = ModKeys.Create(ModOrigin.SteamWorkshop, "author:pack");
        Assert.True(ModKeys.TryParse(key, out var origin, out var id));
        Assert.Equal(ModOrigin.SteamWorkshop, origin);
        Assert.Equal("author:pack", id);
    }

    [Fact]
    public void EnglishLanguageSwapsEveryPanelString()
    {
        var previous = PanelCopy.Language;
        PanelCopy.Language = PanelLanguage.English;
        try
        {
            Assert.Equal("Search mod name or Workshop title...", PanelCopy.SearchPlaceholder);
            Assert.Equal("Select all", PanelCopy.SelectAll);
            Assert.Equal("Invert", PanelCopy.Invert);
            Assert.Equal("Only affects this group", PanelCopy.GroupScopeHint);
            Assert.Equal("Only affects the current search results", PanelCopy.VisibleScopeHint);
            Assert.Equal("({0} extension(s))", PanelCopy.ExtensionsLabel);
            Assert.Equal("Extends {0}", PanelCopy.ExtendsFormat);
            Assert.Equal("No matching mods found", PanelCopy.NoHits);
            Assert.Equal("Loading Workshop info…", PanelCopy.Loading);
            Assert.Equal("Libraries", ModGroupDisplay.Name(ModGroup.Library));
            Assert.Equal("Characters / Gameplay / Content", ModGroupDisplay.Name(ModGroup.Gameplay));
            Assert.Equal("Cosmetics", ModGroupDisplay.Name(ModGroup.LocalOnly));
        }
        finally
        {
            PanelCopy.Language = previous;
        }
    }

    [Fact]
    public void EnglishLanguageReachesBlocksReasonsAndErrors()
    {
        var previous = PanelCopy.Language;
        PanelCopy.Language = PanelLanguage.English;
        try
        {
            var session = Session(Mod("A", "界面甲", affectsGameplay: false, enabled: false));
            session.SetQuery("界面");
            var block = Assert.Single(session.Snapshot().Blocks);
            Assert.Equal("Cosmetics", block.DisplayName);
            var row = Assert.Single(block.Rows);
            Assert.Contains("affects_gameplay is false", row.Reasons);

            Assert.Equal("Mod not found.", session.TrySet("missing", true));

            session.SetQuery("不存在的名字");
            Assert.True(session.Snapshot().NoHits);
        }
        finally
        {
            PanelCopy.Language = previous;
        }
    }

    [Fact]
    public void ChineseIsTheDefaultLanguage()
    {
        Assert.Equal(PanelLanguage.Chinese, PanelCopy.Language);
        Assert.Equal("皮肤", ModGroupDisplay.Name(ModGroup.LocalOnly));
    }

    private static string IdOf(string key) => key[(key.IndexOf(':') + 1)..];

    private static ModPanelSession Session(params ModFacts[] mods) =>
        ModPanelSession.Create(mods, new RecordingStore());

    private static ModFacts Mod(
        string id,
        string? name = null,
        bool affectsGameplay = true,
        bool enabled = false,
        string? workshopTitle = null,
        string? description = null,
        string[]? tags = null,
        string[]? dependsOn = null) => new()
    {
        Key = ModKeys.Create(ModOrigin.ModsDirectory, id),
        Id = id,
        Origin = ModOrigin.ModsDirectory,
        Name = name ?? id,
        Description = description ?? name,
        AffectsGameplay = affectsGameplay,
        Enabled = enabled,
        WorkshopTitle = workshopTitle,
        WorkshopTags = tags ?? [],
        DependencyIds = dependsOn ?? [],
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
