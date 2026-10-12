namespace FullModPanel.Core.Tests;

public class ClassifierTests
{
    [Fact]
    public void DisplayNamesStayFixedAndUnknownIsLast()
    {
        Assert.Equal(
            ["前置 / API", "角色与扩展", "体验优化 · QoL", "玩法机制 · 调整", "开发与调试", "未分类"],
            ModCategoryDisplay.Order.Select(ModCategoryDisplay.Name).ToArray());
    }

    [Fact]
    public void KnownLibraryIdWinsOverContentKeywords()
    {
        var mod = Sample("BaseLib", "BaseLib 卡牌", affectsGameplay: true);
        Assert.Equal(ModCategory.DependencyLibrary, Classify(mod));
    }

    [Fact]
    public void LibrarySuffixWithoutOtherSignalsIsLibrary()
    {
        var mod = Sample("WidgetLib", "WidgetLib", affectsGameplay: true);
        Assert.Equal(ModCategory.DependencyLibrary, Classify(mod));
    }

    [Fact]
    public void LibrarySuffixPlusCardKeywordsIsUnknown()
    {
        var mod = Sample("WidgetLib", "卡牌 library", affectsGameplay: true);
        Assert.Equal(ModCategory.Unknown, Classify(mod));
    }

    [Fact]
    public void TwoDependentsWithoutContentSignalsIsLibrary()
    {
        var library = Sample("SharedHooks", "SharedHooks", affectsGameplay: true);
        var left = Sample("Left", "Left", dependsOn: ["SharedHooks"]);
        var right = Sample("Right", "Right", dependsOn: ["SharedHooks"]);
        var classified = ModClassifier.ClassifyAll([library, left, right]);

        Assert.Equal(3, classified.Count);
        Assert.Equal(ModCategory.DependencyLibrary, CategoryOf(classified, "SharedHooks"));
        Assert.Equal(ModCategory.Unknown, CategoryOf(classified, "Left"));
    }

    [Fact]
    public void TwoDependentsPlusCardKeywordsIsUnknown()
    {
        var library = Sample("HeroPack", "角色卡牌", affectsGameplay: true);
        var left = Sample("PatchA", "PatchA", dependsOn: ["HeroPack"]);
        var right = Sample("PatchB", "PatchB", dependsOn: ["HeroPack"]);
        var classified = ModClassifier.ClassifyAll([library, left, right]);

        Assert.Equal(ModCategory.Unknown, CategoryOf(classified, "HeroPack"));
    }

    [Fact]
    public void SingleDependentIsNotEnough()
    {
        var library = Sample("MaybeHooks", "MaybeHooks", affectsGameplay: false);
        var user = Sample("User", "User", dependsOn: ["MaybeLib"]);
        var classified = ModClassifier.ClassifyAll([library, user]);

        Assert.Equal(ModCategory.Unknown, CategoryOf(classified, "MaybeHooks"));
    }

    [Theory]
    [InlineData(true, "一张新卡牌", ModCategory.Content)]
    [InlineData(false, "一张新卡牌", ModCategory.Unknown)]
    [InlineData(true, "数值平衡与削弱", ModCategory.Balance)]
    [InlineData(false, "数值平衡与削弱", ModCategory.Unknown)]
    [InlineData(false, "主菜单设置界面", ModCategory.QolUi)]
    [InlineData(true, "主菜单设置界面", ModCategory.Unknown)]
    [InlineData(true, "战斗调试 console", ModCategory.DebugTool)]
    [InlineData(false, "战斗调试 console", ModCategory.DebugTool)]
    [InlineData(false, "小工具", ModCategory.DebugTool)]
    [InlineData(true, "小工具", ModCategory.Unknown)]
    [InlineData(true, "卡牌平衡", ModCategory.Unknown)]
    [InlineData(true, "没有关键词", ModCategory.Unknown)]
    public void KeywordAndGameplayFlag(bool affectsGameplay, string text, ModCategory expected)
    {
        Assert.Equal(expected, Classify(Sample("Sample", text, affectsGameplay: affectsGameplay)));
    }

    [Fact]
    public void WorkshopTagClassifiesWhenManifestIsSilent()
    {
        var mod = Sample("Silent", "Silent", affectsGameplay: true) with
        {
            WorkshopTags = ["Balance"],
        };
        Assert.Equal(ModCategory.Balance, Classify(mod));
    }

    [Fact]
    public void WorkshopTitleAndTagConflictIsUnknown()
    {
        var mod = Sample("Silent", "Silent", affectsGameplay: true) with
        {
            WorkshopTitle = "新卡牌",
            WorkshopTags = ["QoL"],
        };
        Assert.Equal(ModCategory.Unknown, Classify(mod));
    }

    [Fact]
    public void WorkshopLibraryTagLosesToCardTitle()
    {
        var mod = Sample("Silent", "Silent", affectsGameplay: true) with
        {
            WorkshopTitle = "新卡牌",
            WorkshopTags = ["library"],
        };
        Assert.Equal(ModCategory.Unknown, Classify(mod));
    }

    [Fact]
    public void NothingIsDropped()
    {
        var mods = new[]
        {
            Sample("Known", "BaseLib", affectsGameplay: true) with { Id = "BaseLib" },
            Sample("Mystery", "Mystery"),
            Sample("", "无名") with { Id = "", CanPersist = false },
        };
        var classified = ModClassifier.ClassifyAll(mods);
        Assert.Equal(mods.Length, classified.Count);
        Assert.Contains(classified, item => item.Category == ModCategory.Unknown);
    }

    [Fact]
    public void PanelModDescriptionIsQol()
    {
        var mod = Sample(
            "FullModPanel",
            "在设置界面中分类管理已安装模组，支持按模组名及创意工坊标题双向搜索、批量启闭。不影响游戏对局与存档。",
            affectsGameplay: false);
        Assert.Equal(ModCategory.QolUi, Classify(mod));
    }

    private static ModCategory Classify(ModFacts mod) =>
        ModClassifier.ClassifyAll([mod]).Single().Category;

    private static ModCategory CategoryOf(IReadOnlyList<ClassifiedMod> mods, string id) =>
        mods.Single(mod => mod.Facts.Id == id).Category;

    private static ModFacts Sample(
        string id,
        string text,
        bool affectsGameplay = true,
        IReadOnlyList<string>? dependsOn = null) => new()
    {
        Key = ModKeys.Create(ModOrigin.ModsDirectory, id),
        Id = id,
        Origin = ModOrigin.ModsDirectory,
        Name = text,
        Description = text,
        AffectsGameplay = affectsGameplay,
        DependencyIds = dependsOn ?? [],
        CanPersist = true,
    };
}
