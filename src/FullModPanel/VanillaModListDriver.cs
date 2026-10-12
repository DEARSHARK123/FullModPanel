using FullModPanel.Core;
using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Platform.Steam;

namespace FullModPanel;

/// <summary>
/// Inserts search, bulk actions, and category headers into the stock
/// <see cref="NModdingScreen"/>. Vanilla rows, tickboxes, detail pane, and Back stay.
/// </summary>
internal sealed class VanillaModListDriver : Node
{
    private const string InsertGroup = "fullmodpanel_insert";
    private const float RowHeight = 72f;
    private const float RowHeightWithSubtitle = 100f;

    private static readonly Color Cream = new(1f, 0.964706f, 0.886275f);
    private static readonly Color Gold = new(0.831373f, 0.701961f, 0.360784f);
    private static readonly Color Dim = new(0.603922f, 0.592157f, 0.549020f);
    private static readonly Color Line = new(0.403922f, 0.682353f, 0.921569f, 0.35f);
    private static readonly Color ErrorColor = new(0.95f, 0.55f, 0.45f);

    private static Font? _font;

    private readonly Sts2EnableStore _store = new();
    private readonly Dictionary<ulong, WorkshopText> _workshop = [];

    private NModdingScreen? _screen;
    private NScrollableContainer? _scroll;
    private VBoxContainer? _content;
    private VBoxContainer? _chrome;
    private LineEdit? _search;
    private Button? _selectAll;
    private Button? _invert;
    private Label? _loading;
    private Label? _error;
    private ModPanelSession? _session;
    private NModMenuRow? _selected;
    private Callable _poll;
    private bool _polling;
    private bool _layingOut;
    private ulong[] _requestedIds = [];

    public static void NoteSelection(NModdingScreen screen, NModMenuRow row) =>
        screen.GetNodeOrNull<VanillaModListDriver>("FullModPanelDriver")?.Remember(row);

    public void Install(NModdingScreen screen)
    {
        _screen = screen;
        if (screen.GetNodeOrNull<Control>("%ModInfoContainer") is Control info)
        {
            info.Visible = true;
        }

        _scroll = screen.GetNode<NScrollableContainer>("%ModsScrollContainer");
        _content = _scroll.GetNode<VBoxContainer>("Mask/Content");
        if (_content.GetNodeOrNull("FullModPanelRoot") is Node replaced)
        {
            _content.RemoveChild(replaced);
            replaced.QueueFree();
        }

        if (_chrome is null || !GodotObject.IsInstanceValid(_chrome))
        {
            BuildChrome();
        }

        EnsurePolling();
        Relayout();
    }

    public void Remember(NModMenuRow row) => _selected = row;

    public void Relayout()
    {
        if (_layingOut || _screen is null || _content is null || _scroll is null)
        {
            return;
        }

        _layingOut = true;
        try
        {
            LayoutList();
        }
        catch (Exception ex)
        {
            Log.Error($"FullModPanel failed to lay out the mod list: {ex}");
        }
        finally
        {
            _layingOut = false;
        }
    }

    private void LayoutList()
    {
        if (_content is not VBoxContainer content || _scroll is not NScrollableContainer scroll)
        {
            return;
        }

        var query = _session?.Query ?? _search?.Text ?? "";
            _session = ModPanelSession.Create(Sts2ModCatalog.Read(_workshop), _store);
            _session.SetQuery(query);
            var snapshot = _session.Snapshot();
            if (_selectAll is not null)
            {
                _selectAll.Disabled = !snapshot.BulkEnabled;
            }

            if (_invert is not null)
            {
                _invert.Disabled = !snapshot.BulkEnabled;
            }

            var rows = new List<NModMenuRow>();
            foreach (var child in content.GetChildren())
            {
                if (child is NModMenuRow row)
                {
                    rows.Add(row);
                }
            }

            RemoveInserts();

            var facts = snapshot.Blocks
                .SelectMany(block => block.Rows.Select(row => (block, row)))
                .ToDictionary(item => item.row.Key, item => item);
            var used = new HashSet<NModMenuRow>();
            var grouped = new Dictionary<ModCategory, List<Placed>>();
            foreach (var category in ModCategoryDisplay.Order)
            {
                grouped[category] = [];
            }

            foreach (var row in rows)
            {
                if (row.Mod is not { } mod)
                {
                    continue;
                }

                var identity = Sts2ModCatalog.Identify(mod);
                if (!facts.TryGetValue(identity.Key, out var placed))
                {
                    var name = mod.manifest?.name ?? identity.StableId;
                    if (!ModClassifier.MatchesQuery(query, name, null))
                    {
                        HideRow(row);
                        continue;
                    }

                    grouped[ModCategory.Unknown].Add(new Placed(row, null, null, ReadTick(row)));
                    used.Add(row);
                    continue;
                }

                used.Add(row);
                var subtitle = ModClassifier.SecondaryWorkshopName(placed.row.InGameName, placed.row.WorkshopTitle);
                grouped[placed.block.Category].Add(new Placed(row, placed.row, subtitle, placed.row.Enabled));
            }

            foreach (var row in rows)
            {
                if (!used.Contains(row))
                {
                    HideRow(row);
                }
            }

            var index = 0;
            var any = false;
            foreach (var category in ModCategoryDisplay.Order)
            {
                var group = grouped[category];
                if (group.Count == 0)
                {
                    continue;
                }

                any = true;
                var selectedCount = 0;
                foreach (var item in group)
                {
                    if (item.Enabled)
                    {
                        selectedCount++;
                    }
                }

                var header = MakeHeader(category, selectedCount, group.Count);
                Place(content, header, ref index);
                foreach (var item in group)
                {
                    item.Row.Visible = true;
                    ApplySubtitle(item.Row, item.Subtitle);
                    SyncTick(item.Row, item.Enabled);
                    Place(content, item.Row, ref index);
                }
            }

            var empty = MakeEmpty(any);
            Place(content, empty, ref index);
            scroll.DisableScrollingIfContentFits();
            RequestWorkshop();
            FitChrome();
    }

    public override void _ExitTree()
    {
        StopPolling();
        base._ExitTree();
    }

    private void BuildChrome()
    {
        if (_scroll?.GetParent() is not Control border)
        {
            Log.Warn("FullModPanel could not find the Installed Mods frame.");
            return;
        }

        _chrome = new VBoxContainer { Name = "FullModPanelChrome", MouseFilter = Control.MouseFilterEnum.Stop };
        _chrome.AddThemeConstantOverride("separation", 6);
        _chrome.AnchorLeft = 0;
        _chrome.AnchorTop = 0;
        _chrome.AnchorRight = 1;
        _chrome.AnchorBottom = 0;
        border.AddChild(_chrome);

        var hint = MakeLabel(PanelCopy.RestartNotice, Gold, 15);
        hint.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _chrome.AddChild(hint);

        var tools = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tools.AddThemeConstantOverride("separation", 6);
        tools.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _search = new LineEdit
        {
            PlaceholderText = PanelCopy.SearchPlaceholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
        };
        StyleField(_search);
        _search.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnSearchChanged));
        tools.AddChild(_search);

        _selectAll = MakeButton(PanelCopy.SelectAllVisible);
        _selectAll.Connect(Button.SignalName.Pressed, Callable.From(() => DeferBulk(null, BulkAction.SelectAll)));
        tools.AddChild(_selectAll);
        _invert = MakeButton(PanelCopy.InvertVisible);
        _invert.Connect(Button.SignalName.Pressed, Callable.From(() => DeferBulk(null, BulkAction.Invert)));
        tools.AddChild(_invert);
        _chrome.AddChild(tools);

        _loading = MakeLabel("正在加载创意工坊信息…", Dim, 13);
        _loading.Visible = false;
        _chrome.AddChild(_loading);
        _error = MakeLabel("", ErrorColor, 13);
        _error.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _error.Visible = false;
        _chrome.AddChild(_error);

        var rule = new ColorRect
        {
            Color = Line,
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _chrome.AddChild(rule);
        FitChrome();
    }

    private void OnSearchChanged(string text)
    {
        if (_session is null)
        {
            return;
        }

        _session.SetQuery(text);
        ShowError(null);
        Relayout();
    }

    private void DeferBulk(ModCategory? category, BulkAction action)
    {
        Callable.From(() =>
        {
            if (_session is null || _screen is null)
            {
                return;
            }

            ShowError(_session.TryBulk(action, category));
            _screen.OnModEnabledOrDisabled();
        }).CallDeferred();
    }

    private void FitChrome()
    {
        if (_chrome is null || _scroll is null)
        {
            return;
        }

        var height = Math.Max(_chrome.GetCombinedMinimumSize().Y, 72);
        _chrome.OffsetLeft = 12;
        _chrome.OffsetRight = -12;
        _chrome.OffsetTop = 8;
        _chrome.OffsetBottom = 8 + height;
        _scroll.OffsetTop = _chrome.OffsetBottom + 4;
    }

    private void RemoveInserts()
    {
        if (_content is null)
        {
            return;
        }

        var doomed = new List<Node>();
        foreach (var child in _content.GetChildren())
        {
            if (child.IsInGroup(InsertGroup))
            {
                doomed.Add(child);
            }
        }

        foreach (var node in doomed)
        {
            _content.RemoveChild(node);
            node.QueueFree();
        }
    }

    private Control MakeHeader(ModCategory category, int selected, int total)
    {
        var panel = new PanelContainer
        {
            Name = "FullModPanelHeader",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.AddToGroup(InsertGroup);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.04f),
            BorderColor = Line,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 6,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
        });

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        var heading = MakeLabel($"{ModCategoryDisplay.Name(category)}  {selected}/{total}", Cream, 16);
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(heading);

        var select = MakeButton(PanelCopy.SelectCategory);
        select.Disabled = total == 0;
        var captured = category;
        select.Connect(Button.SignalName.Pressed, Callable.From(() => DeferBulk(captured, BulkAction.SelectAll)));
        row.AddChild(select);
        var invert = MakeButton(PanelCopy.InvertCategory);
        invert.Disabled = total == 0;
        invert.Connect(Button.SignalName.Pressed, Callable.From(() => DeferBulk(captured, BulkAction.Invert)));
        row.AddChild(invert);
        panel.AddChild(row);
        return panel;
    }

    private Control MakeEmpty(bool anyVisible)
    {
        var label = MakeLabel(PanelCopy.NoHits, Cream, 18);
        label.Name = "FullModPanelEmpty";
        label.AddToGroup(InsertGroup);
        label.Visible = !anyVisible;
        label.CustomMinimumSize = new Vector2(0, anyVisible ? 0 : 36);
        return label;
    }

    private static void Place(Node parent, Node node, ref int index)
    {
        if (node.GetParent() != parent)
        {
            parent.AddChild(node);
        }

        parent.MoveChild(node, index);
        index++;
    }

    private void HideRow(NModMenuRow row)
    {
        row.Visible = false;
        if (_selected != row)
        {
            return;
        }

        row.SetSelected(false);
        _selected = null;
        BlankDetail();
    }

    /// <summary>
    /// v0.107.1 <c>NModInfoContainer</c> only exposes <c>Fill(Mod)</c>. Blank the
    /// stock detail nodes when they are still there; otherwise leave the pane.
    /// </summary>
    private void BlankDetail()
    {
        if (_screen?.GetNodeOrNull<Control>("%ModInfoContainer") is not Control info)
        {
            return;
        }

        if (info.GetNodeOrNull<RichTextLabel>("ModTitle") is RichTextLabel title)
        {
            title.Text = "";
        }

        if (info.GetNodeOrNull<RichTextLabel>("ModDescription") is RichTextLabel description)
        {
            description.Text = "";
        }

        if (info.GetNodeOrNull<TextureRect>("ModImage") is TextureRect image)
        {
            image.Texture = null;
        }
    }

    private static void ApplySubtitle(NModMenuRow row, string? subtitle)
    {
        var title = row.GetNode<Control>("Title");
        var extra = row.GetNodeOrNull<Label>("FullModPanelSubtitle");
        if (string.IsNullOrEmpty(subtitle))
        {
            if (extra is not null)
            {
                extra.Visible = false;
            }

            title.OffsetBottom = 0;
            row.CustomMinimumSize = new Vector2(0, RowHeight);
            return;
        }

        extra ??= MakeSubtitle(row);
        extra.Text = subtitle;
        extra.Visible = true;
        title.OffsetBottom = -24;
        row.CustomMinimumSize = new Vector2(0, RowHeightWithSubtitle);
    }

    private static Label MakeSubtitle(NModMenuRow row)
    {
        var label = MakeLabel("", Dim, 14);
        label.Name = "FullModPanelSubtitle";
        label.AnchorLeft = 0;
        label.AnchorRight = 1;
        label.AnchorTop = 1;
        label.AnchorBottom = 1;
        label.OffsetLeft = 2;
        label.OffsetRight = -150;
        label.OffsetTop = -22;
        label.OffsetBottom = -2;
        label.ClipContents = true;
        row.AddChild(label);
        return label;
    }

    private static void SyncTick(NModMenuRow row, bool enabled)
    {
        if (row.GetNodeOrNull<NTickbox>("Tickbox") is not NTickbox tick || tick.IsTicked == enabled)
        {
            return;
        }

        try
        {
            tick.IsTicked = enabled;
        }
        catch (Exception ex)
        {
            Log.Warn($"FullModPanel could not sync a tickbox: {ex.Message}");
        }
    }

    private static bool ReadTick(NModMenuRow row) =>
        row.GetNodeOrNull<NTickbox>("Tickbox")?.IsTicked ?? true;

    private void RequestWorkshop()
    {
        if (_session is null)
        {
            return;
        }

        var ids = Sts2ModCatalog.Read(_workshop)
            .Select(mod => mod.WorkshopId)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .Order()
            .ToArray();
        if (ids.SequenceEqual(_requestedIds))
        {
            return;
        }

        _requestedIds = ids;
        if (ids.Length == 0 || !SteamInitializer.Initialized)
        {
            if (_loading is not null)
            {
                _loading.Visible = false;
            }

            return;
        }

        if (_loading is not null)
        {
            _loading.Visible = true;
        }

        EnsurePolling();
        SteamWorkshopTitles.Begin(ids);
    }

    private void EnsurePolling()
    {
        if (_polling || GetTree() is not SceneTree tree)
        {
            return;
        }

        _poll = Callable.From(PollWorkshop);
        tree.Connect(SceneTree.SignalName.ProcessFrame, _poll);
        _polling = true;
    }

    private void PollWorkshop()
    {
        if (!SteamWorkshopTitles.TryConsume(out var titles) || titles is null)
        {
            return;
        }

        foreach (var pair in titles)
        {
            _workshop[pair.Key] = pair.Value;
        }

        if (_loading is not null)
        {
            _loading.Visible = false;
        }

        StopPolling();
        Relayout();
    }

    private void StopPolling()
    {
        if (_polling && GetTree() is SceneTree tree)
        {
            tree.Disconnect(SceneTree.SignalName.ProcessFrame, _poll);
        }

        _polling = false;
    }

    private void ShowError(string? error)
    {
        if (_error is null)
        {
            return;
        }

        _error.Text = error ?? "";
        _error.Visible = !string.IsNullOrEmpty(error);
        FitChrome();
    }

    private static Label MakeLabel(string text, Color color, int size)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        if (UiFont() is { } font)
        {
            label.AddThemeFontOverride("font", font);
        }

        return label;
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", Cream);
        button.AddThemeColorOverride("font_hover_color", Cream);
        button.AddThemeColorOverride("font_pressed_color", Cream);
        button.AddThemeColorOverride("font_disabled_color", new Color(Cream.R, Cream.G, Cream.B, 0.35f));
        if (UiFont() is { } font)
        {
            button.AddThemeFontOverride("font", font);
        }

        button.AddThemeStyleboxOverride("normal", ButtonBox(new Color(0.109804f, 0.133333f, 0.172549f, 0.92f)));
        button.AddThemeStyleboxOverride("hover", ButtonBox(new Color(0.149020f, 0.172549f, 0.219608f, 0.96f)));
        button.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.078431f, 0.094118f, 0.125490f, 1f)));
        button.AddThemeStyleboxOverride("disabled", ButtonBox(new Color(0.109804f, 0.133333f, 0.172549f, 0.45f)));
        button.AddThemeStyleboxOverride("focus", ButtonBox(new Color(0.149020f, 0.172549f, 0.219608f, 0.96f)));
        return button;
    }

    private static void StyleField(LineEdit field)
    {
        field.AddThemeColorOverride("font_color", Cream);
        field.AddThemeColorOverride("font_placeholder_color", Dim);
        field.AddThemeColorOverride("caret_color", Cream);
        field.AddThemeFontSizeOverride("font_size", 15);
        if (UiFont() is { } font)
        {
            field.AddThemeFontOverride("font", font);
        }

        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.047059f, 0.062745f, 0.094118f, 0.92f),
            BorderColor = new Color(Gold.R, Gold.G, Gold.B, 0.45f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
        field.AddThemeStyleboxOverride("normal", box);
        field.AddThemeStyleboxOverride("focus", box);
    }

    private static StyleBoxFlat ButtonBox(Color background) => new()
    {
        BgColor = background,
        BorderColor = new Color(Gold.R, Gold.G, Gold.B, 0.55f),
        BorderWidthBottom = 1,
        BorderWidthTop = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        ContentMarginLeft = 6,
        ContentMarginRight = 6,
        ContentMarginTop = 2,
        ContentMarginBottom = 2,
    };

    private static Font? UiFont()
    {
        _font ??= FontManager.GetSubstituteFont("zhs", FontType.Regular)
            ?? ResourceLoader.Load<Font>("res://themes/fonts/zhs/noto_sans_mono_cjksc_regular_shared.tres", null, ResourceLoader.CacheMode.Reuse);
        return _font;
    }

    private readonly record struct Placed(NModMenuRow Row, RowSnapshot? Snapshot, string? Subtitle, bool Enabled);
}
