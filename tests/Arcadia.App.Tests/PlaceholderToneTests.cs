using Arcadia.App.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Arcadia.App.Tests;

/// <summary>#377: the placeholders' saturation and lightness come from the theme, the hue from the title.</summary>
public sealed class PlaceholderToneTests
{
    [AvaloniaFact]
    public void A_cover_without_artwork_takes_saturation_and_lightness_from_the_theme()
    {
        var (background, accent) = Formatting.CoverBrushes("Example Game");

        var backgroundHsl = ((ISolidColorBrush)background).Color.ToHsl();
        var accentHsl = ((ISolidColorBrush)accent).Color.ToHsl();
        Assert.Equal(0.28, backgroundHsl.S, tolerance: 0.015);
        Assert.Equal(0.20, backgroundHsl.L, tolerance: 0.015);
        Assert.Equal(0.55, accentHsl.S, tolerance: 0.015);
        Assert.Equal(0.66, accentHsl.L, tolerance: 0.015);
        Assert.Equal(backgroundHsl.H, accentHsl.H, tolerance: 2);
    }

    [AvaloniaFact]
    public void Two_titles_differ_in_their_hue_only()
    {
        var first = ((ISolidColorBrush)Formatting.LauncherTileBrush("steam")).Color.ToHsl();
        var second = ((ISolidColorBrush)Formatting.LauncherTileBrush("epic")).Color.ToHsl();

        Assert.True(Math.Abs(first.H - second.H) > 2);
        Assert.Equal(first.S, second.S, tolerance: 0.015);
        Assert.Equal(first.L, second.L, tolerance: 0.015);
    }
}
