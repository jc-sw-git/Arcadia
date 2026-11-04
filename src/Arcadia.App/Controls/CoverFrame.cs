using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// Keeps a cover at 2 : 3 (#24): its height follows the width it is given, as a box art's does. Used where the width
/// comes from the layout (the library's grid of 8 columns); fixed covers (search, detail page) are simply sized 2 : 3.
/// </summary>
public sealed class CoverFrame : Decorator
{
    /// <summary>Height to width of a cover, as on Steam's and SteamGridDB's box art (600 × 900).</summary>
    public const double HeightPerWidth = 1.5;

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = CoverSize(availableSize.Width);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = CoverSize(finalSize.Width);
        Child?.Arrange(new Rect(size));
        return size;
    }

    // Without a width to follow (never in Arcadia's grids), the cover takes nothing rather than an infinite height.
    private static Size CoverSize(double width) =>
        double.IsFinite(width) ? new Size(width, width * HeightPerWidth) : default;
}
