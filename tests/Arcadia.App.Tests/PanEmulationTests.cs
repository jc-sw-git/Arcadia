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

    [AvaloniaFact]
    [Trait("TestCase", "T242-8")]
    public async Task A_firmware_update_through_the_copy_is_dropped_and_a_dialog_says_how_the_update_works()
    {
        using var arcadia = await StartAsync(copy: true);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;
        arcadia.Press(UiAction.Settings);

        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB7, 0x00], forwarding: true));
        await arcadia.WaitUntilAsync(() => arcadia.Main.Confirmation is not null, "der Dialog „Firmware-Update gestoppt“");

        var dialog = arcadia.Main.Confirmation!;
        Assert.False(arcadia.Main.IsSettingsOpen, "der Dialog steht nicht unter den Einstellungen");
        Assert.Equal("Firmware-Update gestoppt", dialog.Title);
        Assert.Contains("Dein Steam Controller ist unverändert.", dialog.Message);
        Assert.Equal("SO KLAPPT DAS UPDATE", dialog.StepsTitle);
        Assert.Equal(["Arcadia beenden", "Das Update in Steam erneut starten und abwarten", "Arcadia wieder starten"], dialog.Steps);
        Assert.True(dialog.IsCancelFocused, "„Verstanden“ hat den Fokus");
        Assert.Equal("Verstanden", dialog.CancelLabel);
        Assert.Equal("Arcadia jetzt beenden", dialog.ConfirmLabel);

        // Once per run: the next firmware message is dropped without a dialog.
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Confirmation);
        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x00], forwarding: true));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(arcadia.Main.Confirmation);
        Assert.Equal(0, quit);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-8")]
    public async Task Over_another_dialog_the_firmware_dialog_waits_and_shows_once_that_one_is_closed()
    {
        using var arcadia = await StartAsync(copy: true);
        var other = new ConfirmationViewModel("Andere Frage", "Eine offene Frage", "Ja", () => { });
        arcadia.Main.Ask(other);

        Assert.False(arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0xB7, 0x00], forwarding: true));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(other, arcadia.Main.Confirmation);

        arcadia.Press(UiAction.Back);
        Assert.Equal("Firmware-Update gestoppt", arcadia.Main.Confirmation?.Title);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Confirmation);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-8")]
    public async Task Arcadia_jetzt_beenden_in_the_firmware_dialog_asks_the_App_to_close()
    {
        using var arcadia = await StartAsync(copy: true);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;
        arcadia.Main.ShowFirmwareBlocked();

        arcadia.Press(UiAction.Right).Press(UiAction.Accept);

        Assert.Equal(1, quit);
        Assert.Null(arcadia.Main.Confirmation);
    }
}
