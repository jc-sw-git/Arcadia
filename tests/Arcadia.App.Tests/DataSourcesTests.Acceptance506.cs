using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #506 part B, the QE's acceptance tests in "Datenquellen": a source switched off or on and a changed "Zuschneiden
/// erlaubt" change the images on disk at once, without a request.
/// </summary>
public sealed partial class DataSourcesTests
{
    private static string? ShownHero(ArcadiaDriver arcadia)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var tile = Assert.Single(arcadia.PageGames);
        return tile.Hero is { } hero && File.Exists(hero.Path) ? arcadia.Artwork.Of(tile.Game.Key)?.Hero?.Path : null;
    }

    /// <summary>
    /// The launcher's background (2560 wide, <paramref name="launcherHeroHeight"/> high) is on disk, SteamGridDB's fitting
    /// one in use, after the first key.
    /// </summary>
    private static async Task<(ArcadiaDriver Arcadia, FakeImageServer Images)> StartWithBothHeroesAsync(int launcherHeroHeight = 1440)
    {
        var (arcadia, images) = await StartAsync(wideHeroHeight: launcherHeroHeight);
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await SettledAsync(arcadia);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        lock (images.Requests)
        {
            images.Requests.Clear();
        }

        lock (arcadia.SteamGridDb.Requests)
        {
            arcadia.SteamGridDb.Requests.Clear();
        }

        return (arcadia, images);
    }

    private static int Requests(ArcadiaDriver arcadia, FakeImageServer images)
    {
        lock (images.Requests)
        {
            return images.Requests.Count + arcadia.SteamGridDb.Count;
        }
    }

    private static async Task SwitchAsync(ArcadiaDriver arcadia, ArtworkSourceKind source)
    {
        arcadia.FocusRow(ArtworkSourceRows.SourceKey(source));
        arcadia.Press(UiAction.Right);
        await SettledAsync(arcadia);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-60")]
    public async Task A_source_switched_off_and_on_swaps_the_images_on_disk_at_once_without_a_download()
    {
        var (arcadia, images) = await StartWithBothHeroesAsync();
        using var _ = arcadia;
        var steamGridDbHero = ShownHero(arcadia);
        Assert.NotNull(steamGridDbHero);

        // SteamGridDB off: the launcher's image takes its place; SteamGridDB's file stays on disk.
        await SwitchAsync(arcadia, ArtworkSourceKind.SteamGridDb);
        Assert.Equal("store-wide", Hero(arcadia));
        Assert.NotNull(ShownHero(arcadia));
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(arcadia.Folder, "artwork"), "off-hero.*", SearchOption.AllDirectories));

        // On again: the same image back, nothing downloaded.
        await SwitchAsync(arcadia, ArtworkSourceKind.SteamGridDb);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        Assert.NotNull(ShownHero(arcadia));

        // The launcher's images off and on: SteamGridDB's stays, nothing downloaded either.
        await SwitchAsync(arcadia, ArtworkSourceKind.Launcher);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        await SwitchAsync(arcadia, ArtworkSourceKind.Launcher);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));

        // Both off: the placeholder.
        await SwitchAsync(arcadia, ArtworkSourceKind.Launcher);
        await SwitchAsync(arcadia, ArtworkSourceKind.SteamGridDb);
        Assert.Null(Assert.Single(arcadia.PageGames).Hero);

        Assert.Equal(0, Requests(arcadia, images));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-50")]
    public async Task A_changed_crop_tolerance_picks_again_among_the_images_on_disk_at_once_without_a_request()
    {
        // The launcher's background cuts 20 %: more than "Normal" allows, within "Viel".
        var (arcadia, images) = await StartWithBothHeroesAsync(launcherHeroHeight: 1033);
        using var _ = arcadia;

        // "Viel": the launcher's background, first in the order, fits now and is shown at once.
        var crop = Assert.IsType<ChoiceRowViewModel>(arcadia.FocusRow("artwork-crop"));
        arcadia.Press(UiAction.Right);
        Assert.Equal(CropAllowance.Much, arcadia.Settings.Current.Artwork.Crop);
        await SettledAsync(arcadia);
        Assert.Equal("store-wide", Hero(arcadia));
        Assert.NotNull(ShownHero(arcadia));

        // Back to "Normal": SteamGridDB's again, at once.
        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.DataSourcesCropNormal, crop.ValueText);
        await SettledAsync(arcadia);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));

        Assert.Equal(0, Requests(arcadia, images));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-54")]
    public async Task Suche_abbrechen_stops_the_search_within_the_request_in_flight_and_keeps_what_was_found()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Cover(ArtworkStoreId.Steam, GameId, "https://cdn.example/grid/4100001.png", 600, 900);
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ArtworkSourceRows.MissingKey));

        // "Jetzt suchen" hangs at SteamGridDB; A on "Suche abbrechen" stops it without waiting for the timeout.
        arcadia.SteamGridDb.Hangs = true;
        var before = arcadia.SteamGridDb.Count;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.SteamGridDb.Count > before, "Anfrage hängt");
        var asked = arcadia.SteamGridDb.Count;
        Assert.Equal(AppTexts.DataSourcesSearchCancel, missing.ActionText);
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);

        Assert.Equal(asked, arcadia.SteamGridDb.Count);
        Assert.Equal(AppTexts.DataSourcesSearchNow, missing.ActionText);
        Assert.Equal(AppTexts.DataSourcesMissingCounts(covers: 1, heroes: 0), missing.Subtitle);
        Assert.False(missing.HasProgress);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        Assert.NotNull(ShownHero(arcadia));
    }
}
