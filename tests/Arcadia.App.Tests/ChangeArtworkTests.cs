using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Launchers;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #490: "Artwork ändern" with the controller alone, from the detail page and the game menu: choosing the launcher's image,
/// an own file through the (fake) file dialog, the 20 MB limit, "Standard wiederherstellen", and the notice without a key.
/// </summary>
public sealed class ChangeArtworkTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static Game Installed(string title) =>
        TestGames.Installed(KnownLaunchers.Gog, title, installedAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [AvaloniaFact]
    [Trait("TestCase", "T490-1")]
    [Trait("TestCase", "T490-6")]
    [Trait("TestCase", "T490-8")]
    [Trait("TestCase", "T490-10")]
    [Trait("TestCase", "T490-31")]
    public async Task The_controller_alone_sets_and_restores_a_cover_from_the_detail_page_and_the_game_menu()
    {
        var alpha = Installed("Alpha");
        using var images = new LauncherImages();
        images.Set(alpha, ArtworkKind.Cover, TestImages.Png(600, 900));
        images.Set(alpha, ArtworkKind.Hero, TestImages.Png(1550, 500));
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Artwork: images)]);
        await SettledAsync(arcadia);
        var dialogs = UseDialogs(arcadia);

        // Detail page → "Artwork ändern": tab "Cover", the launcher's image in use, ticked and focused.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var modal = await OpenFromDetailPageAsync(arcadia);
        Assert.Equal("Alpha · GOG Galaxy", modal.GameLine);
        Assert.True(modal.IsCoverTab);
        var launcher = modal.Tiles[1];
        Assert.True(launcher is { IsInUse: true, IsFocused: true });
        Assert.Contains(modal.Facts, fact => fact.Value == "automatisch (Standard)");
        Assert.False(modal.CanRestore);

        // An own file through the file dialog: used at once, set by hand, shown everywhere.
        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        Assert.Equal(["Bild für das Cover auswählen"], dialogs.Opened);
        dialogs.Answer(Picture(arcadia, "Leuchtturm.png", TestImages.Png(400, 600)));
        await SettledAsync(arcadia, modal);
        Assert.True(modal.Tiles[0] is { IsInUse: true, HasTag: true, Caption: "Leuchtturm.png" });
        Assert.False(modal.Tiles[1].IsInUse);
        Assert.Contains(modal.Facts, fact => fact.Label == "Datei" && fact.Value == "Leuchtturm.png");
        Assert.True(modal.CanRestore);
        Assert.Equal((400, 600), (arcadia.Detail!.Current.Tile.Cover!.Size.Width, arcadia.Detail.Current.Tile.Cover.Size.Height));

        // Y gives the cover back to Arcadia's choice; the own file stays offered. The background was never touched.
        arcadia.Press(UiAction.Status);
        await SettledAsync(arcadia, modal);
        Assert.True(modal.Tiles[1].IsInUse);
        Assert.True(modal.Tiles[0] is { IsInUse: false, IsEmptyOwnFile: false });
        Assert.False(modal.CanRestore);
        Assert.Equal(900, arcadia.Detail.Current.Tile.Cover!.Size.Height);

        // RB to "Hintergrund", A on the launcher's image there: set by hand.
        arcadia.Press(UiAction.NextTab);
        await SettledAsync(arcadia, modal);
        Assert.True(modal.IsHeroTab);
        Assert.True(modal.Tiles[1] is { IsInUse: true, IsFocused: true });
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia, modal);
        Assert.True(modal.CanRestore);

        // B: back on the detail page; nothing to cancel.
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.NotNull(arcadia.Detail);

        // The game menu in the library leads to the same modal; B closes it again.
        arcadia.Press(UiAction.Back).Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!;
        while (menu.Actions.Single(a => a.IsFocused).Kind != GameActionKind.Artwork)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.GameMenu);
        Assert.NotNull(arcadia.Main.ChangeArtwork);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.ChangeArtwork);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-13")]
    [Trait("TestCase", "T490-15")]
    public async Task A_cancelled_dialog_and_a_file_over_20_mb_change_nothing()
    {
        var alpha = Installed("Alpha");
        using var images = new LauncherImages();
        images.Set(alpha, ArtworkKind.Cover, TestImages.Png(600, 900));
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Artwork: images)]);
        await SettledAsync(arcadia);
        var dialogs = UseDialogs(arcadia);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var modal = await OpenFromDetailPageAsync(arcadia);
        var before = arcadia.Detail!.Current.Tile.Cover;

        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        dialogs.Answer(null);
        await SettledAsync(arcadia, modal);
        Assert.True(modal.Tiles[0] is { IsEmptyOwnFile: true, IsFocused: true });

        arcadia.Press(UiAction.Accept);
        dialogs.Answer(Picture(arcadia, "huge.jpg", TestImages.JpegOfLength(400, 600, ImageCheck.MaxBytes + 1)));
        await SettledAsync(arcadia, modal);

        Assert.Equal("Das Bild ist größer als 20 MB und wurde nicht übernommen.", arcadia.Main.Toast.Text);
        Assert.True(modal.Tiles[0].IsEmptyOwnFile);
        Assert.True(modal.Tiles[1].IsInUse);
        Assert.Same(before, arcadia.Detail.Current.Tile.Cover);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-27")]
    [Trait("TestCase", "T490-4")]
    public async Task Without_a_key_the_notice_follows_the_tiles_and_leads_to_the_settings()
    {
        var alpha = Installed("Alpha");
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha])]);
        await SettledAsync(arcadia);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var modal = await OpenFromDetailPageAsync(arcadia);

        // A game without launcher images: only "Eigene Datei", the placeholder in use, Y greyed out.
        Assert.Single(modal.Tiles);
        Assert.True(modal.ShowsNotice);
        Assert.Equal("Mehr Vorschläge mit SteamGridDB", modal.Notice.Title);
        Assert.Equal("COVER · 2:3", modal.GridHeading);
        Assert.Equal([("Quelle", "Platzhalter")], modal.Facts.Select(f => (f.Label, f.Value)));
        Assert.False(modal.CanRestore);

        // The D-pad reaches the notice's button and goes round to the tile.
        arcadia.Press(UiAction.Right);
        Assert.True(modal.Notice.IsFocused);
        arcadia.Press(UiAction.Right);
        Assert.True(modal.Tiles[0].IsFocused);
        arcadia.Press(UiAction.Left).Press(UiAction.Accept);

        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.True(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-3")]
    [Trait("TestCase", "T490-14")]
    public async Task X_replaces_the_own_file_and_each_tab_keeps_its_own()
    {
        var alpha = Installed("Alpha");
        using var images = new LauncherImages();
        images.Set(alpha, ArtworkKind.Cover, TestImages.Png(600, 900));
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Artwork: images)]);
        await SettledAsync(arcadia);
        var dialogs = UseDialogs(arcadia);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var modal = await OpenFromDetailPageAsync(arcadia);

        // X does nothing until "Eigene Datei" holds a file and has the focus.
        arcadia.Press(UiAction.Search);
        Assert.Empty(dialogs.Opened);
        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        dialogs.Answer(Picture(arcadia, "first.png", TestImages.Png(400, 600)));
        await SettledAsync(arcadia, modal);
        Assert.True(modal.CanPickOtherFile);

        arcadia.Press(UiAction.Search);
        dialogs.Answer(Picture(arcadia, "second.jpg", TestImages.Jpeg(200, 300)));
        await SettledAsync(arcadia, modal);
        Assert.Equal("second.jpg", modal.Tiles[0].Caption);
        Assert.Equal(300, arcadia.Detail!.Current.Tile.Cover!.Size.Height);

        // The background has an own file of its own: none yet. LB and RB both switch, round at either end.
        arcadia.Press(UiAction.PreviousTab);
        await SettledAsync(arcadia, modal);
        Assert.True(modal is { IsHeroTab: true, Tiles: [{ IsEmptyOwnFile: true }] });
        arcadia.Press(UiAction.NextTab);
        Assert.True(modal.IsCoverTab);
        Assert.Equal("second.jpg", modal.Tiles[0].Caption);
    }

    private static async Task<ChangeArtworkViewModel> OpenFromDetailPageAsync(ArcadiaDriver arcadia)
    {
        var page = arcadia.Detail!.Current;
        for (var step = 0; step < 6 && page.FocusedKind != GameActionKind.Artwork; step++)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Press(UiAction.Accept);
        var modal = arcadia.Main.ChangeArtwork ?? throw new InvalidOperationException("Das Modal ist nicht offen.");
        await SettledAsync(arcadia, modal);
        return modal;
    }

    private static FakeSystemDialogs UseDialogs(ArcadiaDriver arcadia)
    {
        var dialogs = new FakeSystemDialogs();
        arcadia.Main.Dialogs = dialogs;
        return dialogs;
    }

    private static string Picture(ArcadiaDriver arcadia, string name, byte[] content)
    {
        var path = Path.Combine(arcadia.Folder, "Bilder", name);
        TestImages.Write(path, content);
        return path;
    }

    /// <summary>The artwork run and the modal's last choice have ended, and the views took what changed.</summary>
    private static async Task SettledAsync(ArcadiaDriver arcadia, ChangeArtworkViewModel? modal = null)
    {
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (modal is not null)
            {
                await modal.Loading.WaitAsync(TimeSpan.FromSeconds(10));
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
