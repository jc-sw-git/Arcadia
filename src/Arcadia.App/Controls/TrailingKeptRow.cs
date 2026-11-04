using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// Two children side by side, the second right after the first: the first gets only the room the second leaves, so a
/// long title ends with "…" and what follows it (e.g. "seit 10:05" in the status modal, #166) is never cut. A grid
/// can't do that: its auto column takes the title's whole width, its star column pushes the second to the far edge.
/// </summary>
public sealed class TrailingKeptRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TrailingKeptRow, double>(nameof(Spacing));

    static TrailingKeptRow()
    {
        AffectsMeasure<TrailingKeptRow>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2)
        {
            throw new InvalidOperationException($"{nameof(TrailingKeptRow)} takes exactly two children.");
        }

        var (first, kept) = (Children[0], Children[1]);
        kept.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var room = Math.Max(0, availableSize.Width - kept.DesiredSize.Width - Spacing);
        first.Measure(new Size(room, availableSize.Height));
        return new Size(first.DesiredSize.Width + Spacing + kept.DesiredSize.Width, Math.Max(first.DesiredSize.Height, kept.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (first, kept) = (Children[0], Children[1]);
        var firstWidth = Math.Min(first.DesiredSize.Width, Math.Max(0, finalSize.Width - kept.DesiredSize.Width - Spacing));
        first.Arrange(new Rect(0, 0, firstWidth, finalSize.Height));
        kept.Arrange(new Rect(firstWidth + Spacing, 0, kept.DesiredSize.Width, finalSize.Height));
        return finalSize;
    }
}
