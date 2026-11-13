using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #489: the settings category "Datenquellen": the SteamGridDB key by controller, its warning, the order of the sources,
/// "Zuschneiden erlaubt" and the search for missing artwork, through the view models with a fake SteamGridDB.
/// </summary>
public sealed partial class DataSourcesTests
{
    private const string GameId = "4100001";
    private const string WideHero = "https://images.example/4100001/wide.jpg";
    private const string GridHero = "https://cdn.example/hero/4100001.png";
    private const string WrongKey = "ffffffffffffffffffffffffffffffff";

    private static readonly LauncherInfo Store = new("store", "Shop", "shop", null);

    /// <summary>
    /// A launcher whose one game has a 16 : 9 background (as Epic's; another height with <paramref name="wideHeroHeight"/>)
    /// and a store id SteamGridDB knows.
    /// </summary>
    private static async Task<(ArcadiaDriver Arcadia, FakeImageServer Images)> StartAsync(AppSettings? settings = null, int wideHeroHeight = 1440)
    {
        var images = new FakeImageServer();
        images.Serve(WideHero, TestImages.Png(2560, wideHeroHeight));
        images.Serve(GridHero, TestImages.Png(1920, 620));
        var game = new Game
        {
            Id = GameId,
            LauncherId = Store.Id,
            Title = "Leuchtturm",
            Launch = new LaunchTarget.ProtocolUri("shop://run/" + GameId),
        };
        var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Store, game) with { Artwork = new StoreArtwork() }],
            settings ?? new AppSettings { PanEnabled = true, ActiveLaunchers = [Store.Id] },
            imageServer: images);
        arcadia.SteamGridDb.Hero(ArtworkStoreId.Steam, GameId, GridHero, 1920, 620);
        await SettledAsync(arcadia);
        return (arcadia, images);
    }

    private static async Task SettledAsync(ArcadiaDriver arcadia)
    {
        await arcadia.Artwork.WhenIdleAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Artwork.Search.Running is null, "Suche beendet");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>≡, then RB past the launchers and the controller: "Datenquellen".</summary>
    private static DataSourcesSettingsViewModel OpenDataSources(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        var category = Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(AppTexts.DataSourcesTitle, category.Name);
        return category;
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    /// <summary>A on "Schlüssel", the key typed into the field, ≡ takes it; waits for its check.</summary>
    private static async Task TypeKeyAsync(ArcadiaDriver arcadia, string key)
    {
        arcadia.FocusRow(SteamGridDbKeyRows.KeyRowKey);
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor;
        Assert.NotNull(editor);
        editor.Type(key);
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle != AppTexts.DataSourcesKeyChecking, "Schlüssel geprüft");
        await SettledAsync(arcadia);
    }

    private static string? Hero(ArcadiaDriver arcadia) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(Store.Id + ":" + GameId)?.Hero?.Origin;

    [AvaloniaFact]
    [Trait("TestCase", "T489-1")]
    [Trait("TestCase", "T538-1")]
    public async Task The_category_shows_its_groups_in_two_tabs_and_without_a_key_greys_out_what_needs_one()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        var category = OpenDataSources(arcadia);
        Assert.Equal([AppTexts.DataSourcesArtworkTab, AppTexts.DataSourcesMetadataTab], category.Tabs.Select(t => t.Name));
        var head = Assert.IsType<DataSourcesHeadViewModel>(category.Head);
        Assert.Equal([true, false], head.Tabs.Select(t => t.IsSelected));
        Assert.Equal(AppTexts.DataSourcesTabTurn, category.TurnText);

        Assert.Equal(
            [
                (SteamGridDbKeyRows.KeyRowKey, AppTexts.DataSourcesSteamGridDbGroup, 0),
                ("steamgriddb-paste", AppTexts.DataSourcesSteamGridDbGroup, 1),
                ("steamgriddb-page", AppTexts.DataSourcesSteamGridDbGroup, 1),
                ("steamgriddb-test", AppTexts.DataSourcesSteamGridDbGroup, 1),
                ("steamgriddb-delete", AppTexts.DataSourcesSteamGridDbGroup, 1),
                (ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher), AppTexts.DataSourcesOrderGroup, 0),
                (ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb), AppTexts.DataSourcesOrderGroup, 0),
                (ArtworkSourceRows.MissingKey, AppTexts.DataSourcesArtworkGroup, 0),
                (ArtworkReloadRows.ReloadKey, AppTexts.DataSourcesArtworkGroup, 0),
                ("artwork-crop", AppTexts.DataSourcesArtworkGroup, 0),
                (ArtworkReloadRows.OverwriteKey, AppTexts.DataSourcesArtworkGroup, 0),
                (SeveralHitsRows.SeveralHitsKey(WaitingKind.Artwork), AppTexts.DataSourcesArtworkGroup, 0),
                (SeveralHitsRows.WaitingKey(WaitingKind.Artwork), AppTexts.DataSourcesArtworkGroup, 1),
            ],
            category.Rows.Select(row => (row.Key, row.Group!, row.Indent)));

        // RT: the tab "Metadaten" at its first row; LT back to "Artwork".
        arcadia.Press(UiAction.NextPage);
        Assert.Equal([false, true], head.Tabs.Select(t => t.IsSelected));
        Assert.Equal(IgdbAccessRows.ClientIdRowKey, arcadia.Main.Settings!.FocusedRow?.Key);
        Assert.Equal(
            [
                (IgdbAccessRows.ClientIdRowKey, AppTexts.DataSourcesIgdbGroup, 0),
                ("igdb-client-id-paste", AppTexts.DataSourcesIgdbGroup, 1),
                (IgdbAccessRows.SecretRowKey, AppTexts.DataSourcesIgdbGroup, 0),
                ("igdb-secret-paste", AppTexts.DataSourcesIgdbGroup, 1),
                ("igdb-console", AppTexts.DataSourcesIgdbGroup, 0),
                ("igdb-test", AppTexts.DataSourcesIgdbGroup, 0),
                ("igdb-delete", AppTexts.DataSourcesIgdbGroup, 0),
                (MetadataSourceRows.SourceKey(MetadataSourceKind.Launcher), AppTexts.DataSourcesMetadataOrderGroup, 0),
                (MetadataSourceRows.SourceKey(MetadataSourceKind.Igdb), AppTexts.DataSourcesMetadataOrderGroup, 0),
                (MetadataRows.MissingKey, AppTexts.DataSourcesMetadataGroup, 0),
                (MetadataRows.ReloadKey, AppTexts.DataSourcesMetadataGroup, 0),
                (SeveralHitsRows.SeveralHitsKey(WaitingKind.Metadata), AppTexts.DataSourcesMetadataGroup, 0),
                (SeveralHitsRows.WaitingKey(WaitingKind.Metadata), AppTexts.DataSourcesMetadataGroup, 1),
                (DataSourcesSettingsViewModel.OwnTagsRowKey, AppTexts.DataSourcesUserDataGroup, 0),
            ],
            category.Rows.Select(row => (row.Key, row.Group!, row.Indent)));
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal([true, false], head.Tabs.Select(t => t.IsSelected));
        Assert.Equal(SteamGridDbKeyRows.KeyRowKey, arcadia.Main.Settings!.FocusedRow?.Key);

        var key = Assert.IsType<TextRowViewModel>(Row(arcadia, SteamGridDbKeyRows.KeyRowKey));
        Assert.Equal((AppTexts.DataSourcesKeyNone, AppTexts.DataSourcesKeyEnter, string.Empty), (key.Subtitle, key.ActivateText, key.ValueText));
        Assert.Contains(key.Info, block => block is InfoSection { Title: var title } && title == AppTexts.DataSourcesSendsHeading);
        var test = Assert.IsType<ActionRowViewModel>(Row(arcadia, "steamgriddb-test"));
        Assert.Equal((AppTexts.DataSourcesTestNeedsKey, true, false), (test.Subtitle, test.IsDimmed, test.CanActivate));
        Assert.Equal("steamgriddb-test", arcadia.FocusRow("steamgriddb-test").Key);
        var source = Row(arcadia, ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb));
        Assert.Equal(("2. SteamGridDB", AppTexts.DataSourcesSteamGridDbSkipped, true), (source.Title, source.Subtitle, source.IsDimmed));
        Assert.Equal("1. Launcher-Bilder", Row(arcadia, ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher)).Title);
        Assert.Equal(AppTexts.DataSourcesMissingCounts(covers: 1, heroes: 0), Row(arcadia, ArtworkSourceRows.MissingKey).Subtitle);
        Assert.Equal(AppTexts.DataSourcesCropNormal, Assert.IsType<ChoiceRowViewModel>(Row(arcadia, "artwork-crop")).ValueText);
        Assert.Null(category.Marker);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-2")]
    [Trait("TestCase", "T538-6")]
    public async Task Key_entry_paste_and_the_connection_test_work_with_the_controller_and_B_goes_back()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        var key = Assert.IsType<TextRowViewModel>(Row(arcadia, SteamGridDbKeyRows.KeyRowKey));
        Assert.Equal((AppTexts.DataSourcesKeyConnected, AppTexts.Edit, "…cdef"), (key.Subtitle, key.ActivateText, key.ValueText));
        Assert.Equal((RowLamp.Working, true), (key.Lamp, key.HasTintedSubtitle));
        Assert.Equal(AppTexts.DataSourcesKeySavedSearching, arcadia.Main.Toast.Text);
        Assert.Equal(SteamGridDbKeyState.Valid, arcadia.Artwork.SteamGridDb!.State);
        Assert.Equal(AppTexts.DataSourcesSteamGridDbSubtitle, Row(arcadia, ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb)).Subtitle);

        // "Einfügen" with a key copied with a line break.
        arcadia.External.ClipboardText = FakeSteamGridDb.Key + "\r\n";
        arcadia.FocusRow("steamgriddb-paste");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesKeyPasted, "eingefügt");

        arcadia.FocusRow("steamgriddb-test");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesTestOkToast, "getestet");
        Assert.Equal(AppTexts.DataSourcesTestOk(Services.Formatting.Clock(arcadia.Time.GetLocalNow())), Row(arcadia, "steamgriddb-test").Subtitle);

        // B from the field drops what was typed, B from the page goes back to the library.
        arcadia.FocusRow(SteamGridDbKeyRows.KeyRowKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Main.Settings!.Selected.Editor!.Type("abc");
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings!.Selected.Editor);
        Assert.Equal("…cdef", key.ValueText);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-3")]
    public async Task An_empty_field_keeps_the_key_and_Schluessel_loeschen_deletes_it_without_a_request()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        var delete = Row(arcadia, "steamgriddb-delete");
        Assert.Equal((true, false), (delete.IsDimmed, delete.CanActivate));
        arcadia.SteamGridDb.ValidKey = WrongKey;
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        Assert.True(arcadia.Main.Status.HasSteamGridDbWarning);
        var asked = arcadia.SteamGridDb.Count;

        // "Bearbeiten" and the empty field taken: nothing changes, nothing is sent.
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Settings);
        Assert.Null(arcadia.Main.Settings!.Selected.Editor);
        Assert.Equal(AppTexts.DataSourcesKeyRejected, Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.True(File.Exists(Path.Combine(arcadia.Folder, "data", SteamGridDbKeyStore.FileName)));
        Assert.Equal(asked, arcadia.SteamGridDb.Count);

        arcadia.FocusRow("steamgriddb-delete");
        arcadia.Press(UiAction.Accept);

        Assert.Equal(AppTexts.DataSourcesKeyDeleted, arcadia.Main.Toast.Text);
        Assert.Equal(AppTexts.DataSourcesKeyNone, Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.False(arcadia.Main.Status.HasSteamGridDbWarning);
        Assert.Null(arcadia.Main.Settings!.Selected.Marker);
        Assert.False(File.Exists(Path.Combine(arcadia.Folder, "data", SteamGridDbKeyStore.FileName)));
        arcadia.FocusRow(ArtworkSourceRows.MissingKey);
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-4")]
    public async Task Einfuegen_without_a_key_on_the_clipboard_keeps_the_old_one_and_says_so()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        var asked = arcadia.SteamGridDb.Count;
        var toasts = 0;
        arcadia.Main.Toast.History.Changed += (_, _) => toasts++;

        foreach (var clipboard in new[] { null, string.Empty, "mein Passwort", "0123456789abcdef" })
        {
            arcadia.External.ClipboardText = clipboard;
            arcadia.FocusRow("steamgriddb-paste");
            var before = toasts;
            arcadia.Press(UiAction.Accept);
            await arcadia.WaitUntilAsync(() => toasts > before && arcadia.Main.Toast.Text == AppTexts.DataSourcesPasteNoKey, "Meldung");
        }

        Assert.Equal("…cdef", Assert.IsType<TextRowViewModel>(Row(arcadia, SteamGridDbKeyRows.KeyRowKey)).ValueText);
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-6")]
    public async Task The_key_page_opens_steamgriddbs_api_page_and_by_controller_asks_for_the_mouse_mode()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        var forMouse = 0;
        arcadia.Main.OpenedForMouseByController += (_, _) => forMouse++;
        OpenDataSources(arcadia);

        arcadia.FocusRow("steamgriddb-page");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => forMouse == 1, "Mausmodus angefragt");

        Assert.Equal([new Uri("https://www.steamgriddb.com/profile/preferences/api")], arcadia.External.Opened);
        Assert.Equal(AppTexts.DataSourcesKeyPageOpening, arcadia.Main.Toast.Text);

        arcadia.External.BrowserFailure = new System.ComponentModel.Win32Exception("Kein Browser");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesKeyPageFailed, "Fehlermeldung");
        Assert.Equal(1, forMouse);
        Assert.True(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-7")]
    public async Task The_connection_test_says_whether_steamgriddb_turns_the_key_down_or_doesnt_answer()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        arcadia.SteamGridDb.Offline = true;
        arcadia.FocusRow("steamgriddb-test");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesTestNoConnectionToast, "keine Verbindung");
        Assert.Equal(AppTexts.DataSourcesTestNoConnection, Row(arcadia, "steamgriddb-test").Subtitle);
        Assert.False(arcadia.Main.Status.HasSteamGridDbWarning);
        Assert.Equal(SteamGridDbKeyState.Valid, arcadia.Artwork.SteamGridDb!.State);

        arcadia.SteamGridDb.Offline = false;
        arcadia.SteamGridDb.ValidKey = WrongKey;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesKeyRejectedToast, "abgelehnt");
        Assert.Equal(AppTexts.DataSourcesTestRejected(Services.Formatting.Clock(arcadia.Time.GetLocalNow())), Row(arcadia, "steamgriddb-test").Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-12")]
    public async Task Without_a_key_start_rescan_and_Jetzt_suchen_send_steamgriddb_nothing_and_say_nothing()
    {
        var (arcadia, images) = await StartAsync();
        using var _ = arcadia;
        await arcadia.Main.Session.RescanAsync(null);
        await SettledAsync(arcadia);
        OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkSourceRows.MissingKey);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => Row(arcadia, ArtworkSourceRows.MissingKey).Subtitle == AppTexts.DataSourcesSearchLauncherOnly, "Suche fertig");

        Assert.Equal(0, arcadia.SteamGridDb.Count);
        Assert.Equal("store-wide", Hero(arcadia));
        Assert.False(arcadia.Main.Status.HasSteamGridDbWarning);
        Assert.Null(arcadia.Main.Settings!.Selected.Marker);
        Assert.DoesNotContain(arcadia.Main.Toast.History.Entries, entry => entry.Text.Contains("SteamGridDB", StringComparison.Ordinal));
        Assert.DoesNotContain(images.Requests, request => request.RequestUri!.Host == FakeSteamGridDb.Host);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-13")]
    [Trait("TestCase", "T538-6")]
    public async Task An_invalid_key_warns_at_the_tab_the_row_and_the_status_chip_and_a_valid_one_clears_all_three()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        var category = OpenDataSources(arcadia);
        arcadia.SteamGridDb.ValidKey = WrongKey;

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        Assert.Equal(AppTexts.DataSourcesKeyRejectedToast, arcadia.Main.Toast.Text);
        Assert.Equal(HealthSeverity.Warning, category.Marker);
        var key = Row(arcadia, SteamGridDbKeyRows.KeyRowKey);
        Assert.Equal((AppTexts.DataSourcesKeyRejected, HealthSeverity.Warning), (key.Subtitle, key.Severity));
        Assert.Equal((RowLamp.Problem, true), (key.Lamp, key.HasTintedSubtitle));
        Assert.Equal([true, false], Assert.IsType<DataSourcesHeadViewModel>(category.Head).Tabs.Select(t => t.HasWarning));
        Assert.Contains(key.Info, block => block is InfoSection { Title: var title } && title == AppTexts.DataSourcesNextStepsHeading);
        Assert.True(arcadia.Main.Status.HasSteamGridDbWarning);
        Assert.Equal("store-wide", Hero(arcadia));

        // The status modal's entry leads to the key.
        arcadia.Press(UiAction.Back).Press(UiAction.Status);
        var entry = Assert.Single(Assert.IsType<Status.StatusModalViewModel>(arcadia.Main.StatusModal).Entries);
        Assert.Equal(AppTexts.DataSourcesKeyStatus, entry.Entry.Title);
        arcadia.Press(UiAction.Accept);
        Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(SteamGridDbKeyRows.KeyRowKey, arcadia.Main.Settings.FocusedRow?.Key);

        arcadia.SteamGridDb.ValidKey = FakeSteamGridDb.Key;
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        Assert.Null(arcadia.Main.Settings!.Selected.Marker);
        Assert.Null(Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Severity);
        Assert.False(arcadia.Main.Status.HasSteamGridDbWarning);
        Assert.DoesNotContain(arcadia.Main.Status.Entries, e => e.Id == StatusBarViewModel.SteamGridDbStatusId);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-64")]
    public async Task L3_lifts_a_source_up_down_move_it_L3_drops_it_and_B_puts_it_back_and_the_order_stays_after_a_restart()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        var steamGridDb = ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb);
        var settings = arcadia.Main.Settings!;

        arcadia.FocusRow(steamGridDb);
        Assert.Equal(AppTexts.Move, settings.Selected.L3ActionText);
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(settings.Selected.IsReordering);
        Assert.True(Row(arcadia, steamGridDb).IsLifted);

        // ▲ moves it, a second ▲ at the top stays there (no wrap), ◀ ▶ switch nothing while it is lifted.
        arcadia.Press(UiAction.Up);
        arcadia.Press(UiAction.Up);
        arcadia.Press(UiAction.Left);
        Assert.Equal("1. SteamGridDB", Row(arcadia, steamGridDb).Title);
        Assert.Equal(ArtworkSettings.DefaultOrder, arcadia.Settings.Current.Artwork.Order);
        Assert.Empty(arcadia.Settings.Current.Artwork.Off);
        arcadia.Press(UiAction.ViewOptions);

        Assert.False(settings.Selected.IsReordering);
        Assert.Equal(steamGridDb, settings.FocusedRow?.Key);
        Assert.Equal([ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher], arcadia.Settings.Current.Artwork.Order);
        Assert.Contains("\"SteamGridDb\"", File.ReadAllText(arcadia.SettingsPath), StringComparison.Ordinal);

        // B while lifted: back where it was, nothing saved, still on the page.
        arcadia.Press(UiAction.ViewOptions);
        arcadia.Press(UiAction.Down);
        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.False(settings.Selected.IsReordering);
        Assert.Equal("1. SteamGridDB", Row(arcadia, steamGridDb).Title);
        Assert.Equal([ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher], arcadia.Settings.Current.Artwork.Order);

        using var restarted = await arcadia.RestartAsync();
        OpenDataSources(restarted);
        Assert.Equal(steamGridDb, restarted.Main.Settings!.Selected.Rows[5].Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-59")]
    public async Task Left_and_right_switch_a_source_on_and_off_and_it_stays_after_a_restart()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        var launcher = Assert.IsType<ArtworkSourceRowViewModel>(arcadia.FocusRow(ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher)));
        Assert.True(launcher.IsOn);

        arcadia.Press(UiAction.Right);
        Assert.False(launcher.IsOn);
        Assert.Equal(AppTexts.DataSourcesSourceOffSubtitle, launcher.Subtitle);
        Assert.Equal([ArtworkSourceKind.Launcher], arcadia.Settings.Current.Artwork.Off);

        using var restarted = await arcadia.RestartAsync();
        OpenDataSources(restarted);
        var again = Assert.IsType<ArtworkSourceRowViewModel>(restarted.FocusRow(ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher)));
        Assert.False(again.IsOn);
        restarted.Press(UiAction.Left);
        Assert.True(again.IsOn);
        Assert.Empty(restarted.Settings.Current.Artwork.Off);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-16")]
    public async Task The_tolerance_steps_round_and_a_sixteen_by_nine_background_gives_way_to_steamgriddbs_hero_with_a_key()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        // Without a key: the launcher's 16 : 9 image, cropped.
        Assert.Equal("store-wide", Hero(arcadia));
        OpenDataSources(arcadia);

        var crop = Assert.IsType<ChoiceRowViewModel>(arcadia.FocusRow("artwork-crop"));
        arcadia.Press(UiAction.Right);
        Assert.Equal((AppTexts.DataSourcesCropMuch, CropAllowance.Much), (crop.ValueText, arcadia.Settings.Current.Artwork.Crop));
        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.DataSourcesCropLittle, crop.ValueText);
        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.DataSourcesCropMuch, crop.ValueText);
        arcadia.Press(UiAction.Left);
        Assert.Equal(CropAllowance.Normal, arcadia.Settings.Current.Artwork.Crop);

        // With a key, the search after it finds SteamGridDB's hero; the game's tile shows it without the focus moving.
        var focused = arcadia.Main.Library.FocusedGame;
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Hero(arcadia));
        Assert.Equal(["heroes/steam/" + GameId], arcadia.SteamGridDb.Asked().Where(path => path.StartsWith("heroes/", StringComparison.Ordinal) && !path.EndsWith("/220", StringComparison.Ordinal)));
        Assert.Same(focused, arcadia.Main.Library.FocusedGame);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-19")]
    public async Task Jetzt_suchen_counts_the_missing_images_shows_its_progress_and_what_it_found()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Cover(ArtworkStoreId.Steam, GameId, "https://cdn.example/grid/4100001.png", 600, 900);
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        // The search after the key found no cover yet: the image isn't on the image server.
        Assert.Equal(AppTexts.DataSourcesMissingCounts(covers: 1, heroes: 0), Row(arcadia, ArtworkSourceRows.MissingKey).Subtitle);

        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ArtworkSourceRows.MissingKey));
        arcadia.SteamGridDb.Hangs = true;
        arcadia.Press(UiAction.Accept);
        Assert.Equal((AppTexts.DataSourcesSearchCancel, true), (missing.ActionText, missing.CanActivate));
        await arcadia.WaitUntilAsync(() => missing.Subtitle == AppTexts.DataSourcesSearchRunning(done: 0, total: 1), "Suche läuft");
        Assert.True(missing.HasProgress);
        arcadia.SteamGridDb.Hangs = false;
        arcadia.Time.Advance(SteamGridDbClient.Timeout);
        await SettledAsync(arcadia);

        // That run had no connection: a warning, as the player started it.
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesSearchNoConnection, "Warnung");
        Assert.Equal(1, arcadia.SteamGridDb.Asked().Count(path => path == "grids/steam/" + GameId) - 1);
        Assert.Equal(AppTexts.DataSourcesSearchFound(covers: 0, heroes: 0), missing.Subtitle);
        Assert.False(missing.HasProgress);
    }

    /// <summary>The launcher's images: a 16 : 9 background to download, and the game's store id.</summary>
    private sealed class StoreArtwork : ILauncherArtwork, IArtworkLookup
    {
        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) =>
            kind == ArtworkKind.Hero ? [new ArtworkCandidate("store-wide", Download: new Uri(WideHero))] : [];

        public ArtworkStoreId? StoreIdOf(Game game) => new(ArtworkStoreId.Steam, game.Id);
    }
}
