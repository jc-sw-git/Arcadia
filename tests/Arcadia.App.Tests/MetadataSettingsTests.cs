using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #493: the group "METADATEN" of "Datenquellen" through the view models, with a made-up launcher, its fake store and
/// fake time: the launcher's row, "Fehlende Metadaten" and "Alle Metadaten neu laden" by controller
/// (T493-24 to T493-28).
/// </summary>
public sealed class MetadataSettingsTests
{
    private static readonly LauncherInfo Store = new("store", "Shop", "shop", null);

    private static Game GameOf(string id) => new()
    {
        Id = id,
        LauncherId = Store.Id,
        Title = "Erfundenes Spiel " + id,
        Launch = new LaunchTarget.ProtocolUri("shop://run/" + id),
    };

    /// <param name="served">The games whose store page answers from the start.</param>
    private static Task<ArcadiaDriver> StartAsync(string[] ids, params string[] served) => ArcadiaDriver.StartAsync(
        [LauncherSpec.With(Store, [.. ids.Select(GameOf)]) with { Metadata = new FakeMetadataSource() }],
        new AppSettings { PanEnabled = true, ActiveLaunchers = [Store.Id] },
        stores: store =>
        {
            foreach (var id in served)
            {
                store.Serve(id, "Beschreibung von " + id + ", die dir gefällt und nicht zu lang ist.");
            }
        });

    /// <summary>Lets fake time pass until the metadata run is idle, the UI thread following.</summary>
    private static async Task RunToIdleAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 3000 && !arcadia.Metadata.WhenIdleAsync().IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromSeconds(1));
        }

        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>≡, RB to "Datenquellen", RT to its tab "Metadaten".</summary>
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

    [Theory]
    [Trait("TestCase", "T493-24")]
    [InlineData(412, 1000, 20 * 60, "412 von 1.000, noch etwa 20 Minuten")]
    [InlineData(3, 1000, 70, "3 von 1.000, noch etwa 1 Minute")]
    [InlineData(1, 1000, 20, "1 von 1.000, weniger als eine Minute")]
    [InlineData(1200, 5000, null, "1.200 von 5.000")]
    [InlineData(0, 1000, null, "Alle Metadaten geladen")]
    [InlineData(0, 0, null, "Arcadia lädt nach dem Einlesen von selbst")]
    public void The_progress_reads_in_german_number_format(int missing, int total, int? seconds, string expected)
    {
        var progress = new MetadataProgress(missing, total, seconds is not null, false, seconds is { } s ? TimeSpan.FromSeconds(s) : null);

        Assert.Equal(expected, MetadataRows.MissingText(progress));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-24")]
    [Trait("TestCase", "T538-5")]
    public async Task Launcher_daten_has_a_box_per_launcher_with_what_it_delivers_and_no_row_of_its_own()
    {
        using var arcadia = await StartAsync(["4000001"], "4000001");
        await RunToIdleAsync(arcadia);
        var category = OpenDataSources(arcadia);

        Assert.DoesNotContain(category.Rows, row => row.Key.StartsWith("metadata-provider-", StringComparison.Ordinal));
        var launcherData = arcadia.FocusRow(MetadataSourceRows.SourceKey(MetadataSourceKind.Launcher));
        var box = Assert.Single(launcherData.Info.OfType<InfoLauncherBox>());
        Assert.Equal(Store.DisplayName, box.Name);
        Assert.Equal(["Made-up summary", "Made-up about"], box.Blocks.OfType<InfoText>().Select(t => t.Text));
        Assert.Equal(AppTexts.DataSourcesMetadataMissingNone, Row(arcadia, MetadataRows.MissingKey).Subtitle);
        Assert.True(Row(arcadia, MetadataRows.MissingKey).IsDimmed);
        Assert.False(Row(arcadia, MetadataRows.MissingKey).CanActivate);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T538-4")]
    public async Task Jetzt_suchen_asks_at_once_for_what_is_missing_without_a_scan()
    {
        using var arcadia = await StartAsync(["4000001", "4000002"], "4000001");
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);
        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(MetadataRows.MissingKey));
        Assert.Equal((AppTexts.DataSourcesMetadataMissingCount(missing: "1", total: "2"), AppTexts.DataSourcesSearchNow, true), (missing.Subtitle, missing.ActivateText, missing.CanActivate));

        arcadia.MetadataStore.Serve("4000002", "Beschreibung von 4000002, die dir gefällt und nicht zu lang ist.");
        arcadia.Press(UiAction.Accept);
        Assert.Equal((true, true, AppTexts.DataSourcesSearchCancel), (missing.IsWorking, missing.CanActivate, missing.ActionText));
        await RunToIdleAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesMetadataMissingNone, missing.Subtitle);
        Assert.False(missing.IsWorking);
        Assert.Equal((true, false), (missing.IsDimmed, missing.CanActivate));
        Assert.NotNull(arcadia.Metadata.Of(Store.Id + ":4000002").Description);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-25")]
    public async Task The_counts_follow_the_run_a_skipped_game_stays_missing_and_nothing_is_toasted()
    {
        using var arcadia = await StartAsync(["4000001", "4000002", "4000003"], "4000001", "4000003");
        OpenDataSources(arcadia);
        var toasts = new List<string?>();
        arcadia.Main.Toast.PropertyChanged += (_, _) => toasts.Add(arcadia.Main.Toast.Text);

        // While the run goes the row names the time left and shows a bar.
        await arcadia.WaitUntilAsync(() => Row(arcadia, MetadataRows.MissingKey).Subtitle == AppTexts.DataSourcesMetadataMissingUnderAMinute(missing: "2", total: "3"), "2 von 3");
        Assert.Equal(100.0 / 3, Row(arcadia, MetadataRows.MissingKey).Progress!.Value, 3);
        await RunToIdleAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesMetadataMissingCount(missing: "1", total: "3"), Row(arcadia, MetadataRows.MissingKey).Subtitle);
        Assert.Null(Row(arcadia, MetadataRows.MissingKey).Progress);
        Assert.Empty(toasts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T547-1")]
    public async Task While_a_run_goes_the_focused_rows_info_stays_where_it_was_scrolled()
    {
        using var arcadia = await StartAsync(["4000001", "4000002", "4000003"], "4000001", "4000003");
        OpenDataSources(arcadia);
        var row = arcadia.FocusRow(MetadataSourceRows.SourceKey(MetadataSourceKind.Launcher));
        var resets = new List<string>();
        arcadia.Main.Settings!.FocusMoved += (_, _) => resets.Add("FocusMoved");
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "ValueText" or nameof(SettingsRowViewModel.IsDimmed) or nameof(SettingsRowViewModel.Info))
            {
                resets.Add(e.PropertyName);
            }
        };

        await RunToIdleAsync(arcadia);

        Assert.Empty(resets);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-26")]
    [Trait("TestCase", "T493-28")]
    public async Task Alle_Metadaten_neu_laden_asks_first_b_sends_nothing_and_a_reloads_every_game()
    {
        using var arcadia = await StartAsync(["4000001", "4000002"], "4000001", "4000002");
        await RunToIdleAsync(arcadia);
        OpenDataSources(arcadia);
        var asked = arcadia.MetadataStore.RequestCount;

        arcadia.FocusRow(MetadataRows.ReloadKey);
        arcadia.Press(UiAction.Accept);
        var question = Assert.IsType<SettingsQuestionViewModel>(arcadia.Main.Settings!.Selected.Dialog);
        Assert.Equal((AppTexts.DataSourcesMetadataReloadAsk, AppTexts.DataSourcesMetadataReloadAskText("2")), (question.Title, question.Message));
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings.Selected.Dialog);
        await RunToIdleAsync(arcadia);
        Assert.Equal(asked, arcadia.MetadataStore.RequestCount);

        arcadia.MetadataStore.Serve("4000001", "Neue Beschreibung, die dir gefällt und nicht zu lang ist.");
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Settings.Selected.Dialog);
        var reload = Assert.IsType<ActionRowViewModel>(Row(arcadia, MetadataRows.ReloadKey));
        Assert.Equal((true, true, AppTexts.DataSourcesReloadCancel), (reload.IsWorking, reload.CanActivate, reload.ActionText));
        await RunToIdleAsync(arcadia);

        Assert.Equal(asked + 2, arcadia.MetadataStore.RequestCount);
        Assert.StartsWith("Neue Beschreibung", arcadia.Main.MetadataOf(Game.KeyOf(Store.Id, "4000001")).Description!.Value);
        Assert.False(Assert.IsType<ActionRowViewModel>(Row(arcadia, MetadataRows.ReloadKey)).IsWorking);

        // B leaves the settings as everywhere.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T547-4")]
    public async Task Suche_abbrechen_and_neu_laden_abbrechen_stop_the_run_on_a_and_the_rows_offer_their_action_again()
    {
        using var arcadia = await StartAsync(["4000001", "4000002", "4000003"], "4000001", "4000003");
        arcadia.MetadataStore.Hang("4000002");
        OpenDataSources(arcadia);
        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(MetadataRows.MissingKey));
        await arcadia.WaitUntilAsync(() => missing.ActionText == AppTexts.DataSourcesSearchCancel, "Suche abbrechen");
        Assert.Equal((true, true), (missing.IsWorking, missing.CanActivate));

        arcadia.Press(UiAction.Accept);
        await RunToIdleAsync(arcadia);

        Assert.Equal((AppTexts.DataSourcesSearchNow, false, true), (missing.ActionText, missing.IsWorking, missing.CanActivate));
        Assert.DoesNotContain("4000003", arcadia.MetadataStore.AskedIds);

        // What is missing is asked for again with "Jetzt suchen"; then a reload is stopped from its own row.
        arcadia.MetadataStore.Serve("4000002", "Beschreibung von 4000002, die dir gefällt und nicht zu lang ist.");
        arcadia.Press(UiAction.Accept);
        await RunToIdleAsync(arcadia);
        Assert.Equal(AppTexts.DataSourcesMetadataMissingNone, missing.Subtitle);

        arcadia.MetadataStore.Hang("4000002");
        var reload = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(MetadataRows.ReloadKey));
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        Assert.Equal((AppTexts.DataSourcesReloadCancel, true, true), (reload.ActionText, reload.IsWorking, reload.CanActivate));
        Assert.True(missing.IsDimmed);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Settings!.Selected.Dialog);
        await RunToIdleAsync(arcadia);

        Assert.Equal((AppTexts.DataSourcesMetadataReloadAction, false), (reload.ActionText, reload.IsWorking));
        Assert.Equal(AppTexts.DataSourcesMetadataMissingNone, missing.Subtitle);
        Assert.NotNull(arcadia.Metadata.Of(Store.Id + ":4000002").Description);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-27")]
    public async Task A_reload_without_any_network_says_so_once_and_a_background_run_says_nothing()
    {
        using var arcadia = await StartAsync(["4000001"]);
        arcadia.MetadataStore.Offline = true;
        await RunToIdleAsync(arcadia);
        Assert.NotEqual(AppTexts.DataSourcesMetadataReloadNoConnection, arcadia.Main.Toast.Text);

        OpenDataSources(arcadia);
        arcadia.FocusRow(MetadataRows.ReloadKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        await RunToIdleAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesMetadataReloadNoConnection, arcadia.Main.Toast.Text);
    }
}
