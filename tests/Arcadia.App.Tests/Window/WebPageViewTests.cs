using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>A launcher's web page as drawn (#280): over the whole window in window pixels, not scaled with the layout.</summary>
public sealed class WebPageViewTests
{
    [AvaloniaTheory]
    [Trait("TestCase", "T280-9")]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(1280, 1024)]
    public async Task The_page_fills_its_place_in_the_scaled_layout_at_any_window_size(int width, int height)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(width: width, height: height);
        arcadia.Named<WebPageHost>("WebPageView").OpensBrowser = false;
        ((IWebPageHost)arcadia.Arcadia.Main).Open(WebPageViewModel.SignIn("Bei Steam anmelden", new Uri("https://store.steampowered.com/login/"), "", _ => false));
        arcadia.Settle();

        var area = arcadia.Bounds(arcadia.Named<Border>("WebPageArea"));
        var view = arcadia.Bounds(arcadia.Named<WebPageHost>("WebPageView"));

        // The layout's 1760 × 832 between header and footer, scaled like the rest of the window.
        var scale = Math.Min(width / 1920.0, height / 1080.0);
        Assert.Equal(1760 * scale, area.Width, tolerance: 1);
        Assert.Equal(area.X, view.X, tolerance: 1);
        Assert.Equal(area.Y, view.Y, tolerance: 1);
        Assert.Equal(area.Width, view.Width, tolerance: 1);
        Assert.Equal(area.Height, view.Height, tolerance: 1);
    }
}
