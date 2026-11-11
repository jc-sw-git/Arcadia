using Arcadia.App.Settings;
using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The progress dialog of a driver install (#413) through the view models: it opens with the Windows prompt, can't be
/// left or cancelled while it runs, closes by itself after a success and stays with the error after a failure, and the
/// focus returns to where the install started (D10).
/// </summary>
public sealed class DriverInstallDialogTests
{
    private const string PanMissing = "issue:pan-missing";

    private static async Task<(ArcadiaDriver Arcadia, ControllerSettingsViewModel Controller)> OpenControllerSettingsAsync(FakeSteam steam)
    {
        var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return (arcadia, Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected));
    }

    private static async Task InstallPanFromItsRowAsync(ArcadiaDriver arcadia, ControllerSettingsViewModel controller)
    {
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == PanMissing), "die Meldung „Wächterdienst Pan fehlt“");
        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == PanMissing)).Activate();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-1")]
    [Trait("TestCase", "T413-19")]
    [Trait("TestCase", "T413-20")]
    public async Task The_dialog_runs_without_taking_input_and_closes_by_itself_with_the_focus_where_the_install_started()
    {
        var steam = new FakeSteam { IsPanReady = false };
        steam.Pan.Running = new();
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        var startRow = controller.FocusedRow?.Key;

        await InstallPanFromItsRowAsync(arcadia, controller);
        await arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1 && controller.InstallDialog is { IsRunning: true }, "der laufende Dialog");

        var dialog = controller.InstallDialog!;
        Assert.Equal(DriverInstallProgress.RunningTitle, dialog.Title);
        Assert.Equal(ControllerSupportTexts.StepConfirmPrompt, dialog.Steps[0].Title);
        Assert.True(dialog.Steps[0].IsDone);
        Assert.StartsWith("Schritt ", dialog.StepCount, StringComparison.Ordinal);
        Assert.Equal(DriverInstallDialogViewModel.NoCancelText + " " + DriverInstallDialogViewModel.ClosesByItselfText, dialog.Footer);

        // The footer shows no "B Zurück" (D2); nothing leaves or cancels a running install: not B, not A, not a move.
        Assert.False(controller.ShowsPageHints);
        arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Up);
        Assert.Same(dialog, controller.InstallDialog);
        Assert.NotNull(arcadia.Main.Settings);
        Assert.False(steam.Pan.Aborted);

        steam.Pan.Running.SetResult();
        await arcadia.WaitUntilAsync(() => controller.InstallDialog is null, "der Dialog schließt sich selbst");

        Assert.Null(arcadia.ControllerSupport.LastError);
        Assert.True(controller.ShowsPageHints);
        Assert.DoesNotContain(controller.Rows, row => row.Key == PanMissing);
        Assert.Equal(controller.Rows.Any(row => row.Key == startRow) ? startRow : controller.Rows[0].Key, controller.FocusedRow?.Key);
        Assert.Empty(arcadia.Restarts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-15")]
    [Trait("TestCase", "T413-20")]
    public async Task A_failed_install_keeps_the_dialog_with_its_error_until_B_closes_it()
    {
        var steam = new FakeSteam { IsPanReady = false };
        steam.Pan.ExitCode = 3;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        var startRow = controller.FocusedRow?.Key;

        await InstallPanFromItsRowAsync(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.InstallDialog is { IsFailed: true }, "der Fehler im Dialog");

        var dialog = controller.InstallDialog!;
        Assert.Equal(DriverInstallProgress.FailedTitle, dialog.Title);
        Assert.False(string.IsNullOrEmpty(dialog.Error));
        Assert.Contains(dialog.Steps, step => step.IsFailed);
        Assert.Equal(dialog.Error, arcadia.ControllerSupport.LastError);

        arcadia.Press(UiAction.Back);

        Assert.Null(controller.InstallDialog);
        Assert.NotNull(arcadia.Main.Settings);
        Assert.Equal(controller.Rows.Any(row => row.Key == startRow) ? startRow : controller.Rows[0].Key, controller.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-3")]
    public async Task With_HidHide_installed_the_dialog_stays_on_Arcadia_s_restart()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == PanRows.MainKey && ((PanRowViewModel)row).IsInstall), "„Treiber installieren“");

        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Press(UiAction.Accept);
        // The restart is asked for as the install ends; the step showing it starts right after.
        await arcadia.WaitUntilAsync(() => arcadia.Restarts.Count == 1 && controller.InstallDialog?.Steps[^1].IsRunning == true, "der Neustart");

        var dialog = controller.InstallDialog!;
        Assert.NotNull(dialog);
        Assert.False(dialog.IsFailed);
        Assert.True(dialog.ShowsUsbHint);
        Assert.Equal(ControllerSupportTexts.StepArcadiaRestart, dialog.Steps[^1].Title);
        Assert.True(dialog.Steps[^1].IsRunning);
        Assert.All(dialog.Steps.SkipLast(1), step => Assert.True(step.IsDone, step.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-22")]
    public async Task The_question_before_an_install_with_usbip_win2_says_input_may_pause_briefly()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == PanRows.MainKey && ((PanRowViewModel)row).IsInstall), "„Treiber installieren“");

        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();

        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.True(controller.ShowsUsbLine);
        Assert.Equal(AppTexts.DriversUsbLine, ControllerSettingsViewModel.UsbLineText);
        arcadia.Press(UiAction.Back);
        Assert.False(controller.ShowsUsbLine);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-22")]
    public async Task The_question_before_HidHide_alone_has_no_USB_line()
    {
        var steam = new FakeSteam { IsPanReady = false, HidHideVersion = null };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await InstallPanFromItsRowAsync(arcadia, controller);

        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);
        Assert.False(controller.ShowsUsbLine);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-24")]
    public async Task While_Pan_works_for_minutes_input_is_handled_and_Pan_s_report_moves_the_dialog()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        steam.Pan.Running = new();
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row is PanRowViewModel { IsInstall: true }), "„Treiber installieren“");
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1 && controller.InstallDialog is not null, "die Übergabe");

        // Minutes of usbip-win2: the UI thread keeps taking input, and Pan's report is read while it runs.
        arcadia.Wait(TimeSpan.FromMinutes(3)).Press(UiAction.Down).Press(UiAction.Back);
        steam.Pan.Report(Arcadia.Pan.PanInstallStep.Usbip, Arcadia.Pan.PanStepState.Done);
        steam.Pan.Report(Arcadia.Pan.PanInstallStep.HidHide, Arcadia.Pan.PanStepState.Running);
        await arcadia.WaitUntilAsync(() =>
        {
            arcadia.Wait(TimeSpan.FromSeconds(1));
            return controller.InstallDialog!.Steps.Single(step => step.Title == "HidHide installieren").IsRunning;
        }, "HidHide läuft laut Bericht");

        Assert.True(controller.InstallDialog!.Steps.Single(step => step.Title == ControllerSupportTexts.StepUsbipInstall).IsDone);
        Assert.True(controller.InstallDialog.IsRunning);
        steam.Pan.Running.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Restarts.Count == 1, "der Neustart");
    }
}
