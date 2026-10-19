using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Arcadia.App.Controls;

/// <summary>
/// A row that never grows wider than its room. When its children don't fit, they give up parts
/// in steps, all the same ones at a time, so the row stays even: first each child's last part
/// (<see cref="CollapsingStack.DropTrailingProperty"/>), then all but the first child share the
/// room left and shrink (the widest giving way first, a narrow one keeps its width), and if that
/// isn't enough, they also drop the next part. Used for the launcher chips: keyword, then the name
/// shrinks, then only the icon.
/// </summary>
public sealed class SharedWidthRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<SharedWidthRow, double>(nameof(Spacing));

    /// <summary>The parts a child may drop, at most.</summary>
    private const int MaxDrop = 2;

    static SharedWidthRow()
    {
        AffectsMeasure<SharedWidthRow>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        var spacing = Spacing * Math.Max(0, children.Count - 1);
        for (var drop = 0; drop <= MaxDrop; drop++)
        {
            var width = MeasureAll(children, drop, availableSize.Height) + spacing;
            if (width <= availableSize.Width || children.Count < 2)
            {
                return new Size(width, TallestOf(children));
            }

            // One part fewer didn't do it: the rest shrink, if every one of them still keeps what it has.
            if (drop == 1 && Shrink(children, availableSize.Width - spacing - children[0].DesiredSize.Width, availableSize.Height))
            {
                return new Size(children.Sum(c => c.DesiredSize.Width) + spacing, TallestOf(children));
            }
        }

        return new Size(Math.Min(children.Sum(c => c.DesiredSize.Width) + spacing, availableSize.Width), TallestOf(children));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + Spacing;
        }

        return finalSize;
    }

    private static double MeasureAll(List<Control> children, int drop, double height)
    {
        foreach (var child in children)
        {
            SetDropTrailing(child, drop);
            child.Measure(new Size(double.PositiveInfinity, height));
        }

        return children.Sum(c => c.DesiredSize.Width);
    }

    /// <summary>
    /// Sets <see cref="CollapsingStack.DropTrailing"/> on the stacks inside <paramref name="child"/>,
    /// and makes everything between them and the child measure again: a control measured again with
    /// the same size would otherwise answer from its last measure.
    /// </summary>
    private static void SetDropTrailing(Control child, int drop)
    {
        foreach (var stack in child.GetVisualDescendants().OfType<CollapsingStack>().Where(s => s.DropTrailing != drop))
        {
            stack.DropTrailing = drop;
            for (Visual? visual = stack; visual is not null && visual != child.GetVisualParent(); visual = visual.GetVisualParent())
            {
                (visual as Layoutable)?.InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// Measures all but the first child into <paramref name="room"/>. False if one of them had to
    /// give up a part to fit (it then comes out narrower than its share).
    /// </summary>
    private static bool Shrink(List<Control> children, double room, double height)
    {
        var shrinking = children.Skip(1).ToList();
        var limit = ShareLimit(shrinking.Select(c => c.DesiredSize.Width).ToList(), room);
        var kept = true;
        foreach (var child in shrinking.Where(c => c.DesiredSize.Width > limit))
        {
            child.Measure(new Size(limit, height));
            kept &= child.DesiredSize.Width >= limit - 1;
        }

        return kept;
    }

    private static double TallestOf(List<Control> children) => children.Count == 0 ? 0 : children.Max(c => c.DesiredSize.Height);

    /// <summary>The widest a child may be so that <paramref name="widths"/> fit into <paramref name="room"/>; narrower ones keep theirs.</summary>
    private static double ShareLimit(List<double> widths, double room)
    {
        var sorted = widths.Order().ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var share = Math.Max(0, room) / (sorted.Count - i);
            if (sorted[i] > share)
            {
                return share;
            }

            room -= sorted[i];
        }

        return double.PositiveInfinity;
    }
}
