using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Arcadia.App.Controls;

/// <summary>
/// Lays its children out in a row and, when the row gets too narrow, gives up the last ones first:
/// a child with a <see cref="MinShrinkWidthProperty"/> first shrinks down to it (a text then trims),
/// anything narrower hides it. The first child always shows. <see cref="DropTrailingProperty"/>
/// hides the last ones regardless, so a <see cref="SharedWidthRow"/> can make all its children
/// give up the same parts. Used for the launcher chips: the keyword goes first, then the name
/// shrinks, last only the icon is left.
/// </summary>
public sealed class CollapsingStack : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CollapsingStack, double>(nameof(Spacing));

    /// <summary>Space after the last child, only while more than the first one shows (an icon alone sits centred).</summary>
    public static readonly StyledProperty<double> EndPaddingProperty =
        AvaloniaProperty.Register<CollapsingStack, double>(nameof(EndPadding));

    /// <summary>
    /// How narrow a child may get before it hides; unset, it hides rather than shrink. Not
    /// <see cref="Layoutable.MinWidth"/>, which would keep a hidden child's width.
    /// </summary>
    public static readonly AttachedProperty<double> MinShrinkWidthProperty =
        AvaloniaProperty.RegisterAttached<CollapsingStack, Control, double>("MinShrinkWidth", double.NaN);

    /// <summary>How many of the last children to hide even if there's room.</summary>
    public static readonly StyledProperty<int> DropTrailingProperty =
        AvaloniaProperty.Register<CollapsingStack, int>(nameof(DropTrailing));

    private int _shown;
    private double _shrunkWidth = double.NaN;

    static CollapsingStack()
    {
        AffectsMeasure<CollapsingStack>(SpacingProperty, EndPaddingProperty, DropTrailingProperty);
    }

    // A hidden child is arranged with no width at the end, where this clips what it would draw.
    public CollapsingStack() => ClipToBounds = true;

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double EndPadding
    {
        get => GetValue(EndPaddingProperty);
        set => SetValue(EndPaddingProperty, value);
    }

    public int DropTrailing
    {
        get => GetValue(DropTrailingProperty);
        set => SetValue(DropTrailingProperty, value);
    }

    public static double GetMinShrinkWidth(Control control) => control.GetValue(MinShrinkWidthProperty);

    public static void SetMinShrinkWidth(Control control, double value) => control.SetValue(MinShrinkWidthProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        var height = 0.0;
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        _shrunkWidth = double.NaN;
        for (_shown = Math.Max(1, children.Count - DropTrailing); _shown > 1; _shown--)
        {
            var shown = children.Take(_shown).ToList();
            var width = RowWidth(shown.Select(c => c.DesiredSize.Width));
            if (width <= availableSize.Width)
            {
                break;
            }

            // The last child shown may shrink before it goes.
            var last = shown[^1];
            if (GetMinShrinkWidth(last) is var minWidth && !double.IsNaN(minWidth))
            {
                var room = availableSize.Width - (width - last.DesiredSize.Width);
                if (room >= minWidth)
                {
                    _shrunkWidth = room;
                    last.Measure(new Size(room, availableSize.Height));
                    break;
                }
            }
        }

        var widths = children.Take(_shown).Select((c, i) => i == _shown - 1 && !double.IsNaN(_shrunkWidth) ? _shrunkWidth : c.DesiredSize.Width);
        return new Size(RowWidth(widths), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        var x = 0.0;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (i >= _shown)
            {
                child.Arrange(new Rect(finalSize.Width, 0, 0, finalSize.Height));
                continue;
            }

            var width = i == _shown - 1 && !double.IsNaN(_shrunkWidth) ? _shrunkWidth : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }

        return finalSize;
    }

    private double RowWidth(IEnumerable<double> widths)
    {
        var list = widths.ToList();
        return list.Sum() + (Spacing * Math.Max(0, list.Count - 1)) + (list.Count > 1 ? EndPadding : 0);
    }
}
