using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// "Artwork ändern" drawn (#490): the focus ring whole on the first and last tile and the notice's button on both tabs,
/// tiles in their format, and the mouse: a click on a tile uses it, a click on a hint does what its button does.
/// </summary>
public sealed class ChangeArtworkViewTests
{
    private static async Task<(ArcadiaWindow Window, ChangeArtworkViewModel Modal, FakeSystemDialogs Dialogs)> OpenAsync(LauncherImages images)
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", ArcadiaDriver.Now);
        images.Set(alpha, ArtworkKind.Cover, TestImages.Png(600, 900));
        images.Set(alpha, ArtworkKind.Hero, TestImages.Png(1550, 500));
        var window = await ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Artwork: images)]);
        await SettledAsync(window, null);
        var dialogs = new FakeSystemDialogs();
        window.Arcadia.Main.Dialogs = dialogs;

        window.Press(UiAction.Options);
        var menu = window.Arcadia.Main.GameMenu!;
        while (menu.Actions.Single(a => a.IsFocused).Kind != GameActionKind.Artwork)
        {
            window.Press(UiAction.Down);
        }

        window.Press(UiAction.Accept);
        var modal = window.Arcadia.Main.ChangeArtwork!;
        await SettledAsync(window, modal);
        return (window, modal, dialogs);
    }

    private static Border Frame(ArcadiaWindow window, ArtworkTileViewModel tile) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("artworkFrame") && border.DataContext == tile);

    private static Button TileButton(ArcadiaWindow window, ArtworkTileViewModel tile) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("artworkTile") && button.DataContext == tile);

    [AvaloniaFact]
    [Trait("TestCase", "T490-2")]
    public async Task The_focus_ring_is_whole_on_every_tile_and_the_notice_and_tiles_have_their_format()
    {
        using var images = new LauncherImages();
        var (window, modal, _) = await OpenAsync(images);
        using var _window = window;

        var launcher = Frame(window, modal.Tiles[1]);
        Assert.Equal(2.0 / 3, launcher.Bounds.Width / launcher.Bounds.Height, 2);
        window.AssertRingAround(launcher, "Cover des Launchers");
        window.Press(UiAction.Left);
        window.AssertRingAround(Frame(window, modal.Tiles[0]), "Eigene Datei");
        window.Press(UiAction.Left);
        Assert.True(modal.Notice.IsFocused);
        // The tiles draw their ring on the image, not on the button: the pixel checks above see them; this one the notice.
        RingGeometry.AssertNoRingCutOff(window.Window, () => window.Settle().WaitForTransitions(), "Artwork ändern, Cover");

        window.Press(UiAction.NextTab);
        await SettledAsync(window, modal);
        var hero = Frame(window, modal.Tiles[1]);
        Assert.Equal(3.1, hero.Bounds.Width / hero.Bounds.Height, 1);
        // Two backgrounds a row: the launcher's stands beside "Eigene Datei".
        Assert.Equal(window.Bounds(Frame(window, modal.Tiles[0])).Top, window.Bounds(hero).Top, 0);
        window.AssertRingAround(hero, "Hintergrund des Launchers");
        RingGeometry.AssertNoRingCutOff(window.Window, () => window.Settle().WaitForTransitions(), "Artwork ändern, Hintergrund");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-5")]
    public async Task Clicks_use_a_tile_open_the_file_dialog_restore_and_close()
    {
        using var images = new LauncherImages();
        var (window, modal, dialogs) = await OpenAsync(images);
        using var _window = window;

        window.Click(TileButton(window, modal.Tiles[0]));
        Assert.Equal(["Bild für das Cover auswählen"], dialogs.Opened);
        dialogs.Answer(null);
        await SettledAsync(window, modal);

        window.Click(TileButton(window, modal.Tiles[1]));
        await SettledAsync(window, modal);
        Assert.True(modal.CanRestore);

        window.Click(window.OneWithText(AppTexts.ArtworkRestoreHint));
        await SettledAsync(window, modal);
        Assert.False(modal.CanRestore);

        window.Click(window.OneWithText(AppTexts.Close));
        Assert.Null(window.Arcadia.Main.ChangeArtwork);
    }

    private static async Task SettledAsync(ArcadiaWindow window, ChangeArtworkViewModel? modal)
    {
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await window.Arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (modal is not null)
            {
                await modal.Loading.WaitAsync(TimeSpan.FromSeconds(10));
            }

            window.Settle();
        }
    }
}
