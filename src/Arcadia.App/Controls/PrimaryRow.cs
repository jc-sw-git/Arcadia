using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// A row whose first child, the primary button, fills what the others leave and gets the room its content needs
/// first: the others are measured into the rest, so a <see cref="CollapsingStack"/> inside them gives up its trailing
/// parts. Used in the preview, where "⧉ Optionen" keeps only its symbol beside a long label like "Zurück ins Spiel".
/// </summary>
public sealed class PrimaryRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PrimaryRow, double>(nameof(Spacing));

    static PrimaryRow()
    {
        AffectsMeasure<PrimaryRow>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        if (children.Count == 0)
        {
            return default;
        }

        var primary = children[0];
        primary.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var room = availableSize.Width - primary.DesiredSize.Width - (Spacing * (children.Count - 1));
        foreach (var other in children.Skip(1))
        {
            other.Measure(new Size(Math.Max(0, room), availableSize.Height));
            room -= other.DesiredSize.Width;
        }

        // Measured again into what is left, so content that still doesn't fit can shrink (a Viewbox).
        var othersWidth = OthersWidth(children);
        primary.Measure(new Size(Math.Max(0, availableSize.Width - othersWidth), availableSize.Height));
        var width = double.IsInfinity(availableSize.Width) ? primary.DesiredSize.Width + othersWidth : availableSize.Width;
        return new Size(width, children.Max(c => c.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        if (children.Count == 0)
        {
            return finalSize;
        }

        var x = Math.Max(0, finalSize.Width - OthersWidth(children));
        children[0].Arrange(new Rect(0, 0, x, finalSize.Height));
        foreach (var other in children.Skip(1))
        {
            x += Spacing;
            other.Arrange(new Rect(x, 0, other.DesiredSize.Width, finalSize.Height));
            x += other.DesiredSize.Width;
        }

        return finalSize;
    }

    /// <summary>The width of all but the primary, with the spacing before each.</summary>
    private double OthersWidth(List<Control> children) => children.Skip(1).Sum(c => c.DesiredSize.Width + Spacing);
}
