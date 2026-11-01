using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam Controller mode's default per launcher (#178): the group "Controller" on every launcher page, the three values
/// per game ("Standard (…)", "Arcadia", "Steam-Eingabe"), "Alle Spiele auf die Vorauswahl setzen", and Pan off.
/// </summary>
public sealed class LauncherControllerModeTests
{
    private const string ModeKey = "launcher-controller-mode";
    private const string ResetKey = "launcher-controller-reset";

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    private static AppSettings Active(params string[] launchers) => new() { ActiveLaunchers = launchers };

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync(AppSettings? settings = null, IReadOnlyList<LauncherSpec>? launchers = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(launchers ?? SteamAndGog, settings ?? Active("steam", "gog"));
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    /// <summary>T178-1: every launcher's page has "Steam Controller" under "Controller", on its launcher's own default.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-1")]
    public async Task Every_launcher_page_shows_the_mode_on_the_launchers_own_default_and_arrows_change_it()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        var launchers = arcadia.OpenLauncherSettings();

        var steam = ModeRow(arcadia, launchers, "steam");
        Assert.Equal(("CONTROLLER", "Steam Controller", "Vorauswahl für die Spiele dieses Launchers"), (steam.Group, steam.Title, steam.Subtitle));
        Assert.Equal(ControllerModes.Steam, steam.ValueText);
        Assert.Contains(steam.Info, block => block is InfoFootnote { Text: "Ab Werk: Steam-Eingabe, weil Spiele aus Steam Steams Belegungen nutzen. Gilt ab dem nächsten Spielstart." });
        Assert.Contains(steam.Info, block => block is InfoCard { Title: ControllerModes.Arcadia });
        Assert.True(steam.CanChange);
        Assert.DoesNotContain(launchers.Rows, row => row.Key == ResetKey);

        arcadia.Press(UiAction.Back);
        var gog = ModeRow(arcadia, launchers, "gog");
        Assert.Equal(ControllerModes.Arcadia, gog.ValueText);
        Assert.Contains(gog.Info, block => block is InfoFootnote { Text: "Ab Werk: Arcadia. Gilt ab dem nächsten Spielstart." });

        // ◀ and ▶ both switch between the two (a choice of two wraps); the launcher's own value is stored as none.
        arcadia.Press(UiAction.Left);
        Assert.Equal(ControllerModes.Steam, gog.ValueText);
        Assert.False(arcadia.Settings.Current.PreferencesOf("gog").UsesArcadiaPad);
        arcadia.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Arcadia, gog.ValueText);
        Assert.Empty(arcadia.Settings.Current.Launchers);
    }

    /// <summary>
    /// T178-2: a changed default reaches every game of the launcher on "Standard", at its next start; a game with its own
    /// choice keeps it, and the other launcher's games stay as they were.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-2")]
    public async Task A_changed_default_moves_the_games_on_Standard_at_their_next_start_and_own_choices_stay()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("GOG Galaxy", arcadia.SelectedTab?.Name);
        var ownChoice = arcadia.FocusedGame!.Game;
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);

        var launchers = arcadia.OpenLauncherSettings();
        ModeRow(arcadia, launchers, "gog");
        arcadia.Press(UiAction.Right);
        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsSettingsOpen);

        Assert.Equal(ownChoice.Key, arcadia.FocusedGame!.Game.Key);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);
        arcadia.Press(UiAction.Right);
        Assert.Equal("Standard (Steam-Eingabe)", arcadia.Main.FocusedControllerSupportText);
        arcadia.Press(UiAction.PreviousTab);
        Assert.Equal("Standard (Steam-Eingabe)", arcadia.Main.FocusedControllerSupportText);

        // The GOG game on "Standard" now starts without Arcadia's pad; the one with its own choice keeps it.
        arcadia.Press(UiAction.NextTab).Press(UiAction.Right);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.XboxPad.IsPlugged);
        Assert.False(arcadia.ControllerSupport.UsesArcadiaPad(arcadia.Starts.Keys[0]));
        Assert.True(arcadia.ControllerSupport.UsesArcadiaPad(ownChoice.Key));
    }

    /// <summary>T178-3: [A] on the detail page goes Standard → Arcadia → Steam-Eingabe → Standard; Standard names the default.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-3")]
    public async Task The_detail_page_cycles_through_Standard_Arcadia_and_Steam_Eingabe()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        var page = arcadia.Detail!.Current;
        arcadia.Press(UiAction.Down);
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedKind);

        string[] shown = [ModeOf(page)];
        for (var press = 0; press < 3; press++)
        {
            arcadia.Press(UiAction.Accept);
            shown = [.. shown, ModeOf(page)];
        }

        Assert.Equal(["Standard (Steam-Eingabe)", ControllerModes.Arcadia, ControllerModes.Steam, "Standard (Steam-Eingabe)"], shown);
        Assert.Empty(arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.Equal("Wechselt zu „Arcadia“", page.GameSettingsRow.Single(action => action.Kind == GameActionKind.ControllerMode).Note);

        // Steam's default changed in the settings: back on the page, "Standard" names the new one.
        arcadia.Press(UiAction.Settings);
        var launchers = Assert.IsType<LauncherSettingsViewModel>(arcadia.Main.Settings?.Selected);
        ModeRow(arcadia, launchers, "steam");
        arcadia.Press(UiAction.Right);
        arcadia.Press(UiAction.Settings);
        Assert.Equal("Standard (Arcadia)", ModeOf(arcadia.Detail!.Current));
        Assert.Equal("Wechselt zu „Arcadia“", arcadia.Detail.Current.GameSettingsRow.Single(action => action.Kind == GameActionKind.ControllerMode).Note);
    }

    /// <summary>T178-3: the preview's value (click) and the context menu (⧉) cycle like the detail page (QE review PR #319).</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-3")]
    public async Task The_preview_and_the_context_menu_cycle_through_the_same_three_values()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        string[] shown = [arcadia.Main.FocusedControllerSupportText];
        for (var click = 0; click < 3; click++)
        {
            arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
            shown = [.. shown, arcadia.Main.FocusedControllerSupportText];
        }

        Assert.Equal(["Standard (Steam-Eingabe)", ControllerModes.Arcadia, ControllerModes.Steam, "Standard (Steam-Eingabe)"], shown);

        arcadia.Press(UiAction.Options);
        var mode = arcadia.Main.GameMenu!.Actions.Single(action => action.Kind == GameActionKind.ControllerMode);
        Assert.Equal("Wechselt zu „Arcadia“", mode.Note);
        mode.Invoke();
        Assert.True(arcadia.Settings.Current.ControllerModeChoiceFor(arcadia.FocusedGame!.Game.Key));
        Assert.Equal((ControllerModes.Arcadia, "Wechselt zu „Steam-Eingabe“"), (mode.ButtonLabel, mode.Note));
    }

    /// <summary>
    /// T178-4: "Alle Spiele auf die Vorauswahl setzen" shows only while library games have their own mode, counts and lists
    /// them; [A] asks, [B] keeps everything, [A] in the question puts every game of the launcher on "Standard", also one no
    /// longer in the library; the row goes, the focus is on "Steam Controller", and there is no toast.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-4")]
    public async Task Resetting_asks_first_and_puts_every_game_of_the_launcher_on_Standard()
    {
        var settings = Active("steam", "gog")
            .WithControllerModeChoice("steam:missing-from-library", true)
            .WithControllerModeChoice("gog:1", false);
        using var arcadia = await StartWithSteamControllerAsync(settings);
        var games = arcadia.Main.Session.Libraries.Single(library => library.Launcher.Id == "steam").Games.OrderBy(game => game.Title).ToList();
        arcadia.Settings.Update(s => s.WithControllerModeChoice(games[0].Key, true).WithControllerModeChoice(games[1].Key, false));

        var launchers = arcadia.OpenLauncherSettings();
        ModeRow(arcadia, launchers, "steam");
        var reset = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ResetKey));
        Assert.True(reset.IsIndented);
        Assert.Equal(("Alle Spiele auf die Vorauswahl setzen", "2 Spiele haben einen eigenen Modus", "Zurücksetzen"), (reset.Title, reset.Subtitle, reset.ActionText));
        var listed = Assert.Single(reset.Info.OfType<InfoFacts>()).Facts;
        Assert.Equal([(games[0].Title, ControllerModes.Arcadia), (games[1].Title, ControllerModes.Steam)], listed.Select(fact => (fact.Label, fact.Value)));

        // [A] asks; [B] keeps every choice.
        arcadia.Press(UiAction.Accept);
        var question = Assert.IsType<SettingsQuestionViewModel>(launchers.Dialog);
        Assert.Equal("Alle Spiele auf die Vorauswahl setzen?", question.Title);
        Assert.Equal($"2 Spiele aus Steam bekommen wieder „Steam-Eingabe“: {games[0].Title}, {games[1].Title}. Gilt ab dem nächsten Spielstart.", question.Message);
        arcadia.Press(UiAction.Back);
        Assert.Null(launchers.Dialog);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.Equal(4, arcadia.Settings.Current.ControllerSupportOverrides.Count);

        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        Assert.Null(launchers.Dialog);
        Assert.Equal(new Dictionary<string, bool> { ["gog:1"] = false }, arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.Equal(new Dictionary<string, bool> { ["gog:1"] = false }, new SettingsStore(arcadia.SettingsPath).Load().ControllerSupportOverrides);
        Assert.DoesNotContain(launchers.Rows, row => row.Key == ResetKey);
        Assert.Equal(ModeKey, arcadia.Main.Settings!.FocusedRow?.Key);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    /// <summary>T178-5: with Pan off both rows are greyed out, still focusable, change nothing, and keep their values.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-5")]
    public async Task With_Pan_off_both_rows_are_greyed_out_reachable_and_change_nothing()
    {
        var settings = Active("steam", "gog").WithControllerModeChoice("gog:demo-0", false) with { PanEnabled = false };
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, settings);
        var launchers = arcadia.OpenLauncherSettings();

        var mode = ModeRow(arcadia, launchers, "gog");
        Assert.True(mode.IsDimmed);
        Assert.False(mode.CanChange);
        Assert.Equal("Aus – erst mit dem Controller-Wächter (Pan)", mode.Subtitle);
        Assert.Contains(mode.Info, block => block is InfoNote { Text: "Einstellbar, sobald der Controller-Wächter (Pan) an ist: Controller → Pan." });
        arcadia.Press(UiAction.Right).Press(UiAction.Left).Press(UiAction.Accept);
        Assert.Equal(ControllerModes.Arcadia, mode.ValueText);

        var reset = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ResetKey));
        Assert.True(reset.IsDimmed);
        Assert.False(reset.CanActivate);
        arcadia.Press(UiAction.Accept);
        Assert.Null(launchers.Dialog);
        Assert.Equal(new Dictionary<string, bool> { ["gog:demo-0"] = false }, arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.Empty(arcadia.Settings.Current.Launchers);

        // Pan switched on in "Controller": back on the tab and the page, both rows can change again.
        arcadia.Press(UiAction.NextTab);
        arcadia.Settings.Update(s => s with { PanEnabled = true });
        arcadia.Press(UiAction.PreviousTab);
        Assert.Same(mode, ModeRow(arcadia, launchers, "gog"));
        Assert.False(mode.IsDimmed);
        Assert.True(mode.CanChange);
        Assert.Equal("Vorauswahl für die Spiele dieses Launchers", mode.Subtitle);
        Assert.True(reset.CanActivate);
    }

    /// <summary>
    /// T178-5 where Pan can't run (Linux): switched on but unsupported, the rows are greyed out as with Pan off (QE review
    /// PR #319).
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-5")]
    public async Task Where_Pan_cant_run_both_rows_are_greyed_out_although_it_is_switched_on()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, Active("steam", "gog"), arguments: new StartArguments(PanUnsupported: true));
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.Settings.Current.PanEnabled);
        var launchers = arcadia.OpenLauncherSettings();

        var mode = ModeRow(arcadia, launchers, "gog");
        Assert.True(mode.IsDimmed);
        Assert.False(mode.CanChange);
        Assert.Equal("Auf diesem System noch nicht verfügbar", mode.Subtitle);
        arcadia.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Arcadia, mode.ValueText);
        Assert.Empty(arcadia.Settings.Current.Launchers);
    }

    /// <summary>T178-6: the link "Controller-Einstellungen für Steam" (#169) is gone from Steam's page.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-6")]
    public async Task Steams_page_no_longer_links_to_the_controller_settings()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, Active("steam", "gog"));
        var launchers = arcadia.OpenLauncherSettings();
        ModeRow(arcadia, launchers, "steam");

        // Below the group "Controller" only "Desktop-Steuerung" (#315).
        Assert.Equal([ModeKey, SteamDesktopLayoutRowViewModel.RowKey], launchers.Rows.TakeLast(2).Select(row => row.Key));
        Assert.DoesNotContain(launchers.Rows, row => row.Title == "Controller-Einstellungen für Steam");
    }

    /// <summary>
    /// T178-7: a default and the games' own choices survive a restart; a settings.json from before (only choices against
    /// the fixed default) keeps every game's mode: those choices are the games' own, all others are on "Standard".
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-7")]
    public async Task Defaults_and_own_choices_survive_a_restart_and_old_choices_become_own_choices()
    {
        // As an Arcadia before #178 left it: a Steam game set to Arcadia, a GOG game to Steam-Eingabe.
        var old = Active("steam", "gog").WithControllerModeChoice("steam:demo-0", true).WithControllerModeChoice("gog:demo-0", false);
        using var arcadia = await StartWithSteamControllerAsync(old);
        Assert.Equal((ControllerModes.Arcadia, "Standard (Steam-Eingabe)"), (ModeOfGame(arcadia, "steam:demo-0"), ModeOfGame(arcadia, "steam:demo-1")));

        var launchers = arcadia.OpenLauncherSettings();
        ModeRow(arcadia, launchers, "gog");
        arcadia.Press(UiAction.Right);
        Assert.Equal("1 Spiel hat einen eigenen Modus", launchers.Rows.Single(row => row.Key == ResetKey).Subtitle);
        arcadia.Press(UiAction.Settings);

        using var restarted = await arcadia.RestartAsync();
        restarted.Sdl.Plug(KnownPads.SteamController);
        restarted.StartControllers();
        await restarted.TickSteamControllerAsync();
        Assert.Equal((ControllerModes.Steam, "Standard (Steam-Eingabe)"), (ModeOfGame(restarted, "gog:demo-0"), ModeOfGame(restarted, "gog:demo-1")));
        Assert.Equal((ControllerModes.Arcadia, "Standard (Steam-Eingabe)"), (ModeOfGame(restarted, "steam:demo-0"), ModeOfGame(restarted, "steam:demo-1")));
    }

    /// <summary>T178-8: an inactive launcher's page has the rows too and keeps what is set there.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-8")]
    public async Task An_inactive_launchers_default_can_be_changed_too()
    {
        using var arcadia = await StartWithSteamControllerAsync(Active("steam"));
        var launchers = arcadia.OpenLauncherSettings();

        var gog = ModeRow(arcadia, launchers, "gog");
        Assert.True(gog.CanChange);
        arcadia.Press(UiAction.Right);
        Assert.False(arcadia.Settings.Current.LauncherUsesArcadiaPad("gog", arcadia.Main.Session.Launchers.GamesUseSteamInput));
        Assert.False(arcadia.ControllerSupport.UsesArcadiaPad("gog:demo-0"));
    }

    /// <summary>T178-9 (#119): a Steam game started directly in Steam and not in the library gets Steam's default.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-9")]
    public async Task A_game_started_directly_in_its_launcher_gets_the_launchers_default()
    {
        using var arcadia = await StartWithSteamControllerAsync(Active("steam"), [new LauncherSpec(KnownLaunchers.Steam, 3)]);
        arcadia.Settings.Update(s => s.WithLauncherControllerMode("steam", useArcadiaPad: true, arcadia.Main.Session.Launchers.GamesUseSteamInput));

        arcadia.RunningSteamAppId = 4_000_000;
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);
        Assert.Equal(0, arcadia.XboxPads.Unplugged);

        // Its own choice still wins.
        arcadia.Settings.Update(s => s.WithControllerModeChoice("steam:4000000", false));
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.XboxPad.IsPlugged);
    }

    /// <summary>
    /// T178-4: the question names one game in the singular and, with more than five, the first five in order and how many
    /// more there are (QE review PR #319).
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-4")]
    public async Task The_question_names_one_game_alone_and_at_most_five_of_many()
    {
        using var arcadia = await StartWithSteamControllerAsync(Active("steam"), [new LauncherSpec(KnownLaunchers.Steam, 7)]);
        var titles = arcadia.Main.Session.Libraries.Single().Games.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        arcadia.Settings.Update(s => s.WithControllerModeChoice(titles[0].Key, true));
        var launchers = arcadia.OpenLauncherSettings();
        ModeRow(arcadia, launchers, "steam");
        arcadia.FocusRow(ResetKey);
        arcadia.Press(UiAction.Accept);
        Assert.Equal($"1 Spiel aus Steam bekommt wieder „Steam-Eingabe“: {titles[0].Title}. Gilt ab dem nächsten Spielstart.", Assert.IsType<SettingsQuestionViewModel>(launchers.Dialog).Message);
        arcadia.Press(UiAction.Back);

        arcadia.Settings.Update(s => titles.Aggregate(s, (all, game) => all.WithControllerModeChoice(game.Key, true)));
        ModeRow(arcadia, launchers, "steam");
        Assert.Equal("7 Spiele haben einen eigenen Modus", arcadia.FocusRow(ResetKey).Subtitle);
        arcadia.Press(UiAction.Accept);
        var named = string.Join(", ", titles.Take(5).Select(game => game.Title));
        Assert.Equal($"7 Spiele aus Steam bekommen wieder „Steam-Eingabe“: {named} und 2 weitere. Gilt ab dem nächsten Spielstart.", Assert.IsType<SettingsQuestionViewModel>(launchers.Dialog).Message);
    }

    /// <summary>From the launcher list to the launcher's page, then ▼ to "Steam Controller".</summary>
    private static LauncherControllerModeRowViewModel ModeRow(ArcadiaDriver arcadia, LauncherSettingsViewModel launchers, string launcherId)
    {
        if (launchers.PageLauncherId is not null)
        {
            arcadia.Press(UiAction.Back);
        }

        arcadia.FocusRow(launcherId);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(launcherId, launchers.PageLauncherId);
        return Assert.IsType<LauncherControllerModeRowViewModel>(arcadia.FocusRow(ModeKey));
    }

    private static string ModeOf(GameDetailPageViewModel page) =>
        page.GameSettingsRow.Single(action => action.Kind == GameActionKind.ControllerMode).ButtonLabel;

    /// <summary>The mode the library's preview shows for the game with this key, reached by LB/RB and ◀ ▶ as the user would.</summary>
    private static string ModeOfGame(ArcadiaDriver arcadia, string key)
    {
        var launcherId = Game.LauncherIdOf(key);
        for (var tabs = 0; arcadia.SelectedTab?.Library.Launcher.Id != launcherId; tabs++)
        {
            Assert.True(tabs < arcadia.Main.LauncherTabs.Items.Count, $"Tab {launcherId} nicht erreicht");
            arcadia.Press(UiAction.NextTab);
        }

        var target = arcadia.PageGames.Select(game => game.Game.Key).ToList().IndexOf(key);
        Assert.True(target >= 0, $"Spiel {key} nicht auf der Seite");
        for (var games = 0; arcadia.FocusedGame?.Game.Key != key; games++)
        {
            Assert.True(games < arcadia.PageGames.Count, $"Spiel {key} nicht erreicht");
            arcadia.Press(arcadia.PageGames.ToList().IndexOf(arcadia.FocusedGame!) < target ? UiAction.Right : UiAction.Left);
        }

        return arcadia.Main.FocusedControllerSupportText;
    }
}
