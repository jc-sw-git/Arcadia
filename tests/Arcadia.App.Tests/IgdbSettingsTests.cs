using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #494: the groups "IGDB" and "REIHENFOLGE FÜR METADATEN" of "Datenquellen" through the view models, with a made-up
/// launcher whose games IGDB knows by name, a fake Twitch and IGDB and fake time; no window.
/// </summary>
public sealed class IgdbSettingsTests
{
    private const string OtherSecret = "madeupsecret00000000000000000002";

    private static readonly LauncherInfo Shop = new("shop", "Shop", "shop", null);

    private static Game GameOf(string id, string title) => new()
    {
        Id = id,
        LauncherId = Shop.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri("shop://run/" + id),
    };

    private static async Task<ArcadiaDriver> StartAsync(params Game[] games)
    {
        var source = new FakeMetadataSource { Store = null };
        source.Local["k1"] = new GameMetadata { Developers = ["Erfundenes Studio"] };
        var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Shop, games.Length > 0 ? games : [GameOf("k1", "K?ln")]) with { Metadata = source }],
            new AppSettings { PanEnabled = true, ActiveLaunchers = [Shop.Id] });
        arcadia.Igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln", extra: "\"summary\":\"A made-up English text about the city.\",\"genres\":[{\"name\":\"Adventure\"}],\"first_release_date\":1609459200"));
        return arcadia;
    }

    private static async Task RunToIdleAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 3000 && !arcadia.Metadata.WhenIdleAsync().IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromMilliseconds(250));
        }

        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    private static DataSourcesSettingsViewModel OpenDataSources(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        var category = Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
        arcadia.Press(UiAction.NextPage);
        Assert.IsType<MetadataSourcesTab>(category.ShownTab);
        return category;
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    /// <summary>A on the row, the value typed on the on-screen keyboard, ≡ takes it; waits for a check.</summary>
    private static async Task TypeAsync(ArcadiaDriver arcadia, string rowKey, string value)
    {
        arcadia.FocusRow(rowKey);
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor;
        Assert.NotNull(editor);
        editor.Type(value);
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle != AppTexts.DataSourcesKeyChecking, "Zugang geprüft");
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task GrantAsync(ArcadiaDriver arcadia)
    {
        await TypeAsync(arcadia, IgdbAccessRows.ClientIdRowKey, FakeIgdb.ClientId);
        await TypeAsync(arcadia, IgdbAccessRows.SecretRowKey, FakeIgdb.Secret);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-1")]
    [Trait("TestCase", "T494-10")]
    public async Task The_group_igdb_has_its_rows_and_without_access_nothing_is_sent_and_igdb_is_skipped()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        var category = OpenDataSources(arcadia);

        var keys = category.Rows.Where(r => r.Group == AppTexts.DataSourcesIgdbGroup).Select(r => r.Key).ToList();
        Assert.Equal([IgdbAccessRows.ClientIdRowKey, "igdb-client-id-paste", IgdbAccessRows.SecretRowKey, "igdb-secret-paste", "igdb-console", "igdb-test", "igdb-delete"], keys);
        Assert.Equal(AppTexts.DataSourcesIgdbNone, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle);
        Assert.True(Row(arcadia, "igdb-test").IsDimmed);
        Assert.True(Row(arcadia, "igdb-delete").IsDimmed);
        Assert.Contains(Row(arcadia, IgdbAccessRows.ClientIdRowKey).Info, block => block is InfoText { Text: var text } && text.Contains("4 Anfragen pro Sekunde", StringComparison.Ordinal));
        var igdb = Row(arcadia, MetadataSourceRows.SourceKey(MetadataSourceKind.Igdb));
        Assert.Equal((AppTexts.DataSourcesIgdbSkipped, true), (igdb.Subtitle, igdb.IsDimmed));
        Assert.Null(category.Marker);

        // Greyed rows keep the focus.
        Assert.Equal("igdb-test", arcadia.FocusRow("igdb-test").Key);
        Assert.Empty(arcadia.Igdb.Snapshot());
        Assert.Equal("Erfundenes Studio", arcadia.Main.MetadataOf(Game.KeyOf(Shop.Id, "k1")).Developers?.Value[0]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-2")]
    [Trait("TestCase", "T538-6")]
    public async Task Client_id_and_secret_typed_by_controller_are_checked_once_both_are_there()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);

        await TypeAsync(arcadia, IgdbAccessRows.ClientIdRowKey, FakeIgdb.ClientId);
        Assert.Equal(0, arcadia.Igdb.TokenRequests);
        Assert.Equal(AppTexts.DataSourcesIgdbSecretMissing, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle);
        Assert.Equal((RowLamp.Resting, true), (Row(arcadia, IgdbAccessRows.ClientIdRowKey).Lamp, Row(arcadia, IgdbAccessRows.ClientIdRowKey).HasTintedSubtitle));

        await TypeAsync(arcadia, IgdbAccessRows.SecretRowKey, FakeIgdb.Secret);
        Assert.Equal(1, arcadia.Igdb.TokenRequests);
        Assert.Equal(AppTexts.DataSourcesKeyConnected, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle);
        Assert.Equal((AppTexts.DataSourcesKeyConnected, RowLamp.Working), (Row(arcadia, IgdbAccessRows.SecretRowKey).Subtitle, Row(arcadia, IgdbAccessRows.SecretRowKey).Lamp));
        Assert.Equal("…" + FakeIgdb.Secret[^4..], Assert.IsType<TextRowViewModel>(Row(arcadia, IgdbAccessRows.SecretRowKey)).ValueText);

        // B or an empty field keeps the value and sends nothing.
        arcadia.FocusRow(IgdbAccessRows.SecretRowKey);
        arcadia.Press(UiAction.Accept).Press(UiAction.Back);
        arcadia.Press(UiAction.Accept).Press(UiAction.Settings);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, arcadia.Igdb.TokenRequests);
        Assert.Equal("…" + FakeIgdb.Secret[^4..], Assert.IsType<TextRowViewModel>(Row(arcadia, IgdbAccessRows.SecretRowKey)).ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-3")]
    public async Task Einfuegen_trims_a_valid_value_and_keeps_the_old_one_for_anything_else()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);

        arcadia.External.ClipboardText = "  " + FakeIgdb.ClientId + "\r\n";
        arcadia.FocusRow("igdb-client-id-paste");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesIgdbClientIdSavedNeedsSecret, "Client-ID eingefügt");

        foreach (var clipboard in new string?[] { null, "kein Wert", "madeupsecret 0000000000000000001" })
        {
            arcadia.External.ClipboardText = clipboard;
            arcadia.FocusRow("igdb-secret-paste");
            arcadia.Press(UiAction.Accept);
            await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesIgdbPasteNoSecret, "Warnung");
            arcadia.Main.Toast.Show(HealthSeverity.Info, "Zwischenstand");
        }

        Assert.Equal(AppTexts.DataSourcesIgdbSecretNone, Row(arcadia, IgdbAccessRows.SecretRowKey).Subtitle);
        Assert.Empty(arcadia.Igdb.Snapshot());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-5")]
    public async Task Zugang_anlegen_opens_twitchs_console_and_by_controller_asks_for_the_mouse_mode()
    {
        using var arcadia = await StartAsync();
        var forMouse = 0;
        arcadia.Main.OpenedForMouseByController += (_, _) => forMouse++;
        OpenDataSources(arcadia);

        arcadia.FocusRow("igdb-console");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => forMouse == 1, "Mausmodus angefragt");

        Assert.Equal([new Uri("https://dev.twitch.tv/console/apps")], arcadia.External.Opened);
        Assert.Equal(AppTexts.DataSourcesIgdbConsoleOpening, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-6")]
    public async Task Verbindung_testen_says_verbunden_or_keine_verbindung_without_a_warning()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        var category = OpenDataSources(arcadia);
        await GrantAsync(arcadia);

        arcadia.FocusRow("igdb-test");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.DataSourcesIgdbTestOkToast, "Test ok");
        Assert.Equal(AppTexts.DataSourcesIgdbTestOk(arcadia.Time.GetLocalNow()), Row(arcadia, "igdb-test").Subtitle);

        arcadia.Igdb.Offline = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => Row(arcadia, "igdb-test").Subtitle == AppTexts.DataSourcesTestNoConnection, "Keine Verbindung");
        Assert.Null(category.Marker);
        Assert.False(arcadia.Main.Status.HasIgdbWarning);
        Assert.Equal(AppTexts.DataSourcesIgdbTestNoConnectionToast, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-9")]
    public async Task A_rejected_access_warns_at_the_tab_the_row_and_the_status_chip_and_a_valid_secret_clears_them()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        var category = OpenDataSources(arcadia);
        arcadia.Igdb.AcceptedSecret = OtherSecret;
        await GrantAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesIgdbRejectedToast, arcadia.Main.Toast.Text);
        Assert.Equal(HealthSeverity.Warning, category.Marker);
        var row = Row(arcadia, IgdbAccessRows.ClientIdRowKey);
        Assert.Equal((AppTexts.DataSourcesIgdbRejected, HealthSeverity.Warning), (row.Subtitle, row.Severity));
        Assert.True(arcadia.Main.Status.HasIgdbWarning);

        arcadia.Press(UiAction.Back).Press(UiAction.Status);
        var entry = Assert.Single(Assert.IsType<Status.StatusModalViewModel>(arcadia.Main.StatusModal).Entries);
        Assert.Equal(AppTexts.DataSourcesIgdbStatus, entry.Entry.Title);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(IgdbAccessRows.ClientIdRowKey, arcadia.Main.Settings!.FocusedRow?.Key);

        await TypeAsync(arcadia, IgdbAccessRows.SecretRowKey, OtherSecret);
        Assert.Null(arcadia.Main.Settings!.Selected.Marker);
        Assert.False(arcadia.Main.Status.HasIgdbWarning);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-21")]
    [Trait("TestCase", "T494-31")]
    public async Task Igdb_is_lifted_moved_and_dropped_by_controller_b_puts_it_back_and_the_order_stays_after_a_restart()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);
        await GrantAsync(arcadia);
        await RunToIdleAsync(arcadia);
        var key = Game.KeyOf(Shop.Id, "k1");
        Assert.Equal("Köln", arcadia.Metadata.TitleOf(key));

        arcadia.FocusRow(MetadataSourceRows.SourceKey(MetadataSourceKind.Igdb));
        arcadia.Press(UiAction.ViewOptions).Press(UiAction.Up).Press(UiAction.Back);
        Assert.Equal(MetadataSettings.DefaultOrder, arcadia.Settings.Current.Metadata.Order);
        Assert.True(arcadia.Main.IsSettingsOpen);

        arcadia.Press(UiAction.ViewOptions).Press(UiAction.Up).Press(UiAction.Up).Press(UiAction.ViewOptions);
        Assert.Equal([MetadataSourceKind.Igdb, MetadataSourceKind.Launcher], arcadia.Settings.Current.Metadata.Order);
        Assert.Equal(AppTexts.DataSourcesOrderPlace(place: 1, source: AppTexts.DataSourcesIgdb), Row(arcadia, MetadataSourceRows.SourceKey(MetadataSourceKind.Igdb)).Title);

        // ◀ ▶ switch it; on again, nothing was asked anew.
        var requests = arcadia.Igdb.Snapshot().Count;
        arcadia.Press(UiAction.Right);
        await arcadia.WaitUntilAsync(() => arcadia.Metadata.TitleOf(key) is null, "IGDB aus");
        arcadia.Press(UiAction.Left);
        await arcadia.WaitUntilAsync(() => arcadia.Metadata.TitleOf(key) == "Köln", "IGDB an");
        Assert.Equal(requests, arcadia.Igdb.Snapshot().Count);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        using var restarted = await arcadia.RestartAsync();
        Assert.Equal([MetadataSourceKind.Igdb, MetadataSourceKind.Launcher], restarted.Settings.Current.Metadata.Order);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-26")]
    [Trait("TestCase", "T494-25")]
    public async Task Infos_names_igdb_for_its_fields_and_lists_the_broken_launcher_title_only_among_the_sources()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);
        await GrantAsync(arcadia);
        await RunToIdleAsync(arcadia);

        var facts = new DetailMetadata(arcadia.Main.MetadataOf(Game.KeyOf(Shop.Id, "k1"))).Facts;
        Assert.Equal(new DetailFact(AppTexts.DetailTitleCaption, "Köln", "IGDB"), facts[0]);
        Assert.Equal(new DetailFact(string.Empty, "K?ln", Shop.DisplayName), facts[1]);
        Assert.Contains(new DetailFact(AppTexts.DetailGenresCaption, "Abenteuer", "IGDB"), facts);
        Assert.Contains(new DetailFact(AppTexts.DetailDevelopersCaption, "Erfundenes Studio", Shop.DisplayName), facts);
        Assert.Contains(new DetailFact(AppTexts.DetailReleasedCaption, "01.01.2021", "IGDB"), facts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-25")]
    public async Task Igdbs_title_for_a_doubtful_launcher_title_shows_in_the_library_the_sorting_and_the_search()
    {
        using var arcadia = await StartAsync(GameOf("k1", "K?ln"), GameOf("k2", "Kiel"), GameOf("k3", "Kassel"));
        await RunToIdleAsync(arcadia);
        Assert.Equal(["K?ln", "Kassel", "Kiel"], arcadia.PageGames.Select(game => game.Title));
        OpenDataSources(arcadia);
        await GrantAsync(arcadia);
        await RunToIdleAsync(arcadia);
        arcadia.Press(UiAction.Back);

        Assert.Equal(["Kassel", "Kiel", "Köln"], arcadia.PageGames.Select(game => game.Title));
        arcadia.Press(UiAction.Search);
        arcadia.Search!.Type("köln");
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal(["Köln"], arcadia.Search.Hits.Select(game => game.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-7")]
    public async Task The_access_is_there_again_after_a_restart()
    {
        using var arcadia = await StartAsync();
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);
        await GrantAsync(arcadia);
        arcadia.Press(UiAction.Back);

        using var restarted = await arcadia.RestartAsync();
        // Loaded unchecked; the restart's metadata run may have checked it already, so both count as "there again".
        Assert.Contains(restarted.Metadata.Igdb!.State, new[] { IgdbAccessState.Unchecked, IgdbAccessState.Valid });
        Assert.Equal(FakeIgdb.Secret[^4..], restarted.Metadata.Igdb.SecretEnding);
        Assert.DoesNotContain(FakeIgdb.Secret, File.ReadAllText(Path.Combine(restarted.Folder, "settings.json")));
    }
}
