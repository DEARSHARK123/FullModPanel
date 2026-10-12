using Godot;

namespace FullModPanel;

/// <summary>
/// A solid triangle drawn with Godot polygons, so it does not depend on font glyphs.
/// It points right when collapsed and turns a quarter turn to point down when expanded.
/// It ignores the mouse; the parent button receives the clicks.
/// </summary>
/// <remarks>
/// Drawing goes through the <c>draw</c> signal, not a <c>_Draw</c> override. The mod is
/// compiled without Godot's source generator, so C# virtual overrides such as <c>_Draw</c>
/// are never called, and the triangle would stay invisible.
/// </remarks>
internal sealed class ExpandArrow : Control
{
    private const float ArrowSize = 14f;
    private const float TurnSeconds = 0.15f;

    private Color _color = new(0.831373f, 0.701961f, 0.360784f);
    private bool _expanded;
    private Tween? _tween;

    public ExpandArrow()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(ArrowSize, ArrowSize);
        PivotOffset = new Vector2(ArrowSize / 2f, ArrowSize / 2f);
        // Centre inside the parent button, whatever its size.
        AnchorLeft = 0.5f;
        AnchorTop = 0.5f;
        AnchorRight = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -ArrowSize / 2f;
        OffsetTop = -ArrowSize / 2f;
        OffsetRight = ArrowSize / 2f;
        OffsetBottom = ArrowSize / 2f;
        Connect(CanvasItem.SignalName.Draw, Callable.From(DrawTriangle));
    }

    public void SetColor(Color color)
    {
        _color = color;
        QueueRedraw();
    }

    /// <summary>Points right when collapsed, down when expanded. Animates only when the state changes.</summary>
    public void SetExpanded(bool expanded)
    {
        if (expanded == _expanded)
        {
            return;
        }

        _expanded = expanded;
        var target = expanded ? Mathf.Pi / 2f : 0f;
        _tween?.Kill();
        _tween = null;
        if (!IsInsideTree())
        {
            Rotation = target;
            return;
        }

        _tween = CreateTween();
        _tween.TweenProperty(this, "rotation", target, TurnSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void DrawTriangle()
    {
        var points = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(ArrowSize, ArrowSize / 2f),
            new Vector2(0f, ArrowSize),
        };
        DrawColoredPolygon(points, _color);
    }
}
