using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

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
    private static async Task<ArcadiaDriver> StartAsync(bool copy = false, FakeSteam? steam = null, bool heldForFirmware = false)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam ?? new FakeSteam());
        arcadia.Pan.HeldForFirmware = heldForFirmware;
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

    private static IReadOnlyList<string> RowsBelow(ControllerSettingsViewModel controller) =>
        [.. controller.Rows.Where(row => row.Group == PanRows.Group && row.Indent == 1).Select(row => row.Key)];

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
        Assert.Equal("STEAM", controller.Rows.Single(row => row.Key == "steam-desktop-layout").Group);

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

    /// <summary>Steam runs at the start: it may be updating the puck right now, so Arcadia asks before it hides anything.</summary>
    private static async Task<ArcadiaDriver> StartWithSteamRunningAsync()
    {
        var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true });
        await arcadia.WaitUntilAsync(() => arcadia.Main.TakeoverConsent is not null, "die Frage „Steam Controller und Puck übernehmen?“");
        return arcadia;
    }

    private static readonly TimeSpan OneSecondHold = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan ThreeSecondHold = TimeSpan.FromMilliseconds(3100);

    [AvaloniaFact]
    [Trait("TestCase", "T242-20")]
    public async Task With_Steam_running_Arcadia_asks_first_and_hides_nothing_and_B_or_a_tap_answers_nothing()
    {
        using var arcadia = await StartWithSteamRunningAsync();

        var consent = arcadia.Main.TakeoverConsent!;
        Assert.Equal("Steam Controller und Puck übernehmen?", consent.Title);
        Assert.Equal(["Steam Controller", "Puck", "Steam läuft"], consent.Found);
        Assert.Equal("Erst prüfen: Führt Steam gerade ein Firmware-Update des Steam Controllers oder Pucks aus?", consent.WarningTitle);
        Assert.True(consent.No.IsFocused, "„Nein, nicht übernehmen“ ist vorausgewählt");
        Assert.Equal("Halten (1 s): Nicht übernehmen", consent.Legend);
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Equal(PanEmulationProblem.AwaitingConsent, arcadia.ForGames.Status.Problem);
        Assert.False(arcadia.Main.ControllerModeBroken, "die Frage ist kein Problem");

        // [B] and a tap of [A] answer nothing: the question stays until a button is held.
        arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Left).Press(UiAction.Accept);
        await arcadia.TickSteamControllerAsync();
        Assert.Same(consent, arcadia.Main.TakeoverConsent);
        Assert.Equal("Halten (3 s): Übernehmen", consent.Legend);
        Assert.Equal(TakeoverState.Asking, arcadia.ForGames.Takeover.State);
        Assert.Empty(arcadia.Pan.Hidden);

        // Let go before the 3 s are up: still nothing.
        arcadia.Hold(UiAction.Accept, TimeSpan.FromSeconds(2));
        Assert.Same(consent, arcadia.Main.TakeoverConsent);

        arcadia.Hold(UiAction.Accept, ThreeSecondHold);
        Assert.Null(arcadia.Main.TakeoverConsent);
        await arcadia.TickSteamControllerAsync();
        Assert.Single(arcadia.Pan.Hidden);
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-21")]
    public async Task After_Nein_the_Steam_Controller_is_left_alone_until_the_settings_row_is_held_for_3_seconds()
    {
        using var arcadia = await StartWithSteamRunningAsync();
        arcadia.Hold(UiAction.Accept, OneSecondHold);
        Assert.Null(arcadia.Main.TakeoverConsent);
        await arcadia.TickSteamControllerAsync();
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Equal(PanEmulationProblem.Excluded, arcadia.ForGames.Status.Problem);
        Assert.False(arcadia.Main.ControllerModeBroken, "ausgenommen ist kein Problem");
        Assert.Null(arcadia.Main.TakeoverConsent);

        var controller = OpenControllerSettings(arcadia);
        var pan = Switch(controller);
        Assert.Equal("An – Steam Controller und Puck ausgenommen, siehe unten", pan.Subtitle);
        Assert.Null(controller.Marker);
        var row = Assert.IsType<HoldRowViewModel>(controller.Rows.Single(r => r.Key == PanRows.TakeOverKey));
        Assert.Equal("Steam Controller und Puck übernehmen", row.Title);
        Assert.Equal("Beim Start abgelehnt, weil Steam lief", row.Subtitle);
        Assert.Equal("Halten (3 s): Übernehmen", row.ActivateText);
        Assert.Equal("Vorher sicherstellen, dass kein Firmware-Update läuft", row.Info.OfType<InfoDanger>().Single().Title);

        controller.FocusRow(PanRows.TakeOverKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Hold(UiAction.Accept, TimeSpan.FromSeconds(2));
        Assert.Contains(row, controller.Rows);
        Assert.Equal(TakeoverState.Declined, arcadia.ForGames.Takeover.State);

        // The press is the consent: no second question.
        arcadia.Hold(UiAction.Accept, ThreeSecondHold);
        Assert.DoesNotContain(controller.Rows, r => r.Key == PanRows.TakeOverKey);
        Assert.Equal(PanRows.MainKey, controller.Rows.Single(r => r.IsFocused).Key);
        Assert.Equal("Pan: Steam Controller und Puck übernommen", arcadia.Main.Toast.Text);
        await arcadia.TickSteamControllerAsync();
        Assert.Null(arcadia.Main.TakeoverConsent);
        Assert.Single(arcadia.Pan.Hidden);
    }

    /// <summary>Taken over without a question (Steam didn't run), then Steam tries a firmware update through the copy.</summary>
    private static async Task<ArcadiaDriver> StartUpdateAttemptAsync()
    {
        var arcadia = await StartAsync(copy: true);
        Assert.Single(arcadia.Pan.Hidden);
        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x00], forwarding: true));
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Dialog „Steam will die Firmware aktualisieren“");
        return arcadia;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-8")]
    public async Task A_firmware_attempt_holds_the_controller_until_Steam_gave_up_and_then_releases_it_for_the_update()
    {
        using var arcadia = await StartUpdateAttemptAsync();
        var dialog = arcadia.Main.FirmwareAttempt!;
        Assert.Equal("Steam will die Firmware aktualisieren", dialog.Title);
        Assert.True(dialog.IsTrying);
        Assert.True(dialog.SkipButton.IsFocused);
        Assert.False(dialog.ReleaseButton.IsEnabled, "erst wenn Steam aufgegeben hat");
        Assert.False(dialog.QuitButton.IsEnabled);
        Assert.False(dialog.CloseButton.IsEnabled);

        // [B] doesn't close it; the greyed buttons do nothing.
        arcadia.Press(UiAction.Back).Press(UiAction.Left, 3);
        Assert.True(dialog.ReleaseButton.IsFocused);
        Assert.Equal("Noch gesperrt", dialog.Legend);
        arcadia.Hold(UiAction.Accept, OneSecondHold);
        Assert.Same(dialog, arcadia.Main.FirmwareAttempt);
        Assert.False(dialog.IsReleased);

        // 5 s quiet: the countdown; a new attempt starts the wait anew.
        arcadia.Wait(TimeSpan.FromSeconds(5.3));
        Assert.True(dialog.IsCountingDown);
        Assert.InRange(dialog.SecondsLeft, 24, 25);
        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x00], forwarding: true);
        arcadia.Wait(TimeSpan.FromMilliseconds(300));
        Assert.True(dialog.IsTrying);

        arcadia.Wait(TimeSpan.FromSeconds(30.3));
        Assert.True(dialog.IsReady);
        Assert.Equal("Steam hat aufgegeben", dialog.StatusTitle);
        Assert.False(dialog.SkipButton.IsShown);
        Assert.True(dialog.CloseButton.IsFocused, "„Schließen“ hat den Fokus, sobald es geht");
        Assert.Equal("Schließen", dialog.Legend);
        Assert.Single(arcadia.Pan.Hidden);

        arcadia.Press(UiAction.Left, 2).Hold(UiAction.Accept, OneSecondHold);
        await arcadia.WaitUntilAsync(() => dialog.IsReleased, "„Steam Controller und Puck freigegeben“");
        Assert.Equal("Steam Controller und Puck freigegeben", dialog.ReleasedTitle);
        Assert.Single(arcadia.Pan.Unhidden);
        Assert.Equal(TakeoverState.Released, arcadia.ForGames.Takeover.State);
        Assert.Contains("Pan: Firmware-Versuch beendet (Steam hat aufgegeben, Controller freigegeben); als Update gewertet: ", ArcadiaDriver.Log);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.FirmwareAttempt);
        await arcadia.TickSteamControllerAsync();
        Assert.Single(arcadia.Pan.Hidden);
        Assert.Null(arcadia.Main.TakeoverConsent);
        var controller = OpenControllerSettings(arcadia);
        Assert.Equal("Für ein Firmware-Update freigegeben", controller.Rows.Single(r => r.Key == PanRows.TakeOverKey).Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-23")]
    public async Task Steam_hat_das_Update_abgebrochen_held_3_seconds_frees_the_choice_and_Arcadia_beenden_quits_and_both_are_logged()
    {
        using var arcadia = await StartUpdateAttemptAsync();
        var dialog = arcadia.Main.FirmwareAttempt!;
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;

        arcadia.Hold(UiAction.Accept, TimeSpan.FromSeconds(2));
        Assert.False(dialog.IsReady);
        arcadia.Hold(UiAction.Accept, ThreeSecondHold);

        Assert.True(dialog.IsReady, "der Spieler darf selbst freigeben");
        Assert.Equal("Du hast bestätigt, dass Steam abgebrochen hat", dialog.StatusTitle);
        Assert.True(dialog.CloseButton.IsFocused);
        Assert.Contains("Pan: Spieler sagt, Steam hat das Update abgebrochen (vor Ablauf der Wartezeit); als Update gewertet: Nachricht ", ArcadiaDriver.Log);

        arcadia.Press(UiAction.Left).Hold(UiAction.Accept, OneSecondHold);
        Assert.Equal(1, quit);
        Assert.Contains("Pan: Firmware-Versuch beendet (vom Spieler übersprungen, Arcadia beendet)", ArcadiaDriver.Log);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-23")]
    public async Task Schliessen_after_Steam_gave_up_closes_the_dialog_and_keeps_the_controller_and_a_new_attempt_shows_it_again()
    {
        using var arcadia = await StartUpdateAttemptAsync();
        var dialog = arcadia.Main.FirmwareAttempt!;
        Assert.False(dialog.CloseButton.IsEnabled, "greyed while Steam still tries");
        arcadia.Wait(TimeSpan.FromSeconds(30.3));
        Assert.True(dialog.CloseButton.IsFocused, "focused once Steam gave up");

        arcadia.Press(UiAction.Accept);

        Assert.Null(arcadia.Main.FirmwareAttempt);
        Assert.Empty(arcadia.Pan.Unhidden);
        Assert.Equal(TakeoverState.Granted, arcadia.ForGames.Takeover.State);
        Assert.Contains("Pan: Firmware-Versuch beendet (Steam hat aufgegeben, geschlossen, Controller bleiben übernommen)", ArcadiaDriver.Log);

        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x00], forwarding: true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Dialog beim nächsten Versuch");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-24")]
    public async Task After_a_crash_mid_attempt_the_next_start_takes_over_without_asking_and_waits_on_the_new_copy()
    {
        using var arcadia = await StartAsync(copy: true, new FakeSteam { IsSteamRunning = true }, heldForFirmware: true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Dialog „Steam will die Firmware aktualisieren“");
        var dialog = arcadia.Main.FirmwareAttempt!;
        Assert.Null(arcadia.Main.TakeoverConsent);
        Assert.Single(arcadia.Pan.Hidden);
        Assert.NotNull(arcadia.SteamControllerCopy.Current); // the copy is back, so Steam has a device to retry on
        Assert.True(dialog.IsTrying);
        Assert.False(dialog.ReleaseButton.IsEnabled);

        // Steam retries on the new copy: the wait starts anew.
        arcadia.Wait(TimeSpan.FromSeconds(20));
        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x00], forwarding: true));
        arcadia.Wait(TimeSpan.FromSeconds(20));
        Assert.False(dialog.ReleaseButton.IsEnabled, "der neue Versuch hat die Wartezeit neu gestartet");
        arcadia.Wait(TimeSpan.FromSeconds(10.3));
        Assert.True(dialog.ReleaseButton.IsEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-25")]
    public async Task A_short_gap_mid_game_brings_neither_the_question_nor_Arcadia_to_the_front_and_after_a_hang_it_asks()
    {
        var steam = new FakeSteam();
        using var arcadia = await StartAsync(copy: true, steam);
        Assert.Single(arcadia.Pan.Hidden);
        steam.IsSteamRunning = true;
        arcadia.GameInFront = "steam:620";
        var shown = 0;
        arcadia.Main.PanDialogShown += (_, _) => shown++;

        async Task TickAsync(double seconds)
        {
            arcadia.Wait(TimeSpan.FromSeconds(seconds));
            await arcadia.TickSteamControllerAsync();
        }

        await TickAsync(2);
        await TickAsync(2);
        arcadia.Pan.HidingHolds = PanReason.HidingLost;
        await TickAsync(2);
        arcadia.Pan.HidingHolds = PanReason.None;
        await TickAsync(2);
        Assert.Null(arcadia.Main.TakeoverConsent);
        Assert.Equal(0, shown);
        Assert.Equal(2, arcadia.Pan.Hidden.Count);

        // A hang: no check for 8 s, Pan tore the emulation down meanwhile.
        arcadia.Wait(TimeSpan.FromSeconds(8));
        arcadia.Pan.TearDown();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.TakeoverConsent is not null, "die Frage nach dem Hänger");
        Assert.Equal(1, shown);
    }
}
