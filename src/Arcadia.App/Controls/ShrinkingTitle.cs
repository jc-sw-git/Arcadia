using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Arcadia.App.Controls;

/// <summary>
/// A title that keeps its height (#531): it shrinks from <see cref="LargestSize"/> until it fits one line, down to
/// <see cref="SmallestSize"/>; a title still too long there wraps into two lines of that size and is cut with "…". Two
/// lines at half the size take about the height of one at the full size, so nothing below the title moves.
/// </summary>
public sealed class ShrinkingTitle : TextBlock
{
    public static readonly StyledProperty<double> LargestSizeProperty =
        AvaloniaProperty.Register<ShrinkingTitle, double>(nameof(LargestSize), 66);

    public static readonly StyledProperty<double> SmallestSizeProperty =
        AvaloniaProperty.Register<ShrinkingTitle, double>(nameof(SmallestSize), 33);

    static ShrinkingTitle()
    {
        AffectsMeasure<ShrinkingTitle>(LargestSizeProperty, SmallestSizeProperty);
    }

    public double LargestSize { get => GetValue(LargestSizeProperty); set => SetValue(LargestSizeProperty, value); }

    public double SmallestSize { get => GetValue(SmallestSizeProperty); set => SetValue(SmallestSizeProperty, value); }

    // A TextBlock's own style applies, not one for a type of its own.
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>
    /// The largest whole size from <paramref name="largest"/> down to <paramref name="smallest"/> at which a line
    /// <paramref name="widthAt"/> that size fits <paramref name="available"/>; <paramref name="smallest"/> when none does.
    /// The width grows about linearly with the size, so the first guess is the proportional one, then checked downwards.
    /// </summary>
    public static double FittingSize(Func<double, double> widthAt, double available, double largest, double smallest)
    {
        var full = widthAt(largest);
        if (full <= available || double.IsInfinity(available))
        {
            return largest;
        }

        var size = Math.Clamp(Math.Floor(largest * available / full), smallest, largest);
        while (size > smallest && widthAt(size) > available)
        {
            size--;
        }

        return size;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = FittingSize(WidthAt, availableSize.Width, LargestSize, SmallestSize);
        var lines = WidthAt(size) > availableSize.Width ? 2 : 1;
        // A smaller title keeps the height of one full line, centred in it, so the buttons below stay where they are.
        using var full = Layout(LargestSize);
        using var shown = Layout(size, availableSize.Width, lines);
        var room = full.Height - shown.Height;
        var padding = new Thickness(0, Math.Max(0, room) / 2);
        // Set only on a change: each set measures again, and the same text and width give the same values.
        if (FontSize != size)
        {
            SetCurrentValue(FontSizeProperty, size);
        }

        if (MaxLines != lines)
        {
            SetCurrentValue(MaxLinesProperty, lines);
        }

        if (Padding != padding)
        {
            SetCurrentValue(PaddingProperty, padding);
        }

        return base.MeasureOverride(availableSize);
    }

    private double WidthAt(double size)
    {
        using var line = Layout(size);
        return line.WidthIncludingTrailingWhitespace;
    }

    /// <summary>The text laid out at <paramref name="size"/>: one line, or wrapped into <paramref name="lines"/> within <paramref name="width"/>.</summary>
    private TextLayout Layout(double size, double width = double.PositiveInfinity, int lines = 1) =>
        new(Text ?? string.Empty, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), size, null,
            textWrapping: lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap, maxWidth: width, maxLines: lines);
}
