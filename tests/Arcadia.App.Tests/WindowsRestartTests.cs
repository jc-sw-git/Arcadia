using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The controller setup after an update (#382) as the player meets it: no wrong HidHide hint without HidHide, Pan paused
/// while a removed driver waits for the Windows restart, the one restart row with "Windows neu starten" and its question,
/// and nothing that starts by itself afterwards. Pan, the drivers and Windows' restart are faked; the real ones are the
/// owner's cases T382-30 to T382-34.
/// </summary>
public sealed class WindowsRestartTests
{
    private static readonly string RestartRowKey = PanRows.IssueKey(EmulationParts.WindowsRestartId);
    private const string HidHideMissingKey = "issue:hidhide-missing";
    private const string PausedSubtitle = "Pausiert bis zum Neustart von Windows";

    /// <summary>
    /// Arcadia with Pan on and a Steam Controller whose copy Pan hides; a GOG library whose launcher reports running
    /// games, for the cases where a game is in the way. The parts are checked as at the App's start, before Pan's first tick.
    /// </summary>
    private static async Task<ArcadiaDriver> StartAsync(FakeSteam steam)
    {
        var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Gog, 3, Running: [])], new AppSettings { PanEnabled = true, ActiveLaunchers = ["gog"] }, steam);
        await PlugSteamControllerAsync(arcadia);
        return arcadia;
    }

    /// <summary>The App's start order (<c>App.axaml.cs</c>): the parts first, then Pan's emulation.</summary>
    private static async Task PlugSteamControllerAsync(ArcadiaDriver arcadia)
    {
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        await arcadia.TickControllerSupportAsync();
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
    }

    private static ControllerSettingsViewModel OpenControllerSettings(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static void CloseSettings(ArcadiaDriver arcadia)
    {
        for (var press = 0; press < 3 && arcadia.Main.IsSettingsOpen; press++)
        {
            arcadia.Press(UiAction.Back);
        }

        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    private static PanRowViewModel Switch(ControllerSettingsViewModel controller) =>
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

    private static ActionRowViewModel RestartRow(ControllerSettingsViewModel controller) =>
        Assert.IsType<ActionRowViewModel>(Assert.Single(controller.Rows, row => row.Key == RestartRowKey));

    private static Task WaitForRestartRowAsync(ArcadiaDriver arcadia, ControllerSettingsViewModel controller) =>
        arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == RestartRowKey), "die Zeile „Windows-Neustart ausstehend“");

    /// <summary>The App's timers for <paramref name="duration"/> of virtual time: the parts every 2 s, Pan's emulation with them.</summary>
    private static async Task RunForAsync(ArcadiaDriver arcadia, TimeSpan duration)
    {
        for (var passed = TimeSpan.Zero; passed < duration; passed += TimeSpan.FromSeconds(2))
        {
            arcadia.Time.Advance(TimeSpan.FromSeconds(2));
            await arcadia.TickControllerSupportAsync();
            await arcadia.TickSteamControllerAsync();
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-1")]
    public async Task Without_HidHide_only_HidHide_fehlt_shows_and_Pan_is_never_asked_whether_it_can_hide()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == HidHideMissingKey), "die Meldung „HidHide fehlt“");
        await RunForAsync(arcadia, TimeSpan.FromSeconds(30));

        var hidHide = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == HidHideMissingKey));
        Assert.Equal("Installieren", hidHide.ActionText);
        Assert.DoesNotContain(controller.Rows, row => row.Key == RestartRowKey);
        Assert.DoesNotContain(controller.Rows, row => row.Title.Contains("nicht aktiv", StringComparison.Ordinal));
        Assert.DoesNotContain(controller.Rows, row => row.Title.Contains("Neustart", StringComparison.Ordinal));
        Assert.Equal("Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter.", Switch(controller).Subtitle);
        Assert.Equal(0, arcadia.Pan.HidingChecks);

        CloseSettings(arcadia);
        await RunForAsync(arcadia, TimeSpan.FromSeconds(30));
        Assert.Equal(0, arcadia.Pan.HidingChecks);
        Assert.Empty(arcadia.Pan.Hidden);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-5")]
    [Trait("TestCase", "T382-6")]
    public async Task Installing_HidHide_sets_nothing_up_again_until_Arcadia_restarts_and_the_restarted_Arcadia_hides_with_one_puck_restart()
    {
        // The Steam Controller is emulated as an Xbox pad without its copy for Steam: that runs without HidHide.
        var steam = new FakeSteam { HidHideVersion = null };
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Gog, 3)], new AppSettings { PanEnabled = true, ActiveLaunchers = ["gog"] }, steam);
        var puckRestarts = 0;
        arcadia.Pan.PuckRestart = () =>
        {
            puckRestarts++;
            return PanReason.None;
        };
        arcadia.Sdl.Plug(KnownPads.SteamController);
        await arcadia.TickControllerSupportAsync();
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Empty(arcadia.Pan.Hidden);
        var started = arcadia.Pan.Started.Count;

        var controller = OpenControllerSettings(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == HidHideMissingKey), "die Meldung „HidHide fehlt“");
        controller.Rows.Single(row => row.Key == HidHideMissingKey).Activate();
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);
        steam.Pan.Running = new TaskCompletionSource();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1, "HidHides Installer läuft");
        await RunForAsync(arcadia, TimeSpan.FromSeconds(6));
        steam.Pan.Running.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.ControllerSupport.BusyText is null, "HidHide installiert");
        await arcadia.WaitUntilAsync(() => arcadia.Restarts.Count == 1, "der Neustart von Arcadia");

        // Nothing is set up again during the run or before Arcadia's restart. Arcadia itself holds the emulation while
        // HidHide's installer runs (#396: this process couldn't see what Pan hides afterwards), so it ends once; Pan's
        // service is kept (UnchangedPan, T382-3).
        await RunForAsync(arcadia, TimeSpan.FromSeconds(6));
        Assert.Equal(started, arcadia.Pan.Started.Count);
        Assert.Equal(ControllerSupportService.RestartPendingText, arcadia.ForGames.Status.Reason);
        Assert.Equal(0, puckRestarts);

        // The restarted Arcadia hides the puck for its copy: the one puck restart Arcadia causes (D5; the other is Pan's
        // install, on the owner's PC in T382-31). Nothing else restarts it.
        using var restarted = await arcadia.RestartAsync();
        var restartedPuckRestarts = 0;
        restarted.Pan.PuckRestart = () =>
        {
            restartedPuckRestarts++;
            return PanReason.None;
        };
        await PlugSteamControllerAsync(restarted);
        Assert.Equal(PanEmulationState.Running, restarted.ForGames.Status.State);
        Assert.Equal([1u], restarted.Pan.Hidden);
        await RunForAsync(restarted, TimeSpan.FromSeconds(20));
        Assert.Equal(1, restartedPuckRestarts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-8")]
    public async Task A_usbip_win2_removal_and_Windows_own_pending_restart_show_one_restart_row_and_pause_Pan()
    {
        var steam = new FakeSteam { UsbipRemovalPending = true, UsbipRebootPending = true };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);

        var row = RestartRow(controller);
        Assert.Equal(EmulationParts.PausedSummary, row.Subtitle);
        Assert.Single(controller.Rows, row => row.Title.Contains("Neustart", StringComparison.Ordinal));
        Assert.DoesNotContain(controller.Rows, row => row.Title == "HidHide: Neustart ausstehend");
        Assert.Equal(PausedSubtitle, Switch(controller).Subtitle);
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Empty(arcadia.Pan.Started);
        Assert.False(arcadia.XboxPad.IsPlugged);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-7")]
    [Trait("TestCase", "T382-12")]
    public async Task The_paused_switch_has_a_grey_lamp_the_restart_row_sits_below_it_and_its_info_panel_reads_as_designed()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);

        var pan = Switch(controller);
        Assert.True(pan.IsOn);
        Assert.Equal(PausedSubtitle, pan.Subtitle);
        Assert.Equal(RowLamp.Resting, pan.Lamp);
        Assert.Null(controller.Marker);
        var row = RestartRow(controller);
        Assert.Equal(1, row.Indent);
        Assert.Equal(controller.Rows.IndexOf(pan) + 1, controller.Rows.IndexOf(row));
        Assert.True(controller.Rows.IndexOf(row) < controller.Rows.IndexOf(controller.Rows.Single(r => r.Key == PanRows.RestoreKey)));
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Empty(arcadia.Pan.Started);

        Assert.Equal(
            [
                "Ein Treiber wurde deinstalliert. Windows entfernt ihn ganz erst beim nächsten Neustart.",
                "Bis dahin pausiert Pan: Arcadia emuliert und versteckt keine Controller, Spiele und Steam sehen deine Controller direkt. Der Schalter bleibt an, nach dem Neustart läuft Pan von selbst weiter.",
            ],
            row.Info.OfType<InfoText>().Select(text => text.Text));
        var box = Assert.Single(row.Info.OfType<InfoSection>());
        Assert.Equal("NACH DEM NEUSTART", box.Title);
        Assert.Equal(
            "Fehlt dann noch etwas, steht es hier mit „Installieren“. Ein Druck richtet alles Fehlende in einem Durchgang ein, mit einer Windows-Abfrage.",
            Assert.IsType<InfoText>(Assert.Single(box.Blocks)).Text);
        Assert.Equal("WINDOWS-NEUSTART AUSSTEHEND", row.InfoTitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-13")]
    public async Task Switched_off_and_on_while_paused_Pan_stays_paused_and_the_restart_row_stays_with_its_button()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);

        Switch(controller).Activate();
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.Equal("Aus – Spiele und Launcher sehen deine echten Controller direkt", Switch(controller).Subtitle);
        Assert.True(RestartRow(controller).CanActivate);

        Switch(controller).Activate();
        await RunForAsync(arcadia, TimeSpan.FromSeconds(10));
        Assert.True(arcadia.Settings.Current.PanEnabled);
        Assert.Equal(PausedSubtitle, Switch(controller).Subtitle);
        Assert.True(RestartRow(controller).CanActivate);
        Assert.DoesNotContain(controller.Rows, row => row is ActionRowViewModel { ActionText: "Installieren" });
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Empty(arcadia.Pan.Started);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-14")]
    public async Task With_a_fresh_usbip_win2_a_missing_HidHide_can_still_be_installed()
    {
        var steam = new FakeSteam { UsbipRebootPending = true, HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == HidHideMissingKey), "die Meldung „HidHide fehlt“");
        Assert.Equal(EmulationParts.RecommendedSummary, RestartRow(controller).Subtitle);

        var hidHide = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == HidHideMissingKey));
        Assert.True(hidHide.CanActivate);
        hidHide.Activate();
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && arcadia.ControllerSupport.BusyText is null, "HidHide installiert");

        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
        Assert.Null(arcadia.ControllerSupport.LastError);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T382-15")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_HidHide_removal_outside_Arcadia_shows_the_controllers_again_and_pauses_Pan_without_a_warning(bool settingsOpen)
    {
        var steam = new FakeSteam();
        using var arcadia = await StartAsync(steam);
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Equal([1u], arcadia.Pan.Hidden);
        var controller = settingsOpen ? OpenControllerSettings(arcadia) : null;

        // HidHide is uninstalled in Windows' "Apps" while Arcadia runs.
        steam.HidHideRemovalPending = true;
        await RunForAsync(arcadia, TimeSpan.FromSeconds(10));

        Assert.Equal([1u], arcadia.Pan.Unhidden);
        Assert.Equal([1u], arcadia.Pan.Stopped);
        var unhide = arcadia.Pan.Events.IndexOf("unhide 1");
        Assert.True(unhide >= 0 && unhide < arcadia.Pan.Events.IndexOf("stopped 1"), string.Join(", ", arcadia.Pan.Events));
        Assert.Equal((PanEmulationState.Ready, PanEmulationProblem.WindowsRestart), (arcadia.ForGames.Status.State, arcadia.ForGames.Status.Problem));
        Assert.Empty(arcadia.ForGames.Emulation.NotVisibleToArcadia);
        Assert.DoesNotContain("sieht deine Controller nicht mehr", arcadia.Main.Toast.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.True(arcadia.Settings.Current.PanEnabled);

        controller ??= OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);
        Assert.Equal(PausedSubtitle, Switch(controller).Subtitle);
        Assert.DoesNotContain(controller.Rows, row => row.Key == "pan:not-visible");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-16")]
    [Trait("TestCase", "T382-17")]
    public async Task The_pause_holds_across_an_Arcadia_restart_and_ends_by_itself_with_the_Windows_restart()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        using var arcadia = await StartAsync(steam);
        Assert.Empty(arcadia.Pan.Hidden);

        using var restarted = await arcadia.RestartAsync();
        await PlugSteamControllerAsync(restarted);
        await RunForAsync(restarted, TimeSpan.FromSeconds(10));
        Assert.Empty(restarted.Pan.Hidden);
        Assert.Empty(restarted.Pan.Started);
        Assert.Equal(PanEmulationProblem.WindowsRestart, restarted.ForGames.Status.Problem);
        var controller = OpenControllerSettings(restarted);
        await WaitForRestartRowAsync(restarted, controller);

        // Windows restarted (here: the removal no longer pending), HidHide is gone: Pan runs again without the switch.
        steam.HidHideRemovalPending = false;
        await RunForAsync(restarted, TimeSpan.FromSeconds(4));
        await restarted.WaitUntilAsync(() => controller.Rows.All(row => row.Key != RestartRowKey), "die Zeile verschwindet");
        Assert.Equal(PanEmulationState.Running, restarted.ForGames.Status.State);
        Assert.NotEmpty(restarted.Pan.Hidden);
        Assert.NotEqual(PausedSubtitle, Switch(controller).Subtitle);
        Assert.True(restarted.Settings.Current.PanEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-20")]
    public async Task While_a_game_runs_Windows_neu_starten_is_greyed_out_keeps_its_focus_and_does_nothing_until_the_game_ended()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        using var arcadia = await StartAsync(steam);
        var game = arcadia.Main.Session.Libraries.SelectMany(library => library.Games).First();
        arcadia.RunInLauncher(game);
        await arcadia.FollowRunningGamesAsync();
        var controller = OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);

        controller.FocusRow(RestartRowKey);
        var row = RestartRow(controller);
        Assert.Equal(row, controller.FocusedRow);
        Assert.Equal($"Erst möglich, wenn „{game.Title}“ beendet ist", row.Subtitle);
        Assert.False(row.IsEnabled);
        Assert.False(row.CanActivate);
        Assert.NotEmpty(row.Info);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.WindowsRestarts);

        // The game ends; the App's timers go on as usual (running games, the parts, Pan).
        arcadia.EndInLauncher(game);
        await arcadia.FollowRunningGamesAsync();
        await RunForAsync(arcadia, TimeSpan.FromSeconds(10));
        Assert.Null(arcadia.Main.Session.RealRunStateOf(game.Key));
        await arcadia.WaitUntilAsync(() => RestartRow(controller).CanActivate, "„Windows neu starten“ wieder wählbar");
        Assert.Equal(EmulationParts.PausedSummary, RestartRow(controller).Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-21")]
    public async Task A_game_that_starts_while_the_question_is_open_closes_it_and_restarts_nothing()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await WaitForRestartRowAsync(arcadia, controller);
        controller.FocusRow(RestartRowKey);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmWindowsRestart, controller.Prompt);

        arcadia.RunInLauncher(arcadia.Main.Session.Libraries.SelectMany(library => library.Games).First());
        await arcadia.FollowRunningGamesAsync();
        await arcadia.WaitUntilAsync(() => controller.Prompt == SettingsPrompt.None, "die Frage schließt sich");

        arcadia.Press(UiAction.Accept);
        Assert.Equal(0, steam.WindowsRestarts);
        Assert.Equal(RestartRowKey, controller.FocusedRow?.Key);
        Assert.False(RestartRow(controller).CanActivate);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-26")]
    public async Task After_the_Windows_restart_nothing_installs_or_asks_by_itself()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        await RunForAsync(arcadia, TimeSpan.FromSeconds(60));

        Assert.Equal(0, steam.Pan.Prompts);
        Assert.Equal(0, steam.DriverInstalls);
        Assert.Null(arcadia.ControllerSupport.BusyText);
        Assert.Empty(arcadia.Restarts);

        var controller = OpenControllerSettings(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == HidHideMissingKey), "die Meldung „HidHide fehlt“");
        await RunForAsync(arcadia, TimeSpan.FromSeconds(60));
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.Pan.Prompts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-27")]
    public async Task One_Installieren_sets_up_every_missing_part_with_one_prompt_one_Steam_stop_and_one_Arcadia_restart()
    {
        var steam = new FakeSteam { HidHideVersion = null, IsUsbipReady = false, IsSteamRunning = true };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:usbip-missing"), "die Meldung „usbip-Treiber fehlt“");

        controller.Rows.Single(row => row.Key == "issue:usbip-missing").Activate();
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && arcadia.ControllerSupport.BusyText is null, "eingerichtet");
        await arcadia.WaitUntilAsync(() => arcadia.Restarts.Count == 1, "der Neustart von Arcadia");

        Assert.Equal(1, steam.Pan.Prompts);
        Assert.True(steam.Pan.LastPlan!.Usbip && steam.Pan.LastPlan.HidHide, "usbip-win2 und HidHide in einem Durchgang");
        Assert.Equal(1, steam.SteamStops);
        Assert.True(steam.IsUsbipReady);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);

        // The rows follow the parts' recheck after the run, which lands on the UI thread a moment later (on Windows' CI
        // after the restart above was already counted).
        await arcadia.WaitUntilAsync(() => !controller.Rows.Any(row => row is ActionRowViewModel { ActionText: "Installieren" }), "keine Zeile „Installieren“ mehr");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-28")]
    public async Task A_failing_pending_restart_check_shows_no_restart_row_logs_it_and_Pan_runs()
    {
        var steam = new FakeSteam { PendingRestartError = () => new IOException("Registrierung nicht lesbar (Test 382-28)") };
        using var arcadia = await StartAsync(steam);
        var controller = OpenControllerSettings(arcadia);
        await RunForAsync(arcadia, TimeSpan.FromSeconds(4));

        Assert.DoesNotContain(controller.Rows, row => row.Key == RestartRowKey);
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        var log = ArcadiaDriver.Log;
        var line = log.IndexOf("Nicht geprüft, ob ein Windows-Neustart aussteht: System.IO.IOException: Registrierung nicht lesbar (Test 382-28)", StringComparison.Ordinal);
        Assert.True(line >= 0, "die Zeile in arcadia.log");
        // A stack trace follows; which frames it shows depends on the JIT (a Release build may inline the fake's check).
        Assert.Contains($"(Test 382-28){Environment.NewLine}   at ", log[line..], StringComparison.Ordinal);
    }
}
