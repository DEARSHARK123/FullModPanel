namespace FullModPanel.Core.Tests;

public class ClassifierTests
{
    [Fact]
    public void DisplayNamesAreTheThreeGroups()
    {
        Assert.Equal(
            ["依赖库", "角色 / 玩法 / 内容", "皮肤"],
            ModGroupDisplay.Order.Select(ModGroupDisplay.Name).ToArray());
    }

    [Fact]
    public void KnownLibraryIdsStayLibrariesEvenWithContentTags()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("BaseLib", tags: ["Cards"]),
            Sample("STS2-RitsuLib", "RitsuLib", affectsGameplay: false, tags: ["Tools & APIs", "Misc", "QoL"]),
            Sample("RitsuLib", affectsGameplay: false),
            Sample("ModManagerSettings", affectsGameplay: false),
            Sample("ModConfig", affectsGameplay: false),
        ]);

        Assert.All(classified, item => Assert.Equal(ModGroup.Library, item.Group));
        Assert.True(Of(classified, "BaseLib").SyncRequired);
        Assert.False(Of(classified, "STS2-RitsuLib").SyncRequired);
    }

    [Fact]
    public void LibraryLikeNameWithoutContentTagsIsLibrary()
    {
        var mod = Of(ModClassifier.ClassifyAll([Sample("WidgetLib", description: "一张新卡牌")]), "WidgetLib");
        Assert.Equal(ModGroup.Library, mod.Group);
    }

    [Fact]
    public void ContentTagsBlockLibraryLikeNames()
    {
        var mod = Of(ModClassifier.ClassifyAll([Sample("WidgetLib", tags: ["Characters"])]), "WidgetLib");
        Assert.Equal(ModGroup.Gameplay, mod.Group);
        Assert.Equal(ModKind.Character, mod.Kind);
    }

    [Fact]
    public void DescriptionAloneDoesNotMakeALibrary()
    {
        var mod = Of(ModClassifier.ClassifyAll([Sample("CoolMod", description: "A modding framework and library")]), "CoolMod");
        Assert.Equal(ModGroup.Gameplay, mod.Group);
    }

    [Fact]
    public void ToolsAndApisPlusOneDependentIsLibraryEvenWithContentTags()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("Hooks", tags: ["Cards", "Tools & APIs"]),
            Sample("User", dependsOn: ["Hooks"]),
        ]);

        Assert.Equal(ModGroup.Library, Of(classified, "Hooks").Group);
        Assert.Equal(ModGroup.Gameplay, Of(classified, "User").Group);
    }

    [Fact]
    public void ToolsAndApisWithoutADependentIsNotALibrary()
    {
        var panel = Of(ModClassifier.ClassifyAll([Sample("FullModPanel", "完整版模组面板", affectsGameplay: false, tags: ["Tools & APIs"])]), "FullModPanel");
        Assert.Equal(ModGroup.LocalOnly, panel.Group);
        Assert.Equal(ModKind.Tool, panel.Kind);
        Assert.False(panel.SyncRequired);
    }

    [Fact]
    public void DependentsWithoutToolsAndApisAreNotLibraries()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("SharedHooks"),
            Sample("Left", dependsOn: ["SharedHooks"]),
            Sample("Right", dependsOn: ["SharedHooks"]),
        ]);

        Assert.Equal(ModGroup.Gameplay, Of(classified, "SharedHooks").Group);
    }

    [Fact]
    public void OmittedOrTrueGameplayFlagIsTheMiddleGroupAndFalseIsSkin()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("Body", tags: ["Cards", "Relics"]),
            Sample("RegentFX", affectsGameplay: false, tags: ["Cosmetics", "Utility", "Misc"]),
            Sample("Quiet", "没有关键词"),
        ]);

        Assert.Equal(ModGroup.Gameplay, Of(classified, "Body").Group);
        Assert.Equal(ModKind.Content, Of(classified, "Body").Kind);
        Assert.Equal(ModGroup.LocalOnly, Of(classified, "RegentFX").Group);
        Assert.Equal(ModKind.Cosmetic, Of(classified, "RegentFX").Kind);
        Assert.Equal(ModGroup.Gameplay, Of(classified, "Quiet").Group);
        Assert.True(Of(classified, "Quiet").SyncRequired);
    }

    [Fact]
    public void CharactersInTheSkinGroupAreCosmetic()
    {
        var mod = Of(ModClassifier.ClassifyAll([Sample("Look", affectsGameplay: false, tags: ["Characters", "Cards"])]), "Look");
        Assert.Equal(ModGroup.LocalOnly, mod.Group);
        Assert.Equal(ModKind.Cosmetic, mod.Kind);
    }

    [Fact]
    public void MultipleTagsDoNotDropTheMod()
    {
        var mods = new[]
        {
            Sample("BaseLib", affectsGameplay: false),
            Sample("Mystery", "Mystery", tags: ["Cards", "QoL", "Modifiers"]),
            Sample("Nameless", name: "", affectsGameplay: false) with { Id = "Nameless" },
        };
        var classified = ModClassifier.ClassifyAll(mods);
        Assert.Equal(mods.Length, classified.Count);
        Assert.Equal(ModKind.Content, Of(classified, "Mystery").Kind);
        Assert.DoesNotContain(classified, item => item.Group == (ModGroup)99);
    }

    [Fact]
    public void HextechAttachesByTitlePrefix()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("HextechRunes", "Hextech Mayhem / 海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["gameplay", "Modifiers", "Relics"]),
            Sample("HextechRunesSponsorPack", "海克斯大乱斗：额外拓展包", workshopTitle: "海克斯大乱斗：额外拓展包", tags: ["gameplay", "Modifiers", "Relics"]),
        ]);

        var pack = Of(classified, "HextechRunesSponsorPack");
        Assert.Equal(ModGroup.Gameplay, pack.Group);
        Assert.Equal(Of(classified, "HextechRunes").Facts.Key, pack.ParentKey);
        Assert.Equal(1, pack.Depth);
        Assert.Equal("海克斯大乱斗", pack.ExtendsTitle);
        Assert.Equal("海克斯大乱斗：额外拓展包", pack.Title);
        Assert.Equal("海克斯大乱斗：额外拓展包", pack.Subtitle);
    }

    [Fact]
    public void TitlePrefixAttachesWhenIdsDoNot()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("RunesMain", "本体", workshopTitle: "海克斯大乱斗"),
            Sample("SponsorExtra", "扩展", workshopTitle: "海克斯大乱斗：额外拓展包"),
        ]);

        Assert.Equal(Of(classified, "RunesMain").Facts.Key, Of(classified, "SponsorExtra").ParentKey);
    }

    [Fact]
    public void ChimeraAttachesToAutoAnthonyNotBaseLib()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("BaseLib", affectsGameplay: false),
            Sample("AutoAnthony", "Auto-Anthonyology", workshopTitle: "东尼算法 Auto-Anthonyology", tags: ["Cards", "Modifiers", "Humor"]),
            Sample("Chimera", "东尼算法：重生", workshopTitle: "东尼算法：重生", tags: ["Cards"], dependsOn: ["AutoAnthony", "BaseLib"]),
        ]);

        var chimera = Of(classified, "Chimera");
        Assert.Equal(Of(classified, "AutoAnthony").Facts.Key, chimera.ParentKey);
        Assert.Equal(1, chimera.Depth);
        Assert.Equal(ModGroup.Library, Of(classified, "BaseLib").Group);
        Assert.NotEqual(Of(classified, "BaseLib").Facts.Key, chimera.ParentKey);
    }

    [Fact]
    public void DependencyBeatsADifferentTitlePrefix()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("AutoAnthony", "东尼算法", workshopTitle: "别的名字", tags: ["Cards"]),
            Sample("OtherPack", "海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Cards"]),
            Sample("Chimera", "海克斯大乱斗：重生", workshopTitle: "海克斯大乱斗：重生", tags: ["Cards"], dependsOn: ["AutoAnthony"]),
        ]);

        Assert.Equal(Of(classified, "AutoAnthony").Facts.Key, Of(classified, "Chimera").ParentKey);
    }

    [Fact]
    public void GensokyoDoesNotAttachToMomoLib()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("MomoLib", workshopTitle: "MomoLib", tags: ["mod"]),
            Sample("GensokyoSpire", workshopTitle: "幻想尖塔", tags: ["Characters", "Cards", "Relics"], dependsOn: ["MomoLib"]),
        ]);

        Assert.Equal(ModGroup.Library, Of(classified, "MomoLib").Group);
        Assert.True(Of(classified, "MomoLib").SyncRequired);
        var spire = Of(classified, "GensokyoSpire");
        Assert.Equal(ModGroup.Gameplay, spire.Group);
        Assert.Null(spire.ParentKey);
        Assert.Null(spire.ExtendsTitle);
        Assert.Equal(0, spire.Depth);
        Assert.Equal(ModKind.Character, spire.Kind);
    }

    [Fact]
    public void IdPrefixUsesABoundaryAndSkipsALowercaseContinuation()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("HextechRunes", "Alpha", workshopTitle: "Alpha"),
            Sample("HextechRunesSponsorPack", "Beta", workshopTitle: "Beta"),
            Sample("HextechRunes|SponsorPack", "Gamma", workshopTitle: "Gamma"),
            Sample("HextechRunesextra", "Delta", workshopTitle: "Delta"),
            Sample("Abc", "Epsilon", workshopTitle: "Epsilon"),
            Sample("AbcExtra", "Zeta", workshopTitle: "Zeta"),
        ]);

        Assert.Equal(Of(classified, "HextechRunes").Facts.Key, Of(classified, "HextechRunesSponsorPack").ParentKey);
        Assert.Equal(Of(classified, "HextechRunes").Facts.Key, Of(classified, "HextechRunes|SponsorPack").ParentKey);
        Assert.Null(Of(classified, "HextechRunesextra").ParentKey);
        Assert.Null(Of(classified, "AbcExtra").ParentKey);
    }

    [Fact]
    public void ShortNamesDoNotPrefixAndTheLongestPrefixWins()
    {
        var rejected = ModClassifier.ClassifyAll(
        [
            Sample("Short", "甲", workshopTitle: "甲"),
            Sample("Child", "甲：扩展", workshopTitle: "甲：扩展"),
        ]);
        Assert.Null(Of(rejected, "Child").ParentKey);

        var longest = ModClassifier.ClassifyAll(
        [
            Sample("Short", "海克斯", workshopTitle: "海克斯"),
            Sample("Long", "海克斯大乱斗", workshopTitle: "海克斯大乱斗"),
            Sample("Child", "包", workshopTitle: "海克斯大乱斗：包"),
        ]);
        Assert.Equal(Of(longest, "Long").Facts.Key, Of(longest, "Child").ParentKey);
    }

    [Fact]
    public void ExtensionsFlattenToTheRoot()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("A", "根模组", workshopTitle: "根模组", tags: ["Cards"]),
            Sample("B", "中层", tags: ["Cards"], dependsOn: ["A"]),
            Sample("C", "末端", tags: ["Cards"], dependsOn: ["B"]),
        ]);

        var root = Of(classified, "A").Facts.Key;
        Assert.Equal(root, Of(classified, "B").ParentKey);
        Assert.Equal(root, Of(classified, "C").ParentKey);
        Assert.Equal(1, Of(classified, "C").Depth);
    }

    [Fact]
    public void CrossGroupAttachmentDoesNotIndent()
    {
        var classified = ModClassifier.ClassifyAll(
        [
            Sample("Body", "海克斯大乱斗", workshopTitle: "海克斯大乱斗", tags: ["Cards"]),
            Sample("Skin", "海克斯大乱斗：皮肤", workshopTitle: "海克斯大乱斗：皮肤", affectsGameplay: false),
        ]);

        var skin = Of(classified, "Skin");
        Assert.Equal(ModGroup.LocalOnly, skin.Group);
        Assert.Equal(0, skin.Depth);
        Assert.Null(skin.ParentKey);
        Assert.Equal("海克斯大乱斗", skin.ExtendsTitle);
    }

    [Fact]
    public void ManualParentBeatsDependencyAndDoesNotFallThrough()
    {
        var mods = new[]
        {
            Sample("AutoAnthony", tags: ["Cards"]),
            Sample("Other", "另一个", tags: ["Cards"]),
            Sample("BaseLib", affectsGameplay: false),
            Sample("Chimera", tags: ["Cards"], dependsOn: ["AutoAnthony", "BaseLib"]),
        };

        var chosen = ModClassifier.ClassifyAll(mods, [new ModOverride("Chimera", true, "Other", ModKind.Cosmetic)]);
        Assert.Equal(Of(chosen, "Other").Facts.Key, Of(chosen, "Chimera").ParentKey);
        Assert.Equal(ModKind.Cosmetic, Of(chosen, "Chimera").Kind);
        Assert.Equal(ModGroup.Gameplay, Of(chosen, "Chimera").Group);

        var missing = ModClassifier.ClassifyAll(mods, [new ModOverride("Chimera", true, "Missing", null)]);
        Assert.Null(Of(missing, "Chimera").ParentKey);

        var library = ModClassifier.ClassifyAll(mods, [new ModOverride("Chimera", true, "BaseLib", null)]);
        Assert.Null(Of(library, "Chimera").ParentKey);

        var cleared = ModClassifier.ClassifyAll(mods, [new ModOverride("Chimera", true, null, null)]);
        Assert.Null(Of(cleared, "Chimera").ParentKey);
    }

    [Fact]
    public void ManualParentOnALibraryDoesNotMoveItsGroup()
    {
        var classified = ModClassifier.ClassifyAll(
            [
                Sample("BaseLib", affectsGameplay: false),
                Sample("AutoAnthony", tags: ["Cards"]),
            ],
            [new ModOverride("BaseLib", true, "AutoAnthony", null)]);

        var library = Of(classified, "BaseLib");
        Assert.Equal(ModGroup.Library, library.Group);
        Assert.Equal(0, library.Depth);
        Assert.Null(library.ParentKey);
        Assert.Equal(Of(classified, "AutoAnthony").Title, library.ExtendsTitle);
    }

    [Fact]
    public void OverrideFileCanChangeSubtypeAndParentButNotGroup()
    {
        var parsed = ModOverrideParser.Parse(
            """
            {"mods":[{"id":"Chimera","parent":"AutoAnthony","subtype":"Cosmetic","group":"Library"}]}
            """);
        var entry = Assert.Single(parsed);
        Assert.Equal("Chimera", entry.Id);
        Assert.Equal("AutoAnthony", entry.ParentId);
        Assert.Equal(ModKind.Cosmetic, entry.Subtype);

        var classified = ModClassifier.ClassifyAll(
            [
                Sample("AutoAnthony", tags: ["Cards"]),
                Sample("Chimera", affectsGameplay: false, tags: ["Cosmetics"], dependsOn: ["AutoAnthony"]),
            ],
            parsed);
        var chimera = Of(classified, "Chimera");
        Assert.Equal(ModGroup.LocalOnly, chimera.Group);
        Assert.Equal(ModKind.Cosmetic, chimera.Kind);
        Assert.Equal(0, chimera.Depth);
        Assert.Null(chimera.ParentKey);
        Assert.Equal(Of(classified, "AutoAnthony").Title, chimera.ExtendsTitle);
    }

    [Fact]
    public void InvalidOverrideJsonIsIgnored()
    {
        Assert.Empty(ModOverrideParser.Parse("{"));
        Assert.Empty(ModOverrideParser.Parse("   "));
    }

    [Fact]
    public void DisplayLinesAlwaysShowBothAndFallBackToTheId()
    {
        var same = Of(ModClassifier.ClassifyAll([Sample("Same", "同名模组", workshopTitle: "同名模组", affectsGameplay: false)]), "Same");
        Assert.Equal("同名模组", same.Title);
        Assert.Equal("同名模组", same.Subtitle);

        var local = Of(ModClassifier.ClassifyAll([Sample("LocalOnlyId", "清单名", affectsGameplay: false)]), "LocalOnlyId");
        Assert.Equal("清单名", local.Title);
        Assert.Equal("LocalOnlyId", local.Subtitle);

        var idOnly = new ModFacts
        {
            Key = ModKeys.Create(ModOrigin.ModsDirectory, "OnlyId"),
            Id = "OnlyId",
            Origin = ModOrigin.ModsDirectory,
            Name = null,
            AffectsGameplay = true,
            CanPersist = true,
        };
        var bare = Of(ModClassifier.ClassifyAll([idOnly]), "OnlyId");
        Assert.Equal("OnlyId", bare.Title);
        Assert.Equal("OnlyId", bare.Subtitle);
    }

    [Fact]
    public void SearchMatchesWorkshopTitleManifestNameAndIdButNotDescription()
    {
        var mod = Sample("HextechRunesSponsorPack", "清单名", workshopTitle: "工坊标题", description: "只有简介里的稀有词");
        Assert.True(ModClassifier.MatchesQuery("工坊", mod));
        Assert.True(ModClassifier.MatchesQuery("清单", mod));
        Assert.True(ModClassifier.MatchesQuery("sponsor", mod));
        Assert.False(ModClassifier.MatchesQuery("稀有词", mod));
        Assert.True(ModClassifier.MatchesQuery("  ", mod));
    }

    private static ClassifiedMod Of(IReadOnlyList<ClassifiedMod> mods, string id) =>
        mods.Single(mod => mod.Facts.Id == id);

    private static ModFacts Sample(
        string id,
        string? name = null,
        bool affectsGameplay = true,
        string? workshopTitle = null,
        string[]? tags = null,
        string[]? dependsOn = null,
        string? description = null) => new()
    {
        Key = ModKeys.Create(ModOrigin.ModsDirectory, id),
        Id = id,
        Origin = ModOrigin.ModsDirectory,
        Name = name ?? id,
        Description = description,
        AffectsGameplay = affectsGameplay,
        DependencyIds = dependsOn ?? [],
        WorkshopTitle = workshopTitle,
        WorkshopTags = tags ?? [],
        CanPersist = true,
    };
}
