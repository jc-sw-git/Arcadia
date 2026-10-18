using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// A rounded frame drawn clockwise from the top centre up to <see cref="Progress"/>. Shows how far
/// a held button has come; a style animates <see cref="Progress"/> from 0 to 1.
/// </summary>
public sealed class HoldRing : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<HoldRing, double>(nameof(Progress));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<HoldRing, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<HoldRing, double>(nameof(StrokeThickness), 3);

    /// <summary>Corner radius of the outer edge.</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<HoldRing, double>(nameof(CornerRadius), 8);

    static HoldRing()
    {
        AffectsRender<HoldRing>(ProgressProperty, StrokeProperty, StrokeThicknessProperty, CornerRadiusProperty);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var thickness = StrokeThickness;
        var progress = Math.Clamp(Progress, 0, 1);
        if (Stroke is null || progress <= 0 || thickness <= 0)
        {
            return;
        }

        // The stroke is centred on the path, so the path runs half a stroke inside the bounds.
        var inset = thickness / 2;
        var left = inset;
        var top = inset;
        var right = Bounds.Width - inset;
        var bottom = Bounds.Height - inset;
        var radius = Math.Clamp(CornerRadius - inset, 0, Math.Min(right - left, bottom - top) / 2);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var corner = new Size(radius, radius);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point((left + right) / 2, top), isFilled: false);
            path.LineTo(new Point(right - radius, top));
            path.ArcTo(new Point(right, top + radius), corner, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point(right, bottom - radius));
            path.ArcTo(new Point(right - radius, bottom), corner, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point(left + radius, bottom));
            path.ArcTo(new Point(left, bottom - radius), corner, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point(left, top + radius));
            path.ArcTo(new Point(left + radius, top), corner, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point((left + right) / 2, top));
            path.EndFigure(isClosed: false);
        }

        // One dash as long as the progress, then a gap longer than the rest; dashes count in stroke widths.
        var length = (2 * ((right - left) + (bottom - top) - (4 * radius))) + (2 * Math.PI * radius);
        var dashes = new[] { length * progress / thickness, (length / thickness) + 1 };
        var pen = new Pen(Stroke, thickness, new DashStyle(dashes, 0), PenLineCap.Flat);
        context.DrawGeometry(null, pen, geometry);
    }
}
