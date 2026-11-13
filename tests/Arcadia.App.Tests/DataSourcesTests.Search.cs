using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #506 part B in "Datenquellen": the first usable key searches, another key doesn't; "Suche abbrechen"; SteamGridDB
/// switched off greys out what needs it and "Artwork ändern" leads to its row; the button hints of a lifted source.
/// </summary>
public sealed partial class DataSourcesTests
{
    private const string OtherKey = "abcdefabcdefabcdefabcdefabcdef12";

    private static int AskedForGrids(ArcadiaDriver arcadia) => arcadia.SteamGridDb.Asked().Count(path => path == "grids/steam/" + GameId);

    private static void SwitchSteamGridDb(ArcadiaDriver arcadia)
    {
        arcadia.FocusRow(ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb));
        arcadia.Press(UiAction.Right);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-46")]
    [Trait("TestCase", "T506-47")]
    public async Task The_first_valid_key_searches_every_game_missing_an_image_and_another_key_searches_nothing()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        Assert.Equal(0, arcadia.SteamGridDb.Count);

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        Assert.Equal(AppTexts.DataSourcesKeySavedSearching, arcadia.Main.Toast.Text);
        Assert.Equal(1, AskedForGrids(arcadia));

        // Another valid key, then the same again: the usual toast, only the key check is sent.
        arcadia.SteamGridDb.ValidKey = OtherKey;
        var asked = arcadia.SteamGridDb.Count;
        await TypeKeyAsync(arcadia, OtherKey);
        Assert.Equal(AppTexts.DataSourcesKeySaved, arcadia.Main.Toast.Text);
        await TypeKeyAsync(arcadia, OtherKey);
        Assert.Equal(1, AskedForGrids(arcadia));
        Assert.Equal(2, arcadia.SteamGridDb.Count - asked);

        // "Schlüssel löschen" and a key again: a first usable key once more.
        arcadia.FocusRow("steamgriddb-delete");
        arcadia.Press(UiAction.Accept);
        await TypeKeyAsync(arcadia, OtherKey);
        Assert.Equal(AppTexts.DataSourcesKeySavedSearching, arcadia.Main.Toast.Text);
        Assert.Equal(2, AskedForGrids(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-63")]
    public async Task A_first_key_while_steamgriddb_is_off_is_saved_and_starts_no_search()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        SwitchSteamGridDb(arcadia);
        Assert.Equal([ArtworkSourceKind.SteamGridDb], arcadia.Settings.Current.Artwork.Off);

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        Assert.Equal(AppTexts.DataSourcesKeySaved, arcadia.Main.Toast.Text);
        Assert.True(arcadia.Artwork.SteamGridDb!.IsUsable);
        Assert.Equal(0, AskedForGrids(arcadia));

        // Switched on: nothing starts on its own either (Q1).
        SwitchSteamGridDb(arcadia);
        await SettledAsync(arcadia);
        Assert.Equal(0, AskedForGrids(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-48")]
    public async Task A_first_key_without_network_still_searches_and_the_games_wait_for_Jetzt_suchen()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        arcadia.SteamGridDb.Offline = true;

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        Assert.Equal(AppTexts.DataSourcesKeyKeptNoConnection, arcadia.Main.Toast.Text);
        Assert.Equal(1, AskedForGrids(arcadia));
        arcadia.SteamGridDb.Offline = false;

        // A rescan asks nothing; "Jetzt suchen" does.
        await arcadia.RescanAsync();
        await SettledAsync(arcadia);
        Assert.Equal(1, AskedForGrids(arcadia));
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: true));
        await SettledAsync(arcadia);
        Assert.Equal(2, AskedForGrids(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-53")]
    public async Task The_info_of_the_order_and_the_switches_say_the_images_change_at_once()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);

        var source = Row(arcadia, ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher));
        Assert.Contains(Texts(source.Info), text => text == AppTexts.DataSourcesOrderWhen);
        Assert.Equal([AppTexts.DataSourcesSourceOffInfo, AppTexts.DataSourcesSourceOnInfo], source.ValueBoxes.Select(box => box.Text));
        Assert.All(Texts(source.Info), text => Assert.DoesNotContain("nächsten Suche", text, StringComparison.Ordinal));
    }

    private static IEnumerable<string> Texts(IEnumerable<InfoBlock> blocks) => blocks.SelectMany(block => block switch
    {
        InfoText text => (IEnumerable<string>)[text.Text],
        InfoSection section => Texts(section.Blocks),
        _ => [],
    });

    [AvaloniaFact]
    [Trait("TestCase", "T506-58")]
    public async Task A_twice_on_Jetzt_suchen_cancels_the_search_it_started_and_the_button_reads_Jetzt_suchen_again()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ArtworkSourceRows.MissingKey));
        Assert.Equal(AppTexts.DataSourcesSearchNow, missing.ActionText);
        var asked = AskedForGrids(arcadia);
        arcadia.SteamGridDb.Hangs = true;

        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        arcadia.SteamGridDb.Hangs = false;
        arcadia.Time.Advance(SteamGridDbClient.Timeout);
        await SettledAsync(arcadia);

        Assert.True(AskedForGrids(arcadia) - asked <= 1);
        Assert.Null(arcadia.Artwork.Search.Running);
        Assert.Equal(AppTexts.DataSourcesSearchNow, missing.ActionText);

        // "Suche abbrechen" once the run is over: nothing happens.
        Assert.False(arcadia.Artwork.CancelSearch());
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-62")]
    public async Task SteamGridDB_off_greys_out_Jetzt_suchen_and_the_waiting_list_and_keeps_the_key()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        var asked = arcadia.SteamGridDb.Count;

        SwitchSteamGridDb(arcadia);
        await SettledAsync(arcadia);

        var missing = Assert.IsType<ActionRowViewModel>(Row(arcadia, ArtworkSourceRows.MissingKey));
        Assert.False(missing.CanActivate);
        Assert.Contains(new InfoText(AppTexts.DataSourcesMissingInfoSteamGridDbOff), missing.Info);
        var waiting = Row(arcadia, SeveralHitsRows.WaitingKey(WaitingKind.Artwork));
        Assert.Equal(AppTexts.DataSourcesWaitingSteamGridDbOff, waiting.Subtitle);
        Assert.False(waiting.CanActivate);
        Assert.Equal(AppTexts.DataSourcesKeyConnected, Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);

        // "Alle Artworks neu laden" stays usable and asks SteamGridDB nothing.
        Assert.True(Row(arcadia, ArtworkReloadRows.ReloadKey).CanActivate);
        Assert.True(arcadia.Artwork.ReloadAll(overwriteSetByHand: false));
        await SettledAsync(arcadia);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);

        // On again: Jetzt suchen can be pressed, no run starts on its own.
        SwitchSteamGridDb(arcadia);
        await SettledAsync(arcadia);
        Assert.True(Row(arcadia, ArtworkSourceRows.MissingKey).CanActivate);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-66")]
    public async Task The_button_hints_follow_whether_a_source_is_lifted()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        var category = arcadia.Main.Settings!.Selected;
        var source = arcadia.FocusRow(ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher));

        Assert.True(source.CanChange);
        Assert.Equal((AppTexts.Move, false), (category.L3ActionText, category.IsReordering));

        arcadia.Press(UiAction.ViewOptions);
        Assert.True(category.IsReordering);
        arcadia.Press(UiAction.Back);
        Assert.False(category.IsReordering);

        // Rows that aren't sources have no L3 action.
        arcadia.FocusRow(ArtworkSourceRows.MissingKey);
        Assert.Null(category.L3ActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-67")]
    public async Task With_steamgriddb_off_artwork_aendern_asks_nothing_and_its_notice_leads_to_the_source_row()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        SwitchSteamGridDb(arcadia);
        await SettledAsync(arcadia);
        arcadia.Press(UiAction.Back);
        var asked = arcadia.SteamGridDb.Count;

        var modal = await OpenChangeArtworkAsync(arcadia);

        Assert.True(modal.ShowsNotice);
        Assert.Equal(AppTexts.ArtworkSteamGridDbOffTitle, modal.Notice.Title);
        Assert.DoesNotContain(modal.Tiles, tile => tile.Offer?.Source == ArtworkSource.SteamGridDb);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);

        while (!modal.Notice.IsFocused)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Press(UiAction.Accept);
        Assert.Equal(ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb), arcadia.Main.Settings!.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsDetailOpen);
    }
}
