using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// Pan's emulation of the Steam Controller as the user sees it (#242): the switch "Controller-Wächter (Pan)" with its
/// states, the rows below it that say why it doesn't run, the greyed mode, the header, the notices when the hiding goes
/// and comes back, and the dialog when Steam tries a firmware update. Pan, VIIPER and the copy are faked; the real puck,
/// HidHide and Steam are the manual part (docs/testplans/iteration-2/242-steam-controller-copy.md).
/// </summary>
public sealed class PanEmulationTests
{
    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    /// <param name="copy">This build has the copy for Steam, so Pan hides the puck (#242 parts B and C).</param>
    private static async Task<ArcadiaDriver> StartAsync(bool copy = false, FakeSteam? steam = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam ?? new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = copy;
        arcadia.SteamControllerCopy.PuckPresent = copy;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    private static ControllerSettingsViewModel OpenControllerSettings(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static PanRowViewModel Switch(ControllerSettingsViewModel controller) =>
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

    /// <summary>The status rows below the switch; "Controller zuordnen" (#238) is none, its tests are in <c>EveryControllerForGamesTests</c>.</summary>
    private static IReadOnlyList<string> RowsBelow(ControllerSettingsViewModel controller) =>
        [.. controller.Rows.Where(row => row.Group == PanRows.Group && row.Indent == 1 && row.Key != PanRows.LayoutsKey).Select(row => row.Key)];

    [AvaloniaFact]
    [Trait("TestCase", "T242-11")]
    public async Task The_switch_says_Pan_runs_and_switched_off_it_hides_the_mode_and_keeps_Controller_wiederherstellen()
    {
        using var arcadia = await StartAsync();
        Assert.True(arcadia.Main.ShowControllerSupport);
        var controller = OpenControllerSettings(arcadia);

        var pan = Switch(controller);
        Assert.Equal("Controller-Wächter (Pan)", pan.Title);
        Assert.Equal("PAN", pan.Group);
        Assert.Equal("An – deine Controller werden von Arcadia verwaltet", pan.Subtitle);
        Assert.True(pan.IsLampWorking);
        Assert.Equal("An", pan.ValueText);
        Assert.Equal("Ändern", pan.ActivateText);
        Assert.Equal("PAN, DER CONTROLLER-WÄCHTER", pan.InfoTitle);
        Assert.Contains(pan.Info.OfType<InfoSection>(), section => section.Title == "WAS PAN BRAUCHT");
        Assert.Empty(RowsBelow(controller));
        Assert.Contains(controller.Rows, row => row.Key == PanRows.RestoreKey);

        pan.Activate();
        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.Equal("Aus – Spiele und Launcher sehen deine echten Controller direkt", Switch(controller).Subtitle);
        Assert.Contains(controller.Rows, row => row.Key == PanRows.RestoreKey);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Off, arcadia.ForGames.Status.State);
        Assert.False(arcadia.XboxPad.IsPlugged);
        Assert.Equal([1u], arcadia.Pan.Stopped);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.ShowControllerSupport, "kein Modus, solange Pan aus ist");
        Assert.False(arcadia.Main.Status.HasControllerStatus);

        // On again: the emulation comes back at the next tick.
        OpenControllerSettings(arcadia);
        Switch(Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected)).Activate();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.True(arcadia.XboxPad.IsPlugged);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-12")]
    public async Task While_a_game_starts_or_runs_the_switch_waits_for_its_end()
    {
        using var arcadia = await StartAsync();
        arcadia.Press(UiAction.NextTab);
        var title = arcadia.Main.Library.FocusedGame!.Title;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");

        var controller = OpenControllerSettings(arcadia);
        var pan = Switch(controller);
        Assert.Equal($"Erst änderbar, wenn „{title}“ beendet ist", pan.Subtitle);
        Assert.True(pan.IsLocked);
        Assert.True(pan.IsDimmed);
        Assert.False(pan.CanActivate);
        pan.Activate();
        Assert.True(arcadia.Settings.Current.PanEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T307-1")]
    [Trait("TestCase", "T307-2")]
    public async Task A_demo_game_shown_as_running_does_not_lock_the_switch_but_a_real_one_does()
    {
        // The demo library comes first and its game is on "Steam-Eingabe", so neither the lock text nor the routing
        // can pick it up by accident.
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new(KnownLaunchers.Steam, 3, IsDemo: true), new(KnownLaunchers.Gog, 3)],
            new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        await arcadia.FollowRunningGamesAsync();
        Assert.Contains(arcadia.PageGames, tile => tile.IsRunning);
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged, "das Demo-Spiel auf Steam-Eingabe läuft nicht wirklich");

        var controller = OpenControllerSettings(arcadia);
        var pan = Switch(controller);
        Assert.False(pan.IsLocked, "das Demo-Spiel läuft nicht wirklich");
        Assert.True(pan.CanActivate);
        pan.Activate();
        Assert.False(arcadia.Settings.Current.PanEnabled);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Off, arcadia.ForGames.Status.State);
        Switch(controller).Activate();
        Assert.True(arcadia.Settings.Current.PanEnabled);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);

        // A game really started from Arcadia still locks it, with its own title.
        arcadia.Press(UiAction.Back).Press(UiAction.NextTab);
        var title = arcadia.Main.Library.FocusedGame!.Title;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        pan = Switch(OpenControllerSettings(arcadia));
        Assert.True(pan.IsLocked);
        Assert.Equal($"Erst änderbar, wenn „{title}“ beendet ist", pan.Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-13")]
    public async Task A_hide_that_fails_pauses_the_emulation_with_its_row_and_Erneut_versuchen_tries_again()
    {
        using var arcadia = await StartAsync(copy: true);
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        var controller = OpenControllerSettings(arcadia);

        // Pan loses the hiding while it runs: the puck is shown, the notice says so, and the row says why.
        arcadia.Pan.HidingHolds = PanReason.HidingLost;
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Paused, arcadia.ForGames.Status.State);
        Assert.Equal(PanEmulation.PausedText, arcadia.Main.Toast.Text);
        Assert.Equal(["pan:hide-failed"], RowsBelow(controller));
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:hide-failed"));
        Assert.Equal("Steam Controller lässt sich nicht verstecken", row.Title);
        Assert.Equal("Erneut versuchen", row.ActionText);
        Assert.Equal("Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter.", Switch(controller).Subtitle);
        Assert.True(Switch(controller).IsLampProblem);
        Assert.Equal(HealthSeverity.Warning, controller.Marker);
        Assert.True(arcadia.Main.Status.HasControllerStatus);
        Assert.Contains(StatusBarViewModel.PanNotRunningText, arcadia.Main.Status.ControllerStatusText);

        // HidHide's configuration is open now: the same row as HidHide's own check.
        arcadia.Pan.HidingHolds = PanReason.None;
        arcadia.Pan.HideResult = PanReason.HidHideBusy;
        row.Activate();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(["issue:hidhide-busy"], RowsBelow(controller));

        arcadia.Pan.HideResult = PanReason.None;
        arcadia.Time.Advance(PanEmulation.HideRetry);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Empty(RowsBelow(controller));
        Assert.Equal(PanEmulation.RunningAgainText, arcadia.Main.Toast.Text);
        Assert.False(arcadia.Main.Status.HasControllerStatus);
    }

    /// <summary>
    /// #308: only the puck plugged in, no Steam Controller on it. Pan hid it every 22 s in vain (Arcadia's SDL held it
    /// open, so Windows refused the restart) and gave up after a minute. Now Arcadia lets go of it for the hide.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T308-2")]
    public async Task With_only_the_puck_Pan_hides_it_once_and_the_rows_stay_quiet_for_ten_minutes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        var letGo = arcadia.Sdl.Calls.IndexOf("Steam aus");
        Assert.True(letGo >= 0, "Arcadia didn't let go of the puck for the hide");
        Assert.Equal(["Steam aus", "Update", "Steam an"], arcadia.Sdl.Calls.Skip(letGo).Take(3));
        var controller = OpenControllerSettings(arcadia);

        for (var tick = 0; tick < 300; tick++)
        {
            arcadia.Time.Advance(TimeSpan.FromSeconds(2));
            await arcadia.TickSteamControllerAsync();
        }

        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Equal([1u], arcadia.Pan.Hidden);
        Assert.Empty(RowsBelow(controller));
        Assert.False(Switch(controller).IsLampProblem);
        Assert.False(arcadia.XboxPad.IsPlugged);
        Assert.NotEqual(PanEmulation.StoppedText, arcadia.Main.Toast.Text);
    }

    /// <summary>
    /// #308 as Windows answers it: the hide's puck restart is refused while Arcadia's SDL holds the puck open (event 225).
    /// Without the let-go every hide fails, the warning comes back each retry and the 4th failure stops the emulation.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T308-2")]
    public async Task With_only_the_puck_held_open_by_SDL_Pan_still_hides_it_and_nothing_warns_for_ten_minutes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Pan.PuckRestart = () => arcadia.Sdl.HoldsPuck ? PanReason.HidingFailed : PanReason.None;
        arcadia.StartControllers();
        var controller = OpenControllerSettings(arcadia);
        var rows = new HashSet<string>();
        var toasts = new HashSet<string>();

        for (var tick = 0; tick < 300; tick++)
        {
            await arcadia.TickSteamControllerAsync();
            rows.UnionWith(RowsBelow(controller));
            if (arcadia.Main.Toast.Text is { } toast)
            {
                toasts.Add(toast);
            }

            arcadia.Time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Equal([1u], arcadia.Pan.Started);
        Assert.Equal([1u], arcadia.Pan.Hidden);
        Assert.Empty(rows);
        Assert.DoesNotContain(PanEmulation.StoppedText, toasts);
        Assert.DoesNotContain(PanEmulation.PausedText, toasts);
        Assert.True(arcadia.Sdl.HoldsPuck, "Arcadia didn't read the puck again after the hide");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T308-3")]
    public async Task A_hide_that_keeps_failing_keeps_its_warning_row_through_every_retry()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Pan.HideResult = PanReason.HidingFailed;
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        var controller = OpenControllerSettings(arcadia);
        var states = new List<PanEmulationState>();
        arcadia.ForGames.Changed += () => states.Add(arcadia.ForGames.Status.State);

        for (var tick = 0; tick < 12; tick++)
        {
            arcadia.Time.Advance(TimeSpan.FromSeconds(2));
            await arcadia.TickSteamControllerAsync();
            Assert.Equal(["pan:hide-failed"], RowsBelow(controller));
        }

        Assert.True(arcadia.Pan.Started.Count >= 3, $"{arcadia.Pan.Started.Count} attempts");
        Assert.All(states, state => Assert.Equal(PanEmulationState.Paused, state));
    }

    /// <summary>A retry the player asked for shows that it runs (a hide takes ~11 s on hardware) and how it ended (owner, 2026-10-29).</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T308-3")]
    public async Task Erneut_versuchen_shows_on_its_row_that_it_runs_and_a_toast_says_when_it_failed()
    {
        using var arcadia = await HideFailsAsync();
        var controller = OpenControllerSettings(arcadia);
        var states = new List<PanEmulationState>();
        arcadia.ForGames.Changed += () => states.Add(arcadia.ForGames.Status.State);
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:hide-failed"));
        Assert.Equal(("Erneut versuchen", false), (row.ActionText, row.IsWorking));

        row.Activate();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(("Wird versucht …", true, false), (row.ActionText, row.IsWorking, row.CanActivate));

        // Mid-attempt the emulation sets up anew, which has no warning of its own: the pressed row stays and shows it.
        (IReadOnlyList<string> Rows, bool Working)? during = null;
        arcadia.Pan.PuckRestart = () =>
        {
            Dispatcher.UIThread.RunJobs();
            during = (RowsBelow(controller), row.IsWorking);
            return PanReason.HidingFailed;
        };
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(PanEmulationState.SettingUp, states.Distinct().Skip(1).First());
        Assert.Equal(["pan:hide-failed"], during!.Value.Rows);
        Assert.True(during.Value.Working);
        Assert.Equal(["pan:hide-failed"], RowsBelow(controller));
        Assert.Equal(("Erneut versuchen", false, true), (row.ActionText, row.IsWorking, row.CanActivate));
        Assert.Equal(PanEmulation.RetryFailedText, arcadia.Main.Toast.Text);
        Assert.False(arcadia.ForGames.Retrying);

        // The automatic retry after it keeps the warning, without a toast (the last one is gone after 4 s).
        states.Clear();
        arcadia.Time.Advance(PanEmulation.HideRetry);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(3, arcadia.Pan.Started.Count);
        Assert.Empty(states);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T308-3")]
    public async Task Erneut_versuchen_that_works_ends_with_a_toast_and_the_warning_gone()
    {
        using var arcadia = await HideFailsAsync();
        var controller = OpenControllerSettings(arcadia);
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:hide-failed"));

        row.Activate();
        arcadia.Pan.HideResult = PanReason.None;
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Empty(RowsBelow(controller));
        Assert.Equal(PanEmulation.RunningAgainText, arcadia.Main.Toast.Text);
        Assert.False(arcadia.ForGames.Retrying);
    }

    /// <summary>Only the puck, and every hide fails: the row "Steam Controller lässt sich nicht verstecken" is up.</summary>
    private static async Task<ArcadiaDriver> HideFailsAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Pan.HideResult = PanReason.HidingFailed;
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-14")]
    public async Task A_copy_that_keeps_failing_stops_the_emulation_until_Erneut_versuchen()
    {
        using var arcadia = await StartAsync(copy: true);
        var controller = OpenControllerSettings(arcadia);

        // The copy goes and doesn't come back: each tick counts, the fourth failure in 5 minutes stops it.
        arcadia.SteamControllerCopy.FailAttach = "VIIPER antwortet nicht";
        arcadia.SteamControllerCopy.Current!.Lose();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(["pan:start-failed"], RowsBelow(controller));
        Assert.Equal("Emulation startet nicht", controller.Rows.Single(r => r.Key == "pan:start-failed").Title);
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(PanEmulationState.Stopped, arcadia.ForGames.Status.State);
        var stopped = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:stopped"));
        Assert.Equal("Emulation angehalten", stopped.Title);
        Assert.True(stopped.IsError);
        Assert.Equal("Angehalten – der Grund steht darunter", Switch(controller).Subtitle);
        Assert.True(Switch(controller).IsLampFailure);
        Assert.Equal(HealthSeverity.Error, controller.Marker);
        Assert.True(arcadia.Main.Status.IsControllerStatusError);
        Assert.False(arcadia.XboxPad.IsPlugged);

        arcadia.SteamControllerCopy.FailAttach = null;
        stopped.Activate();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.DoesNotContain(controller.Rows, r => r.Key == "pan:stopped");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-15")]
    public async Task While_Pan_still_cleans_up_the_header_spins_and_a_blue_row_says_it_goes_on_by_itself()
    {
        using var arcadia = await StartAsync();
        var controller = OpenControllerSettings(arcadia);

        arcadia.Pan.Missing = PanClient.CleaningUp;
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(PanEmulationState.SettingUp, arcadia.ForGames.Status.State);
        Assert.True(arcadia.Main.Status.IsControllerStarting);
        var row = controller.Rows.Single(r => r.Key == "pan:cleaning-up");
        Assert.Equal("Pan räumt noch auf", row.Title);
        Assert.True(row.IsInfo);
        Assert.Equal("Wird eingerichtet …", Switch(controller).Subtitle);
        Assert.False(arcadia.Main.ControllerModeBroken, "kein Problem, es geht von selbst weiter");

        arcadia.Pan.Missing = null;
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.Main.Status.IsControllerStarting);
        Assert.Empty(RowsBelow(controller));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-16")]
    public async Task On_the_detail_page_the_broken_mode_is_focusable_and_A_opens_the_row_that_says_why_and_B_goes_back()
    {
        using var arcadia = await StartAsync();
        arcadia.Pan.Missing = "Pan antwortet nicht";
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.Main.ControllerModeBroken);

        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        var detail = arcadia.Detail!;
        var mode = detail.Current.GameSettingsRow.Single(action => action.Kind == GameActionKind.ControllerMode);
        Assert.True(mode.IsBroken);
        Assert.Equal(ControllerModes.Steam, mode.ButtonLabel);
        Assert.Equal(ControllerModes.PanNotRunning, mode.DisabledReason);

        // Greyed out, yet the focus goes there and A works.
        arcadia.Press(UiAction.Down);
        Assert.Equal(GameActionKind.ControllerMode, detail.Current.FocusedKind);
        arcadia.Press(UiAction.Accept);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings?.Selected);
        Assert.Equal("pan:start-failed", controller.Rows.Single(row => row.IsFocused).Key);
        Assert.Equal("Pan antwortet nicht", controller.Rows.Single(row => row.IsFocused).Subtitle);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(detail, arcadia.Main.Detail);
    }

    private static readonly TimeSpan OneSecondHold = TimeSpan.FromMilliseconds(1100);

    /// <summary>Steam's message that starts a firmware update (#295): the real device would restart into its bootloader.</summary>
    private static readonly byte[] RebootIntoBootloader = [1, 0x90, 0x00];

    [AvaloniaFact]
    [Trait("TestCase", "T295-6")]
    public async Task With_Steam_running_at_the_start_Arcadia_takes_the_Steam_Controller_over_without_asking()
    {
        using var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true });

        Assert.Single(arcadia.Pan.Hidden);
        Assert.Null(arcadia.Main.PanDialog);
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
    }

    /// <summary>Taken over, then Steam tries a firmware update through the copy while Arcadia's library is in front.</summary>
    private static async Task<ArcadiaDriver> StartUpdateAttemptAsync()
    {
        var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true });
        Assert.Single(arcadia.Pan.Hidden);
        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true));
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Hinweis „Steam will die Firmware aktualisieren“");
        return arcadia;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T295-2")]
    public async Task A_firmware_attempt_with_the_library_in_front_brings_Arcadia_to_the_front_once()
    {
        using var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true });
        var shown = 0;
        arcadia.Main.PanDialogShown += (_, _) => shown++;

        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Hinweis „Steam will die Firmware aktualisieren“");
        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true);

        // PanDialogShown brings the window back in front of Steam's (App: ForegroundGuard.BringBack).
        Assert.Equal(1, shown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T295-2")]
    public async Task A_firmware_attempt_shows_a_notice_that_acts_at_once_and_B_closes_it_keeping_the_controller()
    {
        var shown = 0;
        using var arcadia = await StartUpdateAttemptAsync();
        arcadia.Main.PanDialogShown += (_, _) => shown++;
        var notice = arcadia.Main.FirmwareAttempt!;
        Assert.Equal("Steam will die Firmware aktualisieren", notice.Title);
        Assert.True(notice.CloseButton.IsFocused);
        Assert.Equal("Schließen", notice.Legend);
        Assert.True(notice.ReleaseButton.IsEnabled && notice.QuitButton.IsEnabled && notice.CloseButton.IsEnabled, "kein Warten");

        // Steam repeats the message: still the one notice.
        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true);
        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.FirmwareAttempt);
        Assert.Empty(arcadia.Pan.Unhidden);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Equal(0, shown);

        // A later attempt shows it again.
        arcadia.Wait(FirmwareAttemptWatch.Quiet);
        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Hinweis beim nächsten Versuch");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T295-3")]
    public async Task Controller_freigeben_shows_them_to_Steam_until_Pan_is_switched_off_and_on()
    {
        using var arcadia = await StartUpdateAttemptAsync();
        var notice = arcadia.Main.FirmwareAttempt!;

        arcadia.Press(UiAction.Left, 2);
        Assert.True(notice.ReleaseButton.IsFocused);
        Assert.Equal("Halten (1 s): Auswählen", notice.Legend);
        arcadia.Hold(UiAction.Accept, OneSecondHold);
        await arcadia.WaitUntilAsync(() => notice.IsReleased, "„Steam Controller und Puck freigegeben“");
        Assert.Single(arcadia.Pan.Unhidden);
        Assert.Contains("aus- und wieder einschalten", notice.ReleasedNext);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.FirmwareAttempt);
        arcadia.Wait(PanEmulation.HideRetry);
        await arcadia.TickSteamControllerAsync();
        Assert.Single(arcadia.Pan.Hidden);
        Assert.Equal(PanEmulationProblem.Excluded, arcadia.ForGames.Status.Problem);
        Assert.False(arcadia.Main.ControllerModeBroken, "freigegeben ist kein Problem");

        var controller = OpenControllerSettings(arcadia);
        Assert.Equal(PanRows.ExcludedSubtitle, Switch(controller).Subtitle);
        Assert.Empty(RowsBelow(controller));

        // Off and on again: taken over again.
        arcadia.Settings.Update(settings => settings with { PanEnabled = false });
        await arcadia.TickSteamControllerAsync();
        arcadia.Settings.Update(settings => settings with { PanEnabled = true });
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(2, arcadia.Pan.Hidden.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T295-2")]
    public async Task Arcadia_beenden_in_the_notice_quits_at_once()
    {
        using var arcadia = await StartUpdateAttemptAsync();
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;

        arcadia.Press(UiAction.Left).Hold(UiAction.Accept, OneSecondHold);

        Assert.Equal(1, quit);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T295-5")]
    public async Task While_a_game_is_in_front_a_notice_over_the_game_says_it_and_Arcadia_stays_behind()
    {
        using var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true });
        arcadia.GameInFront = "steam:620";
        arcadia.IsArcadiaInFront = false;
        var shown = 0;
        arcadia.Main.PanDialogShown += (_, _) => shown++;

        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, RebootIntoBootloader, forwarding: true);

        await arcadia.WaitUntilAsync(() => arcadia.OverlayNotices.Count == 1, "der Hinweis über dem Spiel");
        Assert.Equal(SteamControllerForGames.FirmwareInGameText, arcadia.OverlayNotices[0]);
        Assert.Null(arcadia.Main.FirmwareAttempt);
        Assert.Equal(0, shown);
    }
}
