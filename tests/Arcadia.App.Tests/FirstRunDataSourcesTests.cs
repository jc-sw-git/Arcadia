using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Launchers;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #497: the first start's step "Datenquellen" after the launcher page, through the view models with a fake SteamGridDB,
/// a fake Twitch and IGDB and fake time; no window. It continues where it stopped after a restart.
/// </summary>
public sealed class FirstRunDataSourcesTests
{
    private const string GameId = "4970001";
    private const string WrongKey = "ffffffffffffffffffffffffffffffff";

    private static readonly LauncherInfo Store = new("store", "Shop", "shop", null);

    /// <summary>A made-up launcher with one game SteamGridDB and IGDB know, and GOG with three games; no settings yet.</summary>
    private static async Task<ArcadiaDriver> StartAsync(AppSettings? settings = null, StartArguments arguments = default)
    {
        var game = new Game
        {
            Id = GameId,
            LauncherId = Store.Id,
            Title = "Leuchtturm",
            Launch = new LaunchTarget.ProtocolUri("shop://run/" + GameId),
        };
        var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Store, game) with { Artwork = new StoreArtwork() }, new LauncherSpec(KnownLaunchers.Gog, 3)],
            settings ?? new AppSettings(),
            arguments: arguments);
        arcadia.SteamGridDb.Cover(ArtworkStoreId.Steam, GameId, "https://cdn.example/grid/4970001.png", 600, 900);
        arcadia.Igdb.ServeName("Leuchtturm", FakeIgdb.Game(7, "Leuchtturm", extra: "\"summary\":\"A made-up text.\""));
        return arcadia;
    }

    private static FirstRunDataSourcesViewModel ToStep(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        return arcadia.DataSourcesStep();
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    /// <summary>A on the row, the value typed on the on-screen keyboard, ≡ takes it; waits for its check.</summary>
    private static async Task TypeAsync(ArcadiaDriver arcadia, string rowKey, string value, string checkedRowKey)
    {
        arcadia.FocusRow(rowKey);
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor;
        Assert.NotNull(editor);
        editor.Type(value);
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => Row(arcadia, checkedRowKey).Subtitle != AppTexts.DataSourcesKeyChecking, "geprüft");
        Dispatcher.UIThread.RunJobs();
    }

    private static Task TypeKeyAsync(ArcadiaDriver arcadia, string key) => TypeAsync(arcadia, SteamGridDbKeyRows.KeyRowKey, key, SteamGridDbKeyRows.KeyRowKey);

    private static async Task GrantIgdbAsync(ArcadiaDriver arcadia)
    {
        await TypeAsync(arcadia, IgdbAccessRows.ClientIdRowKey, FakeIgdb.ClientId, IgdbAccessRows.ClientIdRowKey);
        await TypeAsync(arcadia, IgdbAccessRows.SecretRowKey, FakeIgdb.Secret, IgdbAccessRows.ClientIdRowKey);
    }

    private static async Task SettledAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 2000 && !(arcadia.Artwork.WhenIdleAsync().IsCompleted && arcadia.Metadata.WhenIdleAsync().IsCompleted); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromMilliseconds(250));
        }

        await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    private static string RecordPath(ArcadiaDriver arcadia) => Path.Combine(arcadia.Folder, FirstRunProgressFile.FileName);

    [AvaloniaFact]
    [Trait("TestCase", "T497-1")]
    public async Task Fertig_on_the_launcher_page_leads_to_the_step_with_the_rows_of_the_settings()
    {
        using var arcadia = await StartAsync();

        var step = ToStep(arcadia);

        Assert.True(arcadia.Main.IsInFirstRun);
        Assert.False(arcadia.Main.IsFirstRunShown);
        Assert.Equal(AppTexts.Setup, arcadia.Main.Settings!.Title);
        Assert.False(arcadia.Main.Settings.HasSeveralCategories);
        Assert.Equal(
            [
                SteamGridDbKeyRows.KeyRowKey, "steamgriddb-paste", "steamgriddb-page", "steamgriddb-test", "steamgriddb-delete",
                IgdbAccessRows.ClientIdRowKey, "igdb-client-id-paste", IgdbAccessRows.SecretRowKey, "igdb-secret-paste", "igdb-console", "igdb-test", "igdb-delete",
                FirstRunDataSourcesViewModel.FinishRowKey,
            ],
            step.Rows.Select(row => row.Key));
        Assert.Equal(AppTexts.DataSourcesKeyNone, Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.Equal(AppTexts.DataSourcesIgdbNone, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle);

        // What Arcadia sends, and the limits of IGDB and of Steam's shop.
        Assert.Contains(Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Info, block => block is InfoSection { Title: var heading } && heading == AppTexts.DataSourcesSendsHeading);
        Assert.Contains(Row(arcadia, IgdbAccessRows.ClientIdRowKey).Info, block => block is InfoSection { Title: var heading } && heading == AppTexts.DataSourcesSendsHeading);
        Assert.Contains(Row(arcadia, IgdbAccessRows.ClientIdRowKey).Info, block => block is InfoText { Text: var text } && text.Contains("4 Anfragen pro Sekunde", StringComparison.Ordinal));
        var finish = Row(arcadia, FirstRunDataSourcesViewModel.FinishRowKey);
        Assert.Contains(finish.Info, block => block is InfoText { Text: var text } && text.Contains("so langsam, wie es der Shop erlaubt", StringComparison.Ordinal));
        Assert.Equal((AppTexts.FirstRunSkip, AppTexts.FirstRunSkipSubtitle, AppTexts.FirstRunSkip), (finish.Title, finish.Subtitle, step.FinishText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-2")]
    public async Task Skipping_with_one_press_shows_the_library_sends_nothing_and_nothing_comes_again()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);

        arcadia.Press(UiAction.Settings);
        await SettledAsync(arcadia);

        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Equal(["Shop", "GOG Galaxy"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Empty(arcadia.SteamGridDb.Asked());
        Assert.Empty(arcadia.Igdb.Snapshot());
        Assert.False(arcadia.Main.Toast.IsVisible, arcadia.Main.Toast.Text);
        Assert.False(File.Exists(RecordPath(arcadia)));

        using var restarted = await arcadia.RestartAsync();
        Assert.False(restarted.Main.IsInFirstRun);
        Assert.False(restarted.Main.IsSettingsOpen);
        restarted.Main.OpenDataSources();
        Assert.Equal(AppTexts.DataSourcesKeyNone, Row(restarted, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.Equal(AppTexts.DataSourcesIgdbNone, Row(restarted, IgdbAccessRows.ClientIdRowKey).Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-2")]
    public async Task A_on_Ueberspringen_ends_the_step_like_the_menu_button()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);

        arcadia.Press(UiAction.Up);
        Assert.Equal(FirstRunDataSourcesViewModel.FinishRowKey, arcadia.Main.Settings!.FocusedRow?.Key);
        arcadia.Press(UiAction.Accept);

        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-3")]
    public async Task A_key_typed_in_the_step_is_checked_searches_at_once_and_is_in_the_settings_afterwards()
    {
        using var arcadia = await StartAsync();
        var step = ToStep(arcadia);

        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        Assert.Equal(AppTexts.DataSourcesKeyConnected(SteamGridDbClient.Ending(FakeSteamGridDb.Key)), Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        await arcadia.WaitUntilAsync(() => arcadia.SteamGridDb.Asked().Contains("grids/steam/" + GameId), "die Bildersuche soll gleich starten");
        Assert.Equal((AppTexts.Done, AppTexts.Done), (Row(arcadia, FirstRunDataSourcesViewModel.FinishRowKey).Title, step.FinishText));

        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsInFirstRun);
        arcadia.Main.OpenDataSources();
        Assert.Equal(AppTexts.DataSourcesKeyConnected(SteamGridDbClient.Ending(FakeSteamGridDb.Key)), Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.DoesNotContain(FakeSteamGridDb.Key, ArcadiaDriver.Log, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-3")]
    public async Task A_key_pasted_in_the_step_is_taken_as_in_the_settings()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);
        arcadia.External.ClipboardText = FakeSteamGridDb.Key + "\r\n";

        arcadia.FocusRow("steamgriddb-paste");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Artwork.SteamGridDb!.State == SteamGridDbKeyState.Valid, "Schlüssel eingefügt");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AppTexts.DataSourcesKeyConnected(SteamGridDbClient.Ending(FakeSteamGridDb.Key)), Row(arcadia, SteamGridDbKeyRows.KeyRowKey).Subtitle);
        Assert.Equal(AppTexts.DataSourcesKeyPastedSearching, arcadia.Main.Toast.Text);
        Assert.DoesNotContain(FakeSteamGridDb.Key, ArcadiaDriver.Log, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-4")]
    public async Task The_igdb_access_typed_in_the_step_is_checked_and_fetches_the_librarys_metadata()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);

        await GrantIgdbAsync(arcadia);

        Assert.Equal(IgdbAccessState.Valid, arcadia.Metadata.Igdb!.State);
        arcadia.Press(UiAction.Settings);
        await SettledAsync(arcadia);
        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.True(arcadia.Igdb.ApiRequests > 0, "IGDB soll für die Spiele der Bibliothek gefragt werden.");
        Assert.DoesNotContain(FakeIgdb.ClientId, ArcadiaDriver.Log, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeIgdb.Secret, ArcadiaDriver.Log, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-5")]
    public async Task Ending_the_step_waits_for_no_request_and_starts_no_second_search()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);
        // A slow SteamGridDB: the search the key starts is still waiting for its answer when the step ends.
        var grids = "grids/steam/" + GameId;
        arcadia.SteamGridDb.SlowPaths[grids] = TimeSpan.FromSeconds(1);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await arcadia.WaitUntilAsync(() => arcadia.SteamGridDb.Asked().Contains(grids), "Suche fragt SteamGridDB");

        arcadia.Press(UiAction.Settings);

        // At once, while the answer is still on its way.
        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.NotNull(arcadia.Artwork.Search.Running);
        await SettledAsync(arcadia);
        Assert.Equal(1, arcadia.SteamGridDb.Asked().Count(path => path == grids));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-6")]
    public async Task A_rejected_key_warns_in_the_step_which_still_ends_and_steamgriddb_gets_no_more_requests()
    {
        using var arcadia = await StartAsync();
        var step = ToStep(arcadia);

        await TypeKeyAsync(arcadia, WrongKey);

        var key = Row(arcadia, SteamGridDbKeyRows.KeyRowKey);
        Assert.Equal((AppTexts.DataSourcesKeyRejected, HealthSeverity.Warning), (key.Subtitle, key.Severity));
        Assert.Equal(HealthSeverity.Warning, step.Marker);
        var asked = arcadia.SteamGridDb.Asked().Count;

        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsInFirstRun);
        await arcadia.Main.Session.RescanAsync(null);
        await SettledAsync(arcadia);
        Assert.Equal(asked, arcadia.SteamGridDb.Asked().Count);
        Assert.Equal(["Shop", "GOG Galaxy"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        arcadia.Main.OpenDataSources();
        Assert.Equal(HealthSeverity.Warning, arcadia.Main.Settings!.Selected.Marker);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-6")]
    public async Task A_rejected_igdb_access_warns_in_the_step_which_still_ends()
    {
        using var arcadia = await StartAsync();
        var step = ToStep(arcadia);
        arcadia.Igdb.AcceptedSecret = "madeupsecret00000000000000000009";

        await GrantIgdbAsync(arcadia);

        Assert.Equal(IgdbAccessState.Rejected, arcadia.Metadata.Igdb!.State);
        Assert.Equal(HealthSeverity.Warning, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Severity);
        Assert.Equal(HealthSeverity.Warning, step.Marker);
        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsInFirstRun);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-7")]
    public async Task The_connection_test_without_network_keeps_the_key_sends_once_and_the_step_ends_meanwhile()
    {
        using var arcadia = await StartAsync();
        var step = ToStep(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await SettledAsync(arcadia);

        arcadia.SteamGridDb.Offline = true;
        arcadia.FocusRow("steamgriddb-test");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => Row(arcadia, "steamgriddb-test").Subtitle == AppTexts.DataSourcesTestNoConnection, "kein Netz");
        Assert.NotEqual(SteamGridDbKeyState.None, arcadia.Artwork.SteamGridDb!.State);
        Assert.Null(step.Marker);

        // No answer: a second A while the test runs asks nothing more, and the step ends meanwhile.
        arcadia.SteamGridDb.Offline = false;
        arcadia.SteamGridDb.Hangs = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.SteamGridDb.Hanging == 1, "Test läuft");
        arcadia.Press(UiAction.Accept);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, arcadia.SteamGridDb.Hanging);
        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsInFirstRun);
        arcadia.SteamGridDb.Hangs = false;
        arcadia.Time.Advance(SteamGridDbClient.Timeout);
        await SettledAsync(arcadia);
        Assert.NotEqual(SteamGridDbKeyState.None, arcadia.Artwork.SteamGridDb.State);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-8")]
    public async Task B_on_the_step_goes_back_to_the_launcher_page_with_the_switches_as_left()
    {
        using var arcadia = await StartAsync();
        var launcherPage = arcadia.Main.FirstRun!;
        // GOG (second row) off.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        ToStep(arcadia);
        Assert.Equal(["Shop"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));

        arcadia.Press(UiAction.Back);

        Assert.Same(launcherPage, arcadia.Main.FirstRun);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Equal([true, false], launcherPage.Found.Select(row => row.IsOn));
        await arcadia.WaitUntilAsync(() => launcherPage.Found.All(row => row.StatusText != AppTexts.Found), "die Spiele aller gefundenen Launcher gezählt");

        // B on the launcher page still does nothing.
        arcadia.Press(UiAction.Back);
        Assert.Same(launcherPage, arcadia.Main.FirstRun);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-9")]
    public async Task After_a_restart_on_the_launcher_page_it_shows_again_with_the_switches_as_left()
    {
        using var arcadia = await StartAsync();
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);

        using var restarted = await arcadia.RestartAsync();

        var page = Assert.IsType<FirstRunViewModel>(restarted.Main.FirstRun);
        Assert.Equal([true, false], page.Found.Select(row => row.IsOn));
        Assert.False(File.Exists(RecordPath(arcadia) + ".tmp"));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-9")]
    public async Task After_a_restart_on_the_step_it_shows_the_step_with_the_key_and_the_launchers_chosen()
    {
        using var arcadia = await StartAsync();
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        ToStep(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        using var restarted = await arcadia.RestartAsync();

        restarted.DataSourcesStep();
        Assert.True(restarted.Main.IsInFirstRun);
        Assert.Contains(SteamGridDbClient.Ending(FakeSteamGridDb.Key), Row(restarted, SteamGridDbKeyRows.KeyRowKey).Subtitle, StringComparison.Ordinal);
        Assert.Equal(["Shop"], restarted.Main.LauncherTabs.Items.Select(tab => tab.Name));

        // B leads back to the launcher page as it was left, ≡ twice ends it, and a restart then shows the library.
        restarted.Press(UiAction.Back);
        Assert.Equal([true, false], restarted.Main.FirstRun!.Found.Select(row => row.IsOn));
        await restarted.FinishFirstRunAsync();
        Assert.Equal(["Shop"], restarted.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.False(File.Exists(RecordPath(restarted)));
        using var again = await restarted.RestartAsync();
        Assert.False(again.Main.IsInFirstRun);
        Assert.False(again.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-9")]
    public async Task An_unreadable_record_begins_again_on_the_launcher_page_with_a_stored_key_shown_as_stored()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        File.WriteAllText(RecordPath(arcadia), "{ \"Step\": \"DataSo");

        using var restarted = await arcadia.RestartAsync();

        var page = Assert.IsType<FirstRunViewModel>(restarted.Main.FirstRun);
        Assert.All(page.Found, row => Assert.True(row.IsOn));
        Assert.Contains("Ersteinrichtung: Stand nicht lesbar", ArcadiaDriver.Log, StringComparison.Ordinal);
        restarted.Press(UiAction.Settings);
        Assert.Contains(SteamGridDbClient.Ending(FakeSteamGridDb.Key), Row(restarted, SteamGridDbKeyRows.KeyRowKey).Subtitle, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-10")]
    public async Task Settings_with_a_choice_of_launchers_show_the_library_and_no_step()
    {
        using var arcadia = await StartAsync(new AppSettings { ActiveLaunchers = [Store.Id] });

        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.False(File.Exists(RecordPath(arcadia)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-10")]
    public async Task Demo_skips_both_and_demo_with_first_run_leaves_no_record()
    {
        using var demo = await StartAsync(arguments: new StartArguments(Demo: true));
        Assert.False(demo.Main.IsInFirstRun);
        Assert.False(demo.Main.IsSettingsOpen);

        using var arcadia = await StartAsync(arguments: new StartArguments(Demo: true, FirstRun: true));
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        ToStep(arcadia);
        Assert.False(File.Exists(RecordPath(arcadia)));
        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsInFirstRun);
        Assert.Equal(["Shop"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.False(File.Exists(RecordPath(arcadia)));
        Assert.Null(new SettingsStore(arcadia.SettingsPath).Load().ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-10")]
    public async Task First_run_shows_both_pages_again_with_a_stored_key_shown_as_stored()
    {
        using var arcadia = await StartAsync(new AppSettings { ActiveLaunchers = [Store.Id] });
        arcadia.Main.OpenDataSources();
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);

        using var again = await arcadia.RestartAsync(arguments: new StartArguments(FirstRun: true));

        Assert.True(again.Main.IsFirstRunShown);
        ToStep(again);
        Assert.Contains(SteamGridDbClient.Ending(FakeSteamGridDb.Key), Row(again, SteamGridDbKeyRows.KeyRowKey).Subtitle, StringComparison.Ordinal);
        Assert.Equal(AppTexts.Done, Row(again, FirstRunDataSourcesViewModel.FinishRowKey).Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-11")]
    public async Task The_key_page_and_twitchs_console_open_from_the_step_with_the_mouse_mode_and_the_focus_stays()
    {
        using var arcadia = await StartAsync();
        var forMouse = 0;
        arcadia.Main.OpenedForMouseByController += (_, _) => forMouse++;
        ToStep(arcadia);

        arcadia.FocusRow("steamgriddb-page");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => forMouse == 1, "Mausmodus angefragt");
        arcadia.FocusRow("igdb-console");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => forMouse == 2, "Mausmodus angefragt");

        Assert.Equal([SteamGridDbClient.KeyPage, IgdbClient.ConsolePage], arcadia.External.Opened);
        Assert.All(arcadia.External.Opened, page => Assert.Equal(Uri.UriSchemeHttps, page.Scheme));
        Assert.Equal("igdb-console", arcadia.Main.Settings!.FocusedRow?.Key);
        Assert.True(arcadia.Main.Settings.IsFirstRun);

        arcadia.External.BrowserFailure = new System.ComponentModel.Win32Exception("Kein Browser");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesIgdbConsoleFailed, "Fehlermeldung");
        Assert.Equal(2, forMouse);
        Assert.True(arcadia.Main.Settings.IsFirstRun);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T497-12")]
    public async Task The_steps_rows_go_round_and_b_in_the_keyboard_keeps_the_value_without_a_request()
    {
        using var arcadia = await StartAsync();
        ToStep(arcadia);
        var settings = arcadia.Main.Settings!;
        Assert.Equal(SteamGridDbKeyRows.KeyRowKey, settings.FocusedRow?.Key);

        arcadia.Press(UiAction.Up);
        Assert.Equal(FirstRunDataSourcesViewModel.FinishRowKey, settings.FocusedRow?.Key);
        arcadia.Press(UiAction.Down);
        Assert.Equal(SteamGridDbKeyRows.KeyRowKey, settings.FocusedRow?.Key);

        // A greyed-out row keeps the focus, as in the settings' "Datenquellen" (T494-1), and A there does nothing.
        Assert.True(arcadia.FocusRow("steamgriddb-delete").IsDimmed);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SteamGridDbKeyState.None, arcadia.Artwork.SteamGridDb!.State);

        arcadia.FocusRow(SteamGridDbKeyRows.KeyRowKey);
        arcadia.Press(UiAction.Accept);
        var editor = settings.Selected.Editor;
        Assert.NotNull(editor);
        Assert.Equal(string.Empty, editor.Text);
        editor.Type(FakeSteamGridDb.Key);
        arcadia.Press(UiAction.Back);

        Assert.Null(settings.Selected.Editor);
        Assert.True(arcadia.Main.IsInFirstRun);
        Assert.Equal(SteamGridDbKeyState.None, arcadia.Artwork.SteamGridDb.State);
        Assert.Empty(arcadia.SteamGridDb.Asked());
    }

    /// <summary>The launcher's images: none of its own, and the game's Steam id for SteamGridDB.</summary>
    private sealed class StoreArtwork : ILauncherArtwork, IArtworkLookup
    {
        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => [];

        public ArtworkStoreId? StoreIdOf(Game game) => new(ArtworkStoreId.Steam, game.Id);
    }
}
