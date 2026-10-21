using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// A line along the edge of its bounds, growing from the top centre down both sides to the bottom
/// centre as <see cref="Progress"/> goes from 0 to 1. Shows how far Back + Start (the mouse mode, #64)
/// or View (back to Arcadia) have been held, like <see cref="HoldRing"/> does for a held A.
/// </summary>
public sealed class ScreenEdgeFill : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, double>(nameof(Progress));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, double>(nameof(StrokeThickness), 8);

    /// <summary>Rounds the corners, to run along a rounded box.</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<ScreenEdgeFill, double>(nameof(CornerRadius));

    /// <summary>A rounded corner is drawn as this many short straight pieces.</summary>
    private const int CornerSteps = 8;

    static ScreenEdgeFill()
    {
        AffectsRender<ScreenEdgeFill>(ProgressProperty, StrokeProperty, StrokeThicknessProperty, CornerRadiusProperty);
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
        if (right <= left || bottom <= top)
        {
            return;
        }

        // Each half runs from the top centre round one side to the bottom centre: the same length both ways.
        var radius = Math.Clamp(CornerRadius - inset, 0, Math.Min(right - left, bottom - top) / 2);
        var rightHalf = HalfOutline(left, top, right, bottom, radius, side: 1);
        var leftHalf = HalfOutline(left, top, right, bottom, radius, side: -1);
        var reach = Length(rightHalf) * progress;
        var pen = radius > 0
            ? new Pen(Stroke, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : new Pen(Stroke, thickness, lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter);
        context.DrawGeometry(null, pen, Half(reach, rightHalf));
        context.DrawGeometry(null, pen, Half(reach, leftHalf));
    }

    /// <summary>The corners from the top centre round one side (1 right, -1 left) to the bottom centre.</summary>
    private static Point[] HalfOutline(double left, double top, double right, double bottom, double radius, int side)
    {
        var centre = (left + right) / 2;
        var edge = side > 0 ? right : left;
        var points = new List<Point> { new(centre, top) };
        if (radius <= 0)
        {
            points.Add(new(edge, top));
            points.Add(new(edge, bottom));
        }
        else
        {
            Corner(points, new Point(edge - (side * radius), top + radius), radius, -Math.PI / 2, side);
            Corner(points, new Point(edge - (side * radius), bottom - radius), radius, 0, side);
        }

        points.Add(new(centre, bottom));
        return [.. points];
    }

    /// <summary>A quarter circle round <paramref name="centre"/>, starting at <paramref name="from"/> (radians, 0 is the side).</summary>
    private static void Corner(List<Point> points, Point centre, double radius, double from, int side)
    {
        for (var step = 0; step <= CornerSteps; step++)
        {
            var angle = from + (Math.PI / 2 * step / CornerSteps);
            points.Add(new Point(centre.X + (side * radius * Math.Cos(angle)), centre.Y + (radius * Math.Sin(angle))));
        }
    }

    private static double Length(Point[] points)
    {
        var length = 0.0;
        for (var i = 1; i < points.Length; i++)
        {
            length += Point.Distance(points[i - 1], points[i]);
        }

        return length;
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
