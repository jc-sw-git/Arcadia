using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>Which edges of an <see cref="EdgeFade"/> fade: the start is the top or the left, the end the bottom or the right.</summary>
[Flags]
public enum FadeEdges
{
    None = 0,
    Start = 1,
    End = 2,
    Both = Start | End,
}

/// <summary>
/// The one edge fade of Arcadia (#237): content cut off at an edge dissolves into the surface behind it. It lies over
/// the content (same cell and margin as its scroller), so clicks and the wheel pass through to what is under it. Its
/// defaults (curve, transition, overlap, colour) are the style <c>ctl|EdgeFade</c> in <c>Theme.axaml</c>; a place sets
/// only what differs. An edge shows while <see cref="Condition"/> holds and, with a <see cref="ScrollTarget"/>, only
/// where that scroller can still scroll towards.
/// </summary>
public sealed class EdgeFade : Control
{
    /// <summary>Less than this is rounding, not a scroll: the edge counts as reached.</summary>
    private const double ScrollSlack = 1;

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<EdgeFade, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<FadeEdges> EdgesProperty =
        AvaloniaProperty.Register<EdgeFade, FadeEdges>(nameof(Edges), FadeEdges.Both);

    /// <summary>Depth of both fades; <see cref="StartSize"/> or <see cref="EndSize"/> set one apart.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<EdgeFade, double>(nameof(Size), 96);

    public static readonly StyledProperty<double> StartSizeProperty =
        AvaloniaProperty.Register<EdgeFade, double>(nameof(StartSize), double.NaN);

    public static readonly StyledProperty<double> EndSizeProperty =
        AvaloniaProperty.Register<EdgeFade, double>(nameof(EndSize), double.NaN);

    /// <summary>The surface the content fades into, e.g. the page's background or a panel's surface.</summary>
    public static readonly StyledProperty<Color> FadeColorProperty =
        AvaloniaProperty.Register<EdgeFade, Color>(nameof(FadeColor));

    /// <summary>How opaque the fade is from the edge (offset 0) inwards (offset 1): only the stops' alpha counts.</summary>
    public static readonly StyledProperty<GradientStops?> CurveProperty =
        AvaloniaProperty.Register<EdgeFade, GradientStops?>(nameof(Curve));

    /// <summary>The corners of the surface it fades into: the start fade takes the start's corners, the end the end's.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<EdgeFade, CornerRadius>(nameof(CornerRadius));

    /// <summary>
    /// How far a fade reaches past its edge. At a scaled window size the content's clip and the fade's edge round to
    /// different pixels, which leaves a line of unfaded content without it.
    /// </summary>
    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<EdgeFade, double>(nameof(Overlap));

    /// <summary>How long a fade takes to come or go; zero switches it at once.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<EdgeFade, TimeSpan>(nameof(Duration));

    /// <summary>The fades show only while this holds, e.g. while the search's keyboard is out.</summary>
    public static readonly StyledProperty<bool> ConditionProperty =
        AvaloniaProperty.Register<EdgeFade, bool>(nameof(Condition), true);

    /// <summary>With a scroller, an edge fades only where it can still scroll towards.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollTargetProperty =
        AvaloniaProperty.Register<EdgeFade, ScrollViewer?>(nameof(ScrollTarget));

    /// <summary>
    /// With a <see cref="ScrollTarget"/>: instead of laying <see cref="FadeColor"/> over the content, the content itself
    /// turns transparent towards the edge (#531). For a strip over an image, e.g. the launcher tabs over a game's artwork,
    /// where a coloured fade would show as a dark block with hard edges.
    /// </summary>
    public static readonly StyledProperty<bool> MasksContentProperty =
        AvaloniaProperty.Register<EdgeFade, bool>(nameof(MasksContent));

    /// <summary>Room a focused element keeps from a fade, e.g. for its focus ring (<see cref="ScrollIntoView"/>).</summary>
    public static readonly StyledProperty<double> FocusRoomProperty =
        AvaloniaProperty.Register<EdgeFade, double>(nameof(FocusRoom));

    private readonly Border _start = new();
    private readonly Border _end = new();

    static EdgeFade()
    {
        AffectsArrange<EdgeFade>(OrientationProperty, SizeProperty, StartSizeProperty, EndSizeProperty, OverlapProperty);
    }

    public EdgeFade()
    {
        _start.Opacity = 0;
        _end.Opacity = 0;
        VisualChildren.Add(_start);
        VisualChildren.Add(_end);
    }

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public FadeEdges Edges { get => GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double StartSize { get => GetValue(StartSizeProperty); set => SetValue(StartSizeProperty, value); }

    public double EndSize { get => GetValue(EndSizeProperty); set => SetValue(EndSizeProperty, value); }

    public Color FadeColor { get => GetValue(FadeColorProperty); set => SetValue(FadeColorProperty, value); }

    public GradientStops? Curve { get => GetValue(CurveProperty); set => SetValue(CurveProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    public bool Condition { get => GetValue(ConditionProperty); set => SetValue(ConditionProperty, value); }

    public ScrollViewer? ScrollTarget { get => GetValue(ScrollTargetProperty); set => SetValue(ScrollTargetProperty, value); }

    public double FocusRoom { get => GetValue(FocusRoomProperty); set => SetValue(FocusRoomProperty, value); }

    public bool MasksContent { get => GetValue(MasksContentProperty); set => SetValue(MasksContentProperty, value); }

    /// <summary>The fade at the top or the left, as drawn (for tests: its bounds and opacity).</summary>
    public Control StartFade => _start;

    /// <summary>The fade at the bottom or the right, as drawn.</summary>
    public Control EndFade => _end;

    public bool ShowsStart { get; private set; }

    public bool ShowsEnd { get; private set; }

    private double StartDepth => double.IsNaN(StartSize) ? Size : StartSize;

    private double EndDepth => double.IsNaN(EndSize) ? Size : EndSize;

    /// <summary>
    /// Scrolls <paramref name="target"/> into view clear of the fades and with <see cref="FocusRoom"/> around it;
    /// <paramref name="extraRoom"/> keeps more, e.g. above a group's first row for its heading. At the ends of the
    /// scroll range the range stops it.
    /// </summary>
    public void ScrollIntoView(Control target, Thickness extraRoom = default)
    {
        var start = (Edges.HasFlag(FadeEdges.Start) ? StartDepth : 0) + FocusRoom;
        var end = (Edges.HasFlag(FadeEdges.End) ? EndDepth : 0) + FocusRoom;
        var room = Orientation == Orientation.Vertical ? new Thickness(FocusRoom, start, FocusRoom, end) : new Thickness(start, FocusRoom, end, FocusRoom);
        target.BringIntoView(new Rect(target.Bounds.Size).Inflate(room + extraRoom));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollTargetProperty)
        {
            if (change.OldValue is ScrollViewer old)
            {
                old.ScrollChanged -= OnScrollChanged;
                old.OpacityMask = null;
            }

            if (change.NewValue is ScrollViewer scroller)
            {
                scroller.ScrollChanged += OnScrollChanged;
            }

            UpdateShown();
        }
        else if (change.Property == ConditionProperty || change.Property == EdgesProperty || change.Property == OrientationProperty
                 || change.Property == MasksContentProperty || change.Property == CurveProperty)
        {
            UpdateShown();
        }

        if (change.Property == OrientationProperty || change.Property == FadeColorProperty || change.Property == CurveProperty
            || change.Property == CornerRadiusProperty)
        {
            UpdateLook();
        }
        else if (change.Property == DurationProperty)
        {
            _start.Transitions = Fading();
            _end.Transitions = Fading();
        }
    }

    /// <summary>
    /// Shows the fades as they stand when it comes into view: a fade whose condition holds from the start and that has
    /// no scroller gets no property change that would show it.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLook();
        UpdateShown();
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var overlap = Overlap;
        if (Orientation == Orientation.Vertical)
        {
            _start.Arrange(new Rect(0, -overlap, finalSize.Width, StartDepth));
            _end.Arrange(new Rect(0, finalSize.Height - EndDepth + overlap, finalSize.Width, EndDepth));
        }
        else
        {
            _start.Arrange(new Rect(-overlap, 0, StartDepth, finalSize.Height));
            _end.Arrange(new Rect(finalSize.Width - EndDepth + overlap, 0, EndDepth, finalSize.Height));
        }

        return finalSize;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateShown();

    private void UpdateShown()
    {
        var showsStart = Condition && Edges.HasFlag(FadeEdges.Start);
        var showsEnd = Condition && Edges.HasFlag(FadeEdges.End);
        if (ScrollTarget is { } scroller)
        {
            var vertical = Orientation == Orientation.Vertical;
            var offset = vertical ? scroller.Offset.Y : scroller.Offset.X;
            var max = vertical ? scroller.Extent.Height - scroller.Viewport.Height : scroller.Extent.Width - scroller.Viewport.Width;
            showsStart &= offset > ScrollSlack;
            showsEnd &= offset < max - ScrollSlack;
        }

        ShowsStart = showsStart;
        ShowsEnd = showsEnd;
        var paints = !(MasksContent && ScrollTarget is not null);
        _start.Opacity = paints && showsStart ? 1 : 0;
        _end.Opacity = paints && showsEnd ? 1 : 0;
        if (ScrollTarget is { } target)
        {
            target.OpacityMask = paints ? null : Mask(target);
        }
    }

    /// <summary>
    /// The scroller's opacity: the curve turned round (where a painted fade would cover, the content is gone) at each
    /// edge that shows, fully opaque between them. Null with no edge showing.
    /// </summary>
    private LinearGradientBrush? Mask(ScrollViewer target)
    {
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? target.Viewport.Height : target.Viewport.Width;
        if ((!ShowsStart && !ShowsEnd) || length <= 0)
        {
            return null;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        var curve = Curve ?? [];
        if (ShowsStart)
        {
            var depth = Math.Min(StartDepth / length, 0.5);
            foreach (var stop in curve)
            {
                brush.GradientStops.Add(new GradientStop(Visible(stop), stop.Offset * depth));
            }
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(Colors.Black, 0));
        }

        if (ShowsEnd)
        {
            var depth = Math.Min(EndDepth / length, 0.5);
            foreach (var stop in curve.Reverse())
            {
                brush.GradientStops.Add(new GradientStop(Visible(stop), 1 - (stop.Offset * depth)));
            }
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(Colors.Black, 1));
        }

        return brush;
    }

    /// <summary>How much of the content shows where the curve's stop would cover it.</summary>
    private static Color Visible(GradientStop stop) => Color.FromArgb((byte)(255 - stop.Color.A), 0, 0, 0);

    private void UpdateLook()
    {
        var vertical = Orientation == Orientation.Vertical;
        var corners = CornerRadius;
        _start.Background = Gradient(new RelativePoint(0, 0, RelativeUnit.Relative), vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative));
        _end.Background = vertical
            ? Gradient(new RelativePoint(0, 1, RelativeUnit.Relative), new RelativePoint(0, 0, RelativeUnit.Relative))
            : Gradient(new RelativePoint(1, 0, RelativeUnit.Relative), new RelativePoint(0, 0, RelativeUnit.Relative));
        _start.CornerRadius = vertical ? new CornerRadius(corners.TopLeft, corners.TopRight, 0, 0) : new CornerRadius(corners.TopLeft, 0, 0, corners.BottomLeft);
        _end.CornerRadius = vertical ? new CornerRadius(0, 0, corners.BottomRight, corners.BottomLeft) : new CornerRadius(0, corners.TopRight, corners.BottomRight, 0);
    }

    /// <summary>The curve in <see cref="FadeColor"/>, opaque at <paramref name="edge"/>.</summary>
    private LinearGradientBrush Gradient(RelativePoint edge, RelativePoint inside)
    {
        var brush = new LinearGradientBrush { StartPoint = edge, EndPoint = inside };
        var colour = FadeColor;
        foreach (var stop in Curve ?? [])
        {
            var alpha = (byte)Math.Round(colour.A * stop.Color.A / 255.0);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, colour.R, colour.G, colour.B), stop.Offset));
        }

        return brush;
    }

    private Transitions? Fading() => Duration > TimeSpan.Zero
        ? [new DoubleTransition { Property = OpacityProperty, Duration = Duration }]
        : null;
}
