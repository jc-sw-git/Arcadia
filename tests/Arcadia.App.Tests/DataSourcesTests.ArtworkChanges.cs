using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Tests.Artwork;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #489 with #490: SteamGridDB's images in "Artwork ändern", the way from there to the key, and "Alle Artworks neu laden"
/// with "Von Hand gesetzte Artworks überschreiben" in "Datenquellen".
/// </summary>
public sealed partial class DataSourcesTests
{
    private const string LowCover = "https://cdn.example/grid/4100001-low.png";
    private const string HighCover = "https://cdn.example/grid/4100001-high.png";

    [AvaloniaFact]
    [Trait("TestCase", "T490-29")]
    [Trait("TestCase", "T490-30")]
    public async Task With_a_valid_key_artwork_aendern_offers_steamgriddbs_images_and_A_sets_one_by_hand()
    {
        var (arcadia, images) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Cover("steam", GameId, LowCover, 600, 900, score: 2).Cover("steam", GameId, HighCover, 600, 900, score: 7);
        images.Serve(LowCover, TestImages.Png(600, 900));
        images.Serve(HighCover, TestImages.Png(600, 900));
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        Assert.Equal(HighCover, Cached(arcadia)!.Cover!.Stamp);
        arcadia.Press(UiAction.Back);

        var modal = await OpenChangeArtworkAsync(arcadia);

        Assert.False(modal.ShowsNotice);
        var offered = modal.Tiles.Where(tile => tile.Offer?.Source == ArtworkSource.SteamGridDb).ToList();
        Assert.Equal([HighCover, LowCover], offered.Select(tile => tile.Offer!.Stamp));
        Assert.EndsWith(AppTexts.ArtworkSuggestionCount(2), modal.GridHeading, StringComparison.Ordinal);
        Assert.True(offered[0].IsInUse);
        Assert.All(arcadia.SteamGridDb.Requests, request => Assert.Equal(("Bearer", FakeSteamGridDb.Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter)));

        // A on the lower rated one: used at once and set by hand.
        while (!offered[1].IsFocused)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Press(UiAction.Accept);
        await ChangedAsync(arcadia, modal);
        var cover = Cached(arcadia)!.Cover!;
        Assert.Equal((LowCover, ArtworkSource.SteamGridDb, true), (cover.Stamp, cover.Source, cover.SetByHand));
        Assert.True(offered[1].IsInUse);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T532-1")]
    public async Task The_launchers_image_in_use_can_be_chosen_again_after_a_steamgriddb_image()
    {
        var (arcadia, _) = await StartAsync(wideHeroHeight: 826);
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        var modal = await OpenChangeArtworkAsync(arcadia);
        arcadia.Press(UiAction.NextTab);
        await ChangedAsync(arcadia, modal);
        var launcher = modal.Tiles.Single(tile => tile.Offer?.Source == ArtworkSource.Launcher);
        var grid = modal.Tiles.Single(tile => tile.Offer?.Source == ArtworkSource.SteamGridDb);
        Assert.True(launcher.IsInUse);

        Pick(arcadia, modal, grid);
        await ChangedAsync(arcadia, modal);
        Assert.True(grid.IsInUse);
        Pick(arcadia, modal, launcher);
        await ChangedAsync(arcadia, modal);

        Assert.NotEqual(AppTexts.ArtworkOfferFailed, arcadia.Main.Toast.Text);
        Assert.True(launcher.IsInUse);
        Assert.Equal((ArtworkSource.Launcher, true), (Cached(arcadia)!.Hero!.Source, Cached(arcadia)!.Hero!.SetByHand));
        Assert.Equal(826, arcadia.Main.ChangeArtwork!.Tile.Hero!.Size.Height);
    }

    private static void Pick(ArcadiaDriver arcadia, ChangeArtworkViewModel modal, ArtworkTileViewModel tile)
    {
        while (!tile.IsFocused)
        {
            arcadia.Press(modal.Tiles.IndexOf(tile) > modal.Tiles.ToList().FindIndex(t => t.IsFocused) ? UiAction.Right : UiAction.Left);
        }

        arcadia.Press(UiAction.Accept);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T532-3")]
    public async Task While_the_images_are_looked_for_the_modal_shows_the_loading_state_with_a_changing_text()
    {
        var (arcadia, images) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Cover("steam", GameId, HighCover, 600, 900);
        images.Serve(HighCover, TestImages.Png(600, 900));
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        arcadia.Press(UiAction.Back);
        images.Hang(HighCover);

        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Artwork);
        arcadia.Press(UiAction.Accept);
        var modal = arcadia.Main.ChangeArtwork!;
        await images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(modal.IsLoading);
        Assert.Empty(modal.GridItems);
        Assert.Contains(modal.LoadingText, ChangeArtworkViewModel.LoadingTexts);
        var first = modal.LoadingText;
        arcadia.Time.Advance(ChangeArtworkViewModel.LoadingTextTime);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(first, modal.LoadingText);
        Assert.Contains(modal.LoadingText, ChangeArtworkViewModel.LoadingTexts);

        // The download gives up after its time: the tiles there are replace the loading state.
        arcadia.Time.Advance(ArtworkService.DownloadTimeout);
        await ChangedAsync(arcadia, modal);
        Assert.False(modal.IsLoading);
        Assert.NotEmpty(modal.Tiles);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T532-5")]
    [Trait("TestCase", "T532-6")]
    public async Task A_steamgriddb_image_in_use_shows_its_format_and_no_rating()
    {
        var (arcadia, images) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Cover("steam", GameId, HighCover, 600, 900, score: 7);
        images.Serve(HighCover, TestImages.Png(600, 900));
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        var modal = await OpenChangeArtworkAsync(arcadia);

        var offered = Assert.Single(modal.Tiles, tile => tile.Offer?.Source == ArtworkSource.SteamGridDb);
        Assert.True(offered is { IsInUse: true, HasTag: false });
        Assert.Equal(
            [(AppTexts.ArtworkSourceRow, "SteamGridDB"), (AppTexts.ArtworkFormatRow, "600 × 900"), (AppTexts.ArtworkSetRow, AppTexts.ArtworkSetAutomatically)],
            modal.Facts.Select(fact => (fact.Label, fact.Value)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-28")]
    public async Task With_a_key_steamgriddb_turns_down_the_notice_says_so_and_no_suggestion_is_asked()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.ValidKey = WrongKey;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        arcadia.Press(UiAction.Back);
        var asked = arcadia.SteamGridDb.Count;

        var modal = await OpenChangeArtworkAsync(arcadia);

        Assert.True(modal.ShowsNotice);
        Assert.Equal(AppTexts.ArtworkBadKeyTitle, modal.Notice.Title);
        Assert.DoesNotContain(modal.Tiles, tile => tile.Offer?.Source == ArtworkSource.SteamGridDb);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-27")]
    public async Task Zu_den_Datenquellen_opens_the_category_on_the_key_and_B_goes_back()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        var modal = await OpenChangeArtworkAsync(arcadia);
        Assert.True(modal.ShowsNotice);
        Assert.Equal(0, arcadia.SteamGridDb.Count);

        while (!modal.Notice.IsFocused)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Press(UiAction.Accept);

        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(SteamGridDbKeyRows.KeyRowKey, arcadia.Main.Settings.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.True(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T490-21")]
    [Trait("TestCase", "T490-22")]
    public async Task Alle_Artworks_neu_laden_asks_first_follows_the_switch_and_shows_its_progress()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var overwrite = Assert.IsType<SwitchRowViewModel>(Row(arcadia, ArtworkReloadRows.OverwriteKey));
        var reload = Row(arcadia, ArtworkReloadRows.ReloadKey);
        Assert.Equal((false, AppTexts.DataSourcesOverwriteSubtitle, AppTexts.DataSourcesReloadKeeps), (overwrite.IsOn, overwrite.Subtitle, reload.Subtitle));

        // B on the question changes nothing.
        arcadia.FocusRow(ArtworkReloadRows.ReloadKey);
        arcadia.Press(UiAction.Accept);
        var question = Assert.IsType<SettingsQuestionViewModel>(arcadia.Main.Settings!.Selected.Dialog);
        Assert.Equal((AppTexts.DataSourcesReloadAsk, AppTexts.DataSourcesReloadAskKeeps(1)), (question.Title, question.Message));
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings.Selected.Dialog);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.Null(arcadia.Artwork.Search.Running);

        // The switch on: the texts follow and it is kept after a restart.
        arcadia.FocusRow(ArtworkReloadRows.OverwriteKey);
        arcadia.Press(UiAction.Accept);
        Assert.True(arcadia.Settings.Current.OverwritesArtworkSetByHand);
        Assert.Equal(AppTexts.DataSourcesReloadReplaces, Row(arcadia, ArtworkReloadRows.ReloadKey).Subtitle);

        // "Neu laden" while SteamGridDB takes its time: the progress row and the greyed out button.
        arcadia.SteamGridDb.Hangs = true;
        arcadia.FocusRow(ArtworkReloadRows.ReloadKey);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(AppTexts.DataSourcesReloadAskReplaces(1), Assert.IsType<SettingsQuestionViewModel>(arcadia.Main.Settings.Selected.Dialog).Message);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Settings.Selected.Dialog);
        await arcadia.WaitUntilAsync(() => arcadia.Artwork.Search.Running is { Reload: true }, "Neu laden läuft");
        await arcadia.WaitUntilAsync(() => { Dispatcher.UIThread.RunJobs(); return Row(arcadia, ArtworkSourceRows.MissingKey).Subtitle == AppTexts.DataSourcesReloadRunning(done: 0, total: 1); }, "Fortschritt");
        var reloadRow = Assert.IsType<ActionRowViewModel>(Row(arcadia, ArtworkReloadRows.ReloadKey));
        Assert.Equal((AppTexts.DataSourcesReloadCancel, true), (reloadRow.ActionText, reloadRow.CanActivate));
        Assert.False(Row(arcadia, ArtworkSourceRows.MissingKey).CanActivate);

        using var restarted = await arcadia.RestartAsync();
        OpenDataSources(restarted);
        Assert.True(Assert.IsType<SwitchRowViewModel>(Row(restarted, ArtworkReloadRows.OverwriteKey)).IsOn);
    }

    private static CachedArtwork? Cached(ArcadiaDriver arcadia) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(Store.Id + ":" + GameId);

    /// <summary>The library's game: A held opens its detail page, then its action "Artwork ändern".</summary>
    private static async Task<ChangeArtworkViewModel> OpenChangeArtworkAsync(ArcadiaDriver arcadia)
    {
        if (arcadia.Main.IsSettingsOpen)
        {
            arcadia.Press(UiAction.Back);
        }

        if (!arcadia.Main.IsDetailOpen)
        {
            arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        }

        // The detail page's areas (#499): "Artwork ändern" is in the area "Verwalten".
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Artwork);
        arcadia.Press(UiAction.Accept);
        var modal = arcadia.Main.ChangeArtwork ?? throw new InvalidOperationException("Das Modal ist nicht offen.");
        await ChangedAsync(arcadia, modal);
        return modal;
    }

    private static async Task ChangedAsync(ArcadiaDriver arcadia, ChangeArtworkViewModel modal)
    {
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await modal.Loading.WaitAsync(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
        }
    }
}
