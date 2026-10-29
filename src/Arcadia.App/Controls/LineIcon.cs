using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// A line icon from <c>Theme.axaml</c> on the 24 × 24 grid of the designs (#141), scaled to the control's size: the
/// geometry <see cref="Icon"/> stroked with round caps and joins, and, where the theme has one, <c>{Icon}Fill</c> filled
/// (the dots of the controller, the head of the note). Both in <see cref="Foreground"/>, inherited like a text's colour.
/// </summary>
public sealed class LineIcon : Control
{
    private const double Grid = 24;

    /// <summary>The resource key of the icon's lines, e.g. <c>IconTypeTool</c>; its filled parts are <c>IconTypeToolFill</c>.</summary>
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<LineIcon, string?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LineIcon>();

    /// <summary>On the 24 × 24 grid, so it grows with the icon; the designs use 2, and 2.2 for 16 px and less.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LineIcon, double>(nameof(StrokeThickness), 2);

    static LineIcon()
    {
        AffectsRender<LineIcon>(IconProperty, ForegroundProperty, StrokeThicknessProperty);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (Icon is not { Length: > 0 } key || Foreground is not { } brush || size <= 0
            || !this.TryFindResource(key, out var found) || found is not Geometry lines)
        {
            return;
        }

        var scale = size / Grid;
        var offset = new Vector((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
        {
            var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, lines);
            if (this.TryFindResource(key + "Fill", out var fill) && fill is Geometry filled)
            {
                context.DrawGeometry(brush, null, filled);
            }
        }
    }
}
