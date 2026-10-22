using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// Children with <see cref="DockPanel.DockProperty"/> Left or Right sit at those edges like in a
/// <see cref="DockPanel"/>; the others stand in the middle of the whole panel, not of the room left
/// between the edges, and move aside only as far as they must not to cover the edges. The labelled keys of
/// the on-screen keyboard: icon left, name centred, controller button right (owner, 2026-10-22).
/// </summary>
public sealed class CentredDockPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CentredDockPanel, double>(nameof(Spacing));

    static CentredDockPanel()
    {
        AffectsMeasure<CentredDockPanel>(SpacingProperty);
    }

    /// <summary>The least room between an edge's children and the centred ones.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        var width = Gap(WidthAt(Dock.Left)) + WidthAt(Dock.Left) + CentreWidth() + Gap(WidthAt(Dock.Right)) + WidthAt(Dock.Right);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var left = 0.0;
        var right = finalSize.Width;
        foreach (var child in Children)
        {
            switch (SideOf(child))
            {
                case Dock.Left:
                    child.Arrange(new Rect(left, 0, child.DesiredSize.Width, finalSize.Height));
                    left += child.DesiredSize.Width;
                    break;
                case Dock.Right:
                    right -= child.DesiredSize.Width;
                    child.Arrange(new Rect(right, 0, child.DesiredSize.Width, finalSize.Height));
                    break;
            }
        }

        var leftmost = left + Gap(left);
        var rightmost = right - Gap(finalSize.Width - right);
        foreach (var child in Children.Where(c => SideOf(c) is null))
        {
            var width = Math.Min(child.DesiredSize.Width, Math.Max(0, rightmost - leftmost));
            var x = Math.Clamp((finalSize.Width - width) / 2, leftmost, Math.Max(leftmost, rightmost - width));
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
        }

        return finalSize;
    }

    // An edge without anything shown on it needs no room beside it.
    private double Gap(double edgeWidth) => edgeWidth > 0 ? Spacing : 0;

    private double WidthAt(Dock side) => Children.Where(c => SideOf(c) == side).Sum(c => c.DesiredSize.Width);

    private double CentreWidth() => Children.Where(c => SideOf(c) is null).Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max();

    private static Dock? SideOf(Control child) => !child.IsSet(DockPanel.DockProperty) ? null : DockPanel.GetDock(child) switch
    {
        Dock.Left => Dock.Left,
        Dock.Right => Dock.Right,
        _ => null,
    };
}
