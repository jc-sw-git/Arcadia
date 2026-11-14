using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The group "SPRACHE" of "Lokalisierung" (#554, #556): choosing a language saves it, "Sprache anwenden" restarts Arcadia into it
/// when that changes the language and nothing a restart would cut off runs, and the restarted Arcadia opens on "Sprache".
/// The tests' system language is German unless a test says otherwise; the texts read stay German (the test thread's).
/// </summary>
public sealed class UiLanguageSettingsTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T554-1")]
    public async Task Language_comes_first_with_its_values_in_their_own_language_and_apply_below_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var category = Open(arcadia);

        Assert.Equal([UiLanguageRows.LanguageKey, UiLanguageRows.ApplyKey], category.Rows.Take(2).Select(row => row.Key));
        Assert.Equal(AppTexts.LocalizationLanguageGroup, category.Rows[0].Group);
        Assert.True(category.Rows[0].HasGroupHeading);
        Assert.Equal(1, category.Rows[1].Indent);
        var language = Language(arcadia);
        Assert.Equal([AppTexts.LocalizationLanguageSystem, "English", "Français", "Español", "Deutsch"], Values(language));
        Assert.Equal(AppTexts.LocalizationLanguageSystem, language.ValueText);
        Assert.Equal(4, language.ValueBoxes.Count);
        Assert.True(language.ValueBoxes[0].IsCurrent);

        // ◀ ▶ wrap at both ends.
        arcadia.Press(UiAction.Left);
        Assert.Equal("Deutsch", language.ValueText);
        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.LocalizationLanguageSystem, language.ValueText);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-2")]
    [Trait("TestCase", "T554-3")]
    [Trait("TestCase", "T554-11")]
    [Trait("TestCase", "T556-1")]
    public async Task System_language_is_used_when_Arcadia_speaks_it_otherwise_English_and_the_info_says_which()
    {
        foreach (var (system, inUse) in new[] { ("de-DE", "de-DE"), ("de-AT", "de-AT"), ("en-US", "en-US"), ("en-GB", "en-GB"), ("es-ES", "es-ES"), ("es-MX", "es-MX"), ("it-IT", "en") })
        {
            using var arcadia = await ArcadiaDriver.StartAsync(arguments: new(SystemLanguage: system));
            Assert.Equal(inUse, arcadia.Main.Languages.InUse.Name);
        }

        Assert.Equal(AppTexts.LocalizationLanguageSystemSpoken(language: "Deutsch"), UiLanguageRows.SystemLanguageText(CultureInfo.GetCultureInfo("de-AT")));
        Assert.Equal(AppTexts.LocalizationLanguageSystemUnsupported(language: "Italienisch"), UiLanguageRows.SystemLanguageText(CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal(AppTexts.LocalizationLanguageSystemUnknown, UiLanguageRows.SystemLanguageText(CultureInfo.InvariantCulture));

        // Fresh settings: the first start asks for no language and runs in the system's.
        using var fresh = await ArcadiaDriver.StartAsync(settings: new AppSettings(), arguments: new(FirstRun: true, SystemLanguage: "it-IT"));
        Assert.NotNull(fresh.Main.FirstRun);
        Assert.Equal("en", fresh.Main.Languages.InUse.Name);
    }

    [Theory]
    [Trait("TestCase", "T556-2")]
    [InlineData("es-ES", "El idioma del sistema es italiano. Arcadia aún no está disponible en él, así que con «Idioma del sistema» está en inglés.")]
    [InlineData("en-US", "The system language is Italian. Arcadia isn’t available in it yet, so with “System language” it is in English.")]
    public void The_system_language_is_named_as_the_UI_language_writes_it(string uiCulture, string expected)
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
            Assert.Equal(expected, UiLanguageRows.SystemLanguageText(CultureInfo.GetCultureInfo("it-IT")));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-2")]
    [Trait("TestCase", "T554-10")]
    public async Task The_info_names_the_system_language_and_points_to_reloading_the_metadata()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(arguments: new(SystemLanguage: "it-IT"));
        Open(arcadia);
        var texts = Language(arcadia).Info.OfType<InfoText>().Select(text => text.Text).ToList();

        Assert.Equal(UiLanguageRows.SystemLanguageText(CultureInfo.GetCultureInfo("it-IT")), texts[0]);
        Assert.Contains(AppTexts.LocalizationLanguageMetadata, texts);
        Assert.Contains(AppTexts.DataSourcesMetadataReload, AppTexts.LocalizationLanguageMetadata);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-5")]
    [Trait("TestCase", "T554-12")]
    public async Task Choosing_saves_at_once_and_restarts_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);
        var language = Language(arcadia);

        arcadia.Press(UiAction.Right);
        Assert.Equal("en", arcadia.Settings.Current.Localization.Language);
        arcadia.Press(UiAction.Right);
        Assert.Equal("fr", arcadia.Settings.Current.Localization.Language);
        arcadia.Click(language.StepBackCommand);
        Assert.Equal("en", arcadia.Settings.Current.Localization.Language);
        Assert.Empty(arcadia.Restarts);
        Assert.Equal("de-DE", arcadia.Main.Languages.InUse.Name);

        arcadia.Press(UiAction.Back);
        using var restarted = await arcadia.RestartAsync();
        Assert.Equal("en", restarted.Main.Languages.InUse.Name);
        Open(restarted);
        Assert.Equal("English", Language(restarted).ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-6")]
    public async Task Apply_is_active_only_when_the_resulting_language_differs()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);
        var apply = Apply(arcadia);
        Assert.False(apply.IsEnabled);
        Assert.True(apply.IsDimmed);
        Assert.Equal(AppTexts.LocalizationApplySame, apply.Subtitle);

        Language(arcadia);
        arcadia.Press(UiAction.Left); // Deutsch: the language in use
        Assert.False(Apply(arcadia).IsEnabled);

        Language(arcadia);
        arcadia.Press(UiAction.Left); // Español
        Assert.True(Apply(arcadia).IsEnabled);

        Language(arcadia);
        arcadia.Press(UiAction.Left); // Français
        apply = Apply(arcadia);
        Assert.True(apply.IsEnabled);
        Assert.Equal(AppTexts.LocalizationApplyRestarts, apply.Subtitle);
        Assert.Contains(new InfoText(AppTexts.LocalizationApplyInfo), apply.Info);

        Language(arcadia);
        arcadia.Press(UiAction.Left); // English
        arcadia.Press(UiAction.Left); // Systemsprache: German again
        apply = Apply(arcadia);
        Assert.False(apply.IsEnabled);

        // A greyed row takes the focus; A and a click do nothing.
        arcadia.Press(UiAction.Accept);
        arcadia.Click(apply.ClickCommand);
        Assert.Empty(arcadia.Restarts);

        // On a system Arcadia doesn't speak, "Systemsprache" is English: choosing English changes nothing.
        using var italian = await ArcadiaDriver.StartAsync(arguments: new(SystemLanguage: "it-IT"));
        Open(italian);
        Language(italian);
        italian.Press(UiAction.Right);
        Assert.False(Apply(italian).IsEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-8")]
    [Trait("TestCase", "T554-9")]
    public void Apply_waits_for_the_driver_installation_and_an_update_and_warns_that_a_reload_stops()
    {
        using var folder = new TempSettings();
        var (drivers, update, reload) = (false, false, false);
        var restarts = 0;
        var rows = new UiLanguageRows(folder.Changes, new LanguageRestart(
            CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("de-DE"), () => null, () => drivers, () => update, () => reload, () => restarts++));
        rows.LanguageRow.Change(1);
        var apply = Assert.IsAssignableFrom<ActionRowViewModel>(rows.ApplyRow);

        drivers = true;
        rows.Refresh();
        Assert.False(apply.CanActivate);
        Assert.Equal(AppTexts.LocalizationApplyWaitsForDrivers, apply.Subtitle);
        Assert.Equal(new InfoNote(AppTexts.LocalizationApplyDriversNote, HealthSeverity.Info), apply.Info[0]);

        (drivers, update) = (false, true);
        rows.Refresh();
        Assert.False(apply.CanActivate);
        Assert.Equal(AppTexts.LocalizationApplyWaitsForUpdate, apply.Subtitle);
        Assert.Equal(new InfoNote(AppTexts.LocalizationApplyUpdateNote, HealthSeverity.Info), apply.Info[0]);

        // A reload doesn't hold the restart up; the info says it stops.
        (update, reload) = (false, true);
        rows.Refresh();
        Assert.True(apply.CanActivate);
        Assert.Equal([new InfoText(AppTexts.LocalizationApplyInfo), new InfoText(AppTexts.LocalizationApplyStopsReload)], apply.Info);
        apply.Activate();
        Assert.Equal(1, restarts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-7")]
    [Trait("TestCase", "T554-14")]
    [Trait("TestCase", "T554-15")]
    public async Task Apply_restarts_with_the_choice_saved_and_the_new_Arcadia_opens_on_language()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);
        Language(arcadia);
        arcadia.Press(UiAction.Right);
        Apply(arcadia);
        arcadia.Press(UiAction.Accept);

        Assert.Equal([ArcadiaDriver.LanguageRestart], arcadia.Restarts);
        Assert.Equal("en", arcadia.Settings.Current.Localization.Language);

        // The App's restart failed (it shows a toast then): this Arcadia stays in its language, the row ready for a retry.
        Assert.Equal("de-DE", arcadia.Main.Languages.InUse.Name);
        Assert.True(Apply(arcadia).CanActivate);

        using var restarted = await arcadia.RestartAsync(arguments: new(ShowsLanguageSetting: true));
        Assert.Equal("en", restarted.Main.Languages.InUse.Name);
        Assert.IsType<LocalizationSettingsViewModel>(restarted.Main.Settings?.Selected);
        Assert.Equal(UiLanguageRows.LanguageKey, restarted.Main.Settings!.FocusedRow?.Key);
        Assert.False(Apply(restarted).IsEnabled);
        restarted.Press(UiAction.Back);
        Assert.Null(restarted.Main.Settings);

        // Ended before the restart (a crash): the next start is in the new language, on the library.
        using var afterCrash = await arcadia.RestartAsync();
        Assert.Equal("en", afterCrash.Main.Languages.InUse.Name);
        Assert.Null(afterCrash.Main.Settings);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-8")]
    public async Task Apply_waits_for_a_running_game_and_comes_back_when_it_ends()
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Running: [])]);
        Open(arcadia);
        Language(arcadia);
        arcadia.Press(UiAction.Right);
        Assert.True(Apply(arcadia).IsEnabled);

        arcadia.RunInLauncher(alpha);
        await arcadia.FollowRunningGamesAsync();
        var apply = (ActionRowViewModel)arcadia.Main.Settings!.FocusedRow!;
        Assert.False(apply.IsEnabled);
        Assert.Equal(AppTexts.LocalizationApplyWaitsForGame(game: "Alpha"), apply.Subtitle);
        Assert.Equal(new InfoNote(AppTexts.LocalizationApplyGameNote(game: "Alpha"), HealthSeverity.Info), apply.Info[0]);
        arcadia.Press(UiAction.Accept);
        Assert.Empty(arcadia.Restarts);

        arcadia.EndInLauncher(alpha);
        await arcadia.FollowRunningGamesAsync();
        Assert.Same(apply, arcadia.Main.Settings.FocusedRow);
        Assert.True(apply.IsEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-8")]
    public async Task Apply_waits_for_a_game_that_is_starting()
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, alpha)]);
        arcadia.Starts.Stage = LaunchStage.Starting;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "the launcher starts Alpha");

        Open(arcadia);
        Language(arcadia);
        arcadia.Press(UiAction.Right);
        var apply = Apply(arcadia);

        Assert.False(apply.IsEnabled);
        Assert.Equal(AppTexts.LocalizationApplyWaitsForGame(game: "Alpha"), apply.Subtitle);
        arcadia.Press(UiAction.Accept);
        Assert.Empty(arcadia.Restarts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T554-9")]
    public async Task Apply_during_a_metadata_reload_warns_that_it_stops_and_the_restarted_Arcadia_doesnt_go_on_with_it()
    {
        var store = new LauncherInfo("store", "Shop", "shop", null);
        var games = new[] { "4000001", "4000002" }.Select(id => new Game
        {
            Id = id,
            LauncherId = store.Id,
            Title = "Made-up game " + id,
            Launch = new LaunchTarget.ProtocolUri("shop://run/" + id),
        }).ToArray();
        using var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(store, games) with { Metadata = new FakeMetadataSource() }],
            new AppSettings { PanEnabled = true, ActiveLaunchers = [store.Id] },
            stores: server => server.Serve("4000001", "A made-up description that is long enough to be kept as it is."));
        await arcadia.WaitUntilAsync(() => arcadia.Metadata.GameCount > 0, "library at the metadata service");
        arcadia.MetadataStore.Hang("4000002");

        // "Alle Metadaten neu laden" asks first; A twice confirms it, and the hanging store keeps it running.
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        arcadia.Press(UiAction.NextPage);
        var reload = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(MetadataRows.ReloadKey));
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        Assert.True(reload.IsWorking);
        arcadia.Press(UiAction.Back);

        Open(arcadia);
        Language(arcadia);
        arcadia.Press(UiAction.Right);
        var apply = Apply(arcadia);
        Assert.True(apply.IsEnabled);
        Assert.Contains(new InfoText(AppTexts.LocalizationApplyStopsReload), apply.Info);
        arcadia.Press(UiAction.Accept);
        Assert.Equal([ArcadiaDriver.LanguageRestart], arcadia.Restarts);

        using var restarted = await arcadia.RestartAsync(arguments: new(ShowsLanguageSetting: true));
        Assert.DoesNotContain(new InfoText(AppTexts.LocalizationApplyStopsReload), Apply(restarted).Info);
        Assert.False(restarted.Metadata.Progress is { Reload: true });
    }

    /// <summary>Settings in a folder of their own, for the rows without the rest of Arcadia.</summary>
    private sealed class TempSettings : IDisposable
    {
        private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-language-").FullName;

        public TempSettings() => Changes = new SettingsChanges(new SettingsService(new SettingsStore(Path.Combine(_folder, "settings.json"))));

        public SettingsChanges Changes { get; }

        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }

    private static LocalizationSettingsViewModel Open(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var tabs = 0; tabs < 8 && arcadia.Main.Settings!.Selected is not LocalizationSettingsViewModel; tabs++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        return Assert.IsType<LocalizationSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static ChoiceRowViewModel Language(ArcadiaDriver arcadia) =>
        Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow(UiLanguageRows.LanguageKey));

    private static ActionRowViewModel Apply(ArcadiaDriver arcadia) =>
        Assert.IsAssignableFrom<ActionRowViewModel>(arcadia.FocusRow(UiLanguageRows.ApplyKey));

    private static List<string> Values(ChoiceRowViewModel row) => [.. row.ValueBoxes.Select(box => box.Title)];
}
