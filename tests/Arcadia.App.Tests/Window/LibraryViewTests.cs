using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;
using Arcadia.Launchers.Steam;
using Arcadia.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The library's covers, preview and launcher logos as drawn.</summary>
public sealed class LibraryViewTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T41-1")]
    public async Task A_game_not_installed_is_greyed_with_the_install_symbol_and_the_preview_offers_to_install_it()
    {
        var gog = KnownLaunchers.Gog;
        using var arcadia = await ArcadiaWindow.ShowAsync(
            [LauncherSpec.With(gog, TestGames.Installed(gog, "Alpha", ArcadiaDriver.Now.AddDays(-2)), TestGames.NotInstalled(gog, "Beta"))]);
        var main = arcadia.Arcadia.Main;
        var installed = arcadia.Tile(arcadia.Arcadia.PageGames[0]);
        var missing = arcadia.Tile(arcadia.Arcadia.PageGames[1]);

        Assert.False(Shade(installed).IsEffectivelyVisible);
        Assert.True(Shade(missing).IsEffectivelyVisible, "Nicht installiert: ausgegraut");
        Assert.Empty(VisibleBadges(installed));
        Assert.Equal("Nicht installiert – A installiert", ToolTip.GetTip(Assert.Single(VisibleBadges(missing))));

        // Greyed for real: the shade draws the cover darker than the same colour without it.
        using (var frame = arcadia.Capture())
        {
            var cover = ((ISolidColorBrush)missing.Background!).Color;
            var drawn = frame.At(arcadia.Bounds(missing).Center);
            Assert.True(Brightness(drawn) < Brightness(cover) - 5, $"Cover {drawn} nicht dunkler als {cover}");
        }

        arcadia.Press(UiAction.Right);
        Assert.NotEmpty(arcadia.WithText("Nicht installiert"));
        Assert.Contains(arcadia.WithText("Installieren"), text => text.GetVisualAncestors().OfType<Button>().Any());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-1")]
    public async Task Steam_and_Epic_show_their_logo_everywhere_a_launcher_is_named_and_others_keep_their_letter()
    {
        var steam = new SteamIntegration(TimeProvider.System).Launcher;
        var epic = new EpicIntegration(TimeProvider.System).Launcher;
        using var arcadia = await ArcadiaWindow.ShowAsync([new(steam, 3), new(epic, 3), new(KnownLaunchers.Gog, 3)]);
        var main = arcadia.Arcadia.Main;

        // Tabs: the selected one dark on its light tab, the others light on dark.
        var tabs = arcadia.Named<ItemsControl>("TabList");
        var tabLogos = Logos(tabs);
        Assert.Equal([true, true, false], tabLogos.Select(IsShape));
        Assert.True(Brightness(tabLogos[0]) < 128, "Logo auf dem gewählten Tab dunkel");
        Assert.True(Brightness(tabLogos[1]) > 128, "Logo auf einem anderen Tab hell");
        Assert.Equal("G", tabLogos[2].Badge?.Monogram);
        AssertDrawn(arcadia, "Tabs", tabLogos);

        // Preview, detail page.
        var preview = Logos(arcadia.Window).Where(logo => !tabs.IsVisualAncestorOf(logo)).ToList();
        AssertShapeIn(arcadia, "Vorschau", preview);
        AssertDrawn(arcadia, "Vorschau", preview);
        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        arcadia.Settle();
        var detail = Logos(arcadia.Window).Where(logo => !tabs.IsVisualAncestorOf(logo) && logo.Badge?.Shape is not null && arcadia.Bounds(logo).Top > 300).ToList();
        AssertShapeIn(arcadia, "Detailseite", detail);
        // Drawn is looked at on the page in the middle; its neighbours stand dimmed, cut off at the edges.
        AssertDrawn(arcadia, "Detailseite", detail.Where(logo => logo.GetVisualAncestors().OfType<Panel>()
            .Any(page => page.Classes.Contains("detailPage") && page.DataContext is GameDetailPageViewModel { IsCentre: true })));
        arcadia.Press(UiAction.Back);

        // Search: the hits on their dark plate and the filter chips.
        arcadia.Press(UiAction.Search).Key(Key.A);
        var hits = Logos(arcadia.Window).Where(logo => logo.Classes.Contains("onCover") && logo.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits, logo => Assert.Equal(logo.Badge?.Monogram is "S" or "E", IsShape(logo)));
        Assert.All(hits.Where(IsShape), logo => Assert.True(Brightness(logo) > 128, "Logo auf dunklem Grund hell"));
        var chips = Logos(arcadia.Window).Where(logo => logo.Classes.Contains("chipLogo")).ToList();
        AssertLogosOfAll(chips);
        AssertDrawn(arcadia, "Suche", hits.Concat(chips));

        // The settings' list of launchers.
        arcadia.Press(UiAction.Back).Press(UiAction.Back).Press(UiAction.Settings);
        Assert.True(main.IsSettingsOpen);
        var listed = Logos(arcadia.Window).Where(logo => logo.Classes.Contains("launcherLogo")).ToList();
        AssertLogosOfAll(listed);
        AssertDrawn(arcadia, "Einstellungen", listed);
    }

    private static Border Shade(Button tile) => tile.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("shade"));

    private static List<Border> VisibleBadges(Button tile) =>
        tile.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("installBadge") && border.IsEffectivelyVisible).ToList();

    private static List<LauncherLogo> Logos(Visual within) => within.GetVisualDescendants().OfType<LauncherLogo>()
        .Where(logo => logo.IsEffectivelyVisible && logo.Badge is not null)
        .ToList();

    private static bool IsShape(LauncherLogo logo) => logo.Badge?.Shape is not null;

    /// <summary>Each of the three launchers is named, Steam (S) and Epic (E) with their logo, GOG with its letter.</summary>
    private static void AssertLogosOfAll(IReadOnlyList<LauncherLogo> logos)
    {
        Assert.Equal(["E", "G", "S"], logos.Select(logo => logo.Badge!.Monogram).Distinct().Order());
        Assert.All(logos, logo => Assert.Equal(logo.Badge!.Monogram is "S" or "E", IsShape(logo)));
    }

    private static void AssertShapeIn(ArcadiaWindow arcadia, string where, IEnumerable<LauncherLogo> logos) =>
        Assert.True(logos.Any(IsShape), $"{where}: kein Steam-Logo");

    /// <summary>
    /// The shapes of <paramref name="logos"/> are drawn and stand out: part of each logo's square is in its colour, part
    /// clearly not (the ground it stands on).
    /// </summary>
    private static void AssertDrawn(ArcadiaWindow arcadia, string where, IEnumerable<LauncherLogo> logos)
    {
        var shapes = logos.Where(IsShape).ToList();
        Assert.True(shapes.Count > 0, $"{where}: kein Logo zum Ansehen");
        using var frame = arcadia.Capture();
        foreach (var logo in shapes)
        {
            var bounds = arcadia.Bounds(logo);
            var ink = ((ISolidColorBrush)logo.Foreground!).Color;
            var pixels = new List<Color>();
            for (var y = (int)Math.Ceiling(bounds.Top * frame.Scaling); y < (int)(bounds.Bottom * frame.Scaling); y++)
            {
                for (var x = (int)Math.Ceiling(bounds.Left * frame.Scaling); x < (int)(bounds.Right * frame.Scaling); x++)
                {
                    pixels.Add(frame.At(x, y));
                }
            }

            var inked = pixels.Count(colour => ArcadiaWindow.Distance(colour, ink) <= 60);
            var ground = pixels.Count(colour => ArcadiaWindow.Distance(colour, ink) > 150);
            Assert.True(
                inked >= pixels.Count / 10 && ground >= pixels.Count / 10,
                $"{where}: Logo {logo.Badge!.Monogram} nicht zu sehen ({inked} in seiner Farbe, {ground} Grund von {pixels.Count} Pixeln)");
        }
    }

    private static double Brightness(LauncherLogo logo) => Brightness(((ISolidColorBrush)logo.Foreground!).Color);

    private static double Brightness(Color colour) => (0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B);
}
