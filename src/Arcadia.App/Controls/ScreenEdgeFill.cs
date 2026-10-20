using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// A line along the edge of its bounds, growing from the top centre down both sides to the bottom
/// centre as <see cref="Progress"/> goes from 0 to 1. Shows how far Back + Start have been held
/// (the mouse mode, #64), like <see cref="HoldRing"/> does for a held A.
/// </summary>
public sealed class ScreenEdgeFill : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, double>(nameof(Progress));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, double>(nameof(StrokeThickness), 8);

    static ScreenEdgeFill()
    {
        AffectsRender<ScreenEdgeFill>(ProgressProperty, StrokeProperty, StrokeThicknessProperty);
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
        if (right <= left || bottom <= top)
        {
            return;
        }

        var centre = (left + right) / 2;

        // Each half runs from the top centre round one side to the bottom centre: the same length both ways.
        var length = (right - left) + (bottom - top);
        var reach = length * progress;
        var pen = new Pen(Stroke, thickness, lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter);
        context.DrawGeometry(null, pen, Half(reach, [new(centre, top), new(right, top), new(right, bottom), new(centre, bottom)]));
        context.DrawGeometry(null, pen, Half(reach, [new(centre, top), new(left, top), new(left, bottom), new(centre, bottom)]));
    }

    /// <summary>The first <paramref name="reach"/> pixels of the line through <paramref name="corners"/>.</summary>
    private static StreamGeometry Half(double reach, Point[] corners)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(corners[0], isFilled: false);
        for (var i = 1; i < corners.Length && reach > 0; i++)
        {
            var from = corners[i - 1];
            var to = corners[i];
            var segment = Point.Distance(from, to);
            if (reach >= segment)
            {
                path.LineTo(to);
            }
            else
            {
                path.LineTo(from + ((to - from) * (reach / segment)));
            }

            reach -= segment;
        }

        path.EndFigure(isClosed: false);
        return geometry;
    }
}
