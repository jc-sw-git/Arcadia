using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Controls;

/// <summary>
/// Places a cover of the detail carousel at its <see cref="Slot"/> (#499): 0 in front beside the title, -1 and -2 the stack
/// on the left, 1 and 2 the one on the right. Each step outwards is smaller, dimmer and turned further away with its inner
/// edge towards the middle, so the stacks lead into the depth (design "Detailseite neu: Mischung aus A, B und C"). A new
/// slot moves the cover there along the same path, so the turn of the carousel is one movement; slots past
/// <see cref="StackDepth"/> are invisible, the way covers come in and go out.
/// </summary>
/// <remarks>The places are in the 1920 × 1080 layout and for a 180 × 270 cover, the cover's top left corner.</remarks>
public sealed class DepthSlot : Decorator
{
    public static readonly StyledProperty<int> SlotProperty =
        AvaloniaProperty.Register<DepthSlot, int>(nameof(Slot));

    public static readonly StyledProperty<int> StackDepthProperty =
        AvaloniaProperty.Register<DepthSlot, int>(nameof(StackDepth), 2);

    /// <summary>Where the cover stands on its way to <see cref="Slot"/>; a transition moves it.</summary>
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<DepthSlot, double>(nameof(Position));

    /// <summary>As long as a turn of the carousel; the background and the page blend as long.</summary>
    public static readonly TimeSpan TurnTime = TimeSpan.FromMilliseconds(480);

    private const double CoverWidth = 180;
    private const double CoverHeight = 270;

    private const double LayoutWidth = 1920;

    // Per slot 0 … 3 on the left: left edge, top edge, scale, turn around the vertical axis (degrees), opacity. The right
    // stack mirrors the left one around the middle of the layout; slot 0 stands beside the title.
    private static readonly (double Left, double Top, double Scale, double Angle, double Opacity)[] Places =
    [
        (280, 116, 1, 0, 1),
        (70, 428, 0.8, 34, 1),
        (18, 462, 0.58, 46, 0.5),
        (-30, 484, 0.44, 54, 0),
    ];

    private readonly ScaleTransform _scale = new();
    private readonly Rotate3DTransform _turn = new() { Depth = 900 };
    private readonly TranslateTransform _move = new();

    public DepthSlot()
    {
        RenderTransform = new TransformGroup { Children = { _scale, _turn, _move } };
        RenderTransformOrigin = RelativePoint.Center;
        IsHitTestVisible = false;
        Place(0);
    }

    public int Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    /// <summary>How many covers each stack shows; covers further out are invisible.</summary>
    public int StackDepth
    {
        get => GetValue(StackDepthProperty);
        set => SetValue(StackDepthProperty, value);
    }

    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A cover that comes in stands at its slot at once; only later slots move it.
        Transitions = null;
        Position = Slot;
        Transitions =
        [
            new DoubleTransition { Property = PositionProperty, Duration = TurnTime, Easing = new SplineEasing(0.2, 0.8, 0.2, 1) },
        ];
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlotProperty && this.IsAttachedToVisualTree())
        {
            Position = Slot;
        }
        else if (change.Property == PositionProperty || change.Property == StackDepthProperty)
        {
            Place(Position);
        }
    }

    /// <summary>The cover between two slots: every value of the two places mixed by how far it has come.</summary>
    private void Place(double position)
    {
        var side = position < 0 ? -1 : 1;
        var distance = Math.Min(Math.Abs(position), Places.Length - 1);
        var inner = (int)Math.Floor(distance);
        var outer = Math.Min(inner + 1, Places.Length - 1);
        var t = distance - inner;
        double Mix(Func<int, double> value) => value(inner) + ((value(outer) - value(inner)) * t);

        var scale = Mix(k => Places[k].Scale);
        var left = Mix(k => k == 0 || side < 0 ? Places[k].Left : LayoutWidth - Places[k].Left - (CoverWidth * Places[k].Scale));
        var top = Mix(k => Places[k].Top);
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _turn.AngleY = side * Mix(k => Places[k].Angle);
        // Scaled around its centre, so the corner moves in by half the size it loses.
        _move.X = left - (CoverWidth / 2 * (1 - scale));
        _move.Y = top - (CoverHeight / 2 * (1 - scale));
        Opacity = Mix(k => k > StackDepth ? 0 : Places[k].Opacity);
    }
}
