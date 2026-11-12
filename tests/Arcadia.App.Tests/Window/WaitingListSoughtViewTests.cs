using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// "Warten auf deine Auswahl" drawn per game with what it seeks (#506, scenario 16): "Gesucht: …", the sentence that says
/// what Arcadia loads after the choice, and covers or backgrounds as tiles.
/// </summary>
public sealed class WaitingListSoughtViewTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T506-89")]
    public async Task Each_game_says_what_it_seeks_and_its_tiles_show_that_kind_of_image()
    {
        // Echo Drift has no image; Golden Crown has a fitting background; Iron Ember a fitting cover and a square background.
        var (echo, crown, ember) = (WaitingListTests.GameOf("echo", "Echo Drift"), WaitingListTests.GameOf("crown", "Golden Crown"), WaitingListTests.GameOf("ember", "Iron Ember"));
        using var launcherImages = new LauncherImages();
        launcherImages.Set(crown, ArtworkKind.Hero, TestImages.Png(1920, 620));
        launcherImages.Set(ember, ArtworkKind.Cover, TestImages.Png(600, 900));
        launcherImages.Set(ember, ArtworkKind.Hero, TestImages.Png(800, 800));
        var launcher = LauncherSpec.With(WaitingListTests.Other, echo, crown, ember) with { Artwork = launcherImages };
        using var window = await ArcadiaWindow.ShowAsync([launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        await WaitingListTests.FindNamesakesAsync(window.Arcadia);
        var list = WaitingListTests.OpenList(window.Arcadia);

        (string Title, string Sought, string Loads, bool Hero)[] expected =
        [
            ("Echo Drift", AppTexts.WaitingSoughtBoth, AppTexts.WaitingLoadsBoth, false),
            ("Golden Crown", AppTexts.WaitingSoughtCover, AppTexts.WaitingLoadsCover, false),
            ("Iron Ember", AppTexts.WaitingSoughtHero, AppTexts.WaitingLoadsHero, true),
        ];
        foreach (var (title, sought, loads, hero) in expected)
        {
            await WaitingListTests.WaitUntilAsync(() => list.Hits?.Title == title);
            await WaitingListTests.ShownAsync(list);
            window.Settle();

            Assert.NotEmpty(window.WithText(sought));
            Assert.NotEmpty(window.WithText(AppTexts.WaitingIntro(count: 2, loads: loads)));
            var tile = Assert.Single(
                window.Window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("waitingHit") && b.DataContext == list.Hits!.Hits[0])
                    .SelectMany(b => b.GetVisualDescendants().OfType<Border>()), border => border.Classes.Contains("artworkFrame"));
            var aspect = tile.Bounds.Width / tile.Bounds.Height;
            if (hero)
            {
                Assert.True(aspect > 1.5, $"{title}: Hintergrund-Kachel erwartet, Seitenverhältnis {aspect:0.00}");
            }
            else
            {
                Assert.Equal(2.0 / 3, aspect, 2);
            }

            window.Press(UiAction.Down);
        }
    }
}
