using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// An installed Pan that isn't this Arcadia's (#297): with Pan on, Arcadia announces the update and runs it only on [A]
/// (#326); declined or failed, Pan goes off and Arcadia waits until the old Pan has ended, with the restore and the steps
/// by hand if it doesn't. Pan's <c>--install</c>, <c>--restore</c>, the service and the UAC prompt are faked
/// (<see cref="FakeSteam"/>); the real exe, service and prompt are the manual part
/// (docs/testplans/iteration-2/326-announce-the-pan-update-and-let-the-player-decline-it.md).
/// </summary>
public sealed class PanUpdateAtStartTests
{
    private static Task<ArcadiaDriver> StartAsync(FakeSteam steam, AppSettings? settings = null) =>
        ArcadiaDriver.StartAsync(settings: settings, steam: steam);

    private static AppSettings Chosen(bool panEnabled = true) => new()
    {
        ActiveLaunchers = ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id).ToList(),
        PanEnabled = panEnabled,
    };

    private static FakeSteam OldPan() => new() { PanCompatibility = PanCompatibility.UpdateRequired };

    /// <summary>Lets the clock run while the page counts down, until <paramref name="until"/> holds.</summary>
    private static async Task RunClockUntilAsync(ArcadiaDriver arcadia, Func<bool> until, string what)
    {
        for (var second = 0; second < 120; second++)
        {
            if (until())
            {
                return;
            }

            arcadia.Wait(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Fail($"Nach 120 s auf der Uhr noch nicht eingetreten: {what}");
    }

    /// <summary>[B] on the notice, the old Pan keeps running: the countdown runs out into stage 1.</summary>
    private static async Task<PanUpdatePageViewModel> StuckAsync(ArcadiaDriver arcadia)
    {
        await arcadia.PanAnsweredAtStartAsync();
        var page = arcadia.Main.PanUpdate!;
        arcadia.Press(UiAction.Back);
        Assert.Equal(PanUpdateState.Stopping, page.State);
        await RunClockUntilAsync(arcadia, () => page.State == PanUpdateState.Stuck, "Stufe 1 „Controller freigeben“");
        return page;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-1")]
    public async Task The_notice_comes_first_and_only_A_starts_the_update_then_the_library_shows()
    {
        var steam = OldPan();
        using var arcadia = await StartAsync(steam);

        var page = arcadia.Main.PanUpdate;
        Assert.NotNull(page);
        Assert.Equal(PanUpdatePageViewModel.NoticeTitle, page.Title);
        Assert.Equal(PanUpdatePageViewModel.NoticeHint, page.Hint);
        Assert.Equal("Aktualisieren", page.ConfirmText);
        Assert.Equal("Ohne Pan weiter", page.CancelText);

        // Pan answered: no prompt opens by itself (#326).
        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.Search).Press(UiAction.Right);
        Assert.Equal(0, steam.PanUpdates);
        Assert.Equal(PanUpdateState.Notice, page.State);
        Assert.False(arcadia.Main.IsSettingsOpen);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");

        Assert.Equal(1, steam.PanUpdates);
        Assert.Equal(PanUpdatePageViewModel.UpdatedToast, arcadia.Main.Toast.Text);
        Assert.False(arcadia.ControllerSupport.IsPanUpdateDue);
        Assert.True(arcadia.Settings.Current.PanEnabled);
        arcadia.Press(UiAction.Settings);
        Assert.True(arcadia.Main.IsSettingsOpen, "die Bibliothek nimmt Eingaben wieder an");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-2")]
    [Trait("TestCase", "T340-1")]
    public async Task B_on_the_notice_switches_Pan_off_waits_for_the_old_Pan_and_the_next_start_shows_no_notice()
    {
        var steam = OldPan();
        steam.IsPanRunning = true;
        AppSettings after;
        using (var arcadia = await StartAsync(steam))
        {
            await arcadia.PanAnsweredAtStartAsync();
            var page = arcadia.Main.PanUpdate!;

            arcadia.Press(UiAction.Back);

            Assert.Equal(PanUpdateState.Stopping, page.State);
            Assert.Equal(PanUpdatePageViewModel.StoppingTitle, page.Title);
            Assert.Equal(PanUpdatePageViewModel.StoppingText, page.Text);
            Assert.True(page.IsCountingDown, "the ring with \"noch höchstens\" above the number");
            Assert.Equal(45, page.SecondsLeft);
            Assert.False(arcadia.Settings.Current.PanEnabled);
            Assert.False(arcadia.ControllerSupport.MayTalkToPan, "Arcadia leaves the old Pan alone");
            Assert.Equal(0, steam.PanUpdates);
            // The countdown takes no press: no library, no quit.
            var quit = 0;
            arcadia.Main.QuitRequested += (_, _) => quit++;
            arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Settings);
            Assert.Equal(0, quit);
            Assert.False(arcadia.Main.IsSettingsOpen);

            await RunClockUntilAsync(arcadia, () => page.SecondsLeft <= 40, "der Countdown läuft");
            steam.IsPanRunning = false;
            await RunClockUntilAsync(arcadia, () => arcadia.Main.PanUpdate is null, "die Bibliothek, sobald sich das alte Pan beendet hat");

            Assert.Equal(PanUpdatePageViewModel.DeclinedToast, arcadia.Main.Toast.Text);
            Assert.Equal(0, steam.PanUpdates);
            after = arcadia.Settings.Current;
        }

        using var next = await StartAsync(steam, after);
        Assert.Null(next.Main.PanUpdate);
        Assert.True(next.ControllerSupport.IsPanUpdateDue, "the settings still offer the update");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-3")]
    public async Task A_declined_prompt_switches_Pan_off_like_B_and_a_failed_update_says_why()
    {
        var declined = OldPan();
        declined.PromptConfirmed = false;
        using (var arcadia = await StartAsync(declined))
        {
            await arcadia.PanAnsweredAtStartAsync();
            arcadia.Press(UiAction.Accept);
            await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek ohne Pan");

            Assert.Equal(PanUpdatePageViewModel.DeclinedToast, arcadia.Main.Toast.Text);
            Assert.False(arcadia.Settings.Current.PanEnabled);
            Assert.Null(arcadia.ControllerSupport.PanUpdateError);
        }

        const string error = "Pan --install meldete Fehler 3 – Details stehen in pan.log.";
        var failed = OldPan();
        failed.PanUpdateError = error;
        using var again = await StartAsync(failed);
        await again.PanAnsweredAtStartAsync();
        again.Press(UiAction.Accept);
        await again.WaitUntilAsync(() => again.Main.PanUpdate is null, "die Bibliothek ohne Pan");

        Assert.Equal(PanUpdatePageViewModel.FailedToast, again.Main.Toast.Text);
        Assert.False(again.Settings.Current.PanEnabled);
        again.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(again.Main.Settings!.Selected);
        var pan = Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));
        Assert.Contains(pan.Info.OfType<InfoError>(), block => block.Text == error);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-4")]
    public async Task Without_Pan_or_with_Pan_off_there_is_no_notice_and_off_the_settings_offer_the_update()
    {
        var none = new FakeSteam { PanCompatibility = PanCompatibility.NotInstalled, IsPanReady = false };
        using (var arcadia = await StartAsync(none))
        {
            await arcadia.PanAnsweredAtStartAsync();
            Assert.Null(arcadia.Main.PanUpdate);
        }

        var same = new FakeSteam();
        using (var arcadia = await StartAsync(same))
        {
            await arcadia.PanAnsweredAtStartAsync();
            Assert.Null(arcadia.Main.PanUpdate);
            Assert.Equal(0, same.PanUpdates);
        }

        var off = OldPan();
        using var withOff = await StartAsync(off, Chosen(panEnabled: false));
        await withOff.PanAnsweredAtStartAsync();
        Assert.Null(withOff.Main.PanUpdate);
        Assert.Equal(0, off.PanUpdates);
        Assert.False(withOff.ControllerSupport.MayTalkToPan);

        withOff.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(withOff.Main.Settings!.Selected);
        var pan = Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));
        Assert.Equal(PanRows.UpdateDueSubtitle, pan.Subtitle);
        Assert.Equal(HealthSeverity.Info, pan.Severity);
        var first = Assert.IsType<InfoNote>(pan.Info[0]);
        Assert.Equal(PanRows.UpdateDueInfo, first.Text);
        Assert.True(first.IsInfo);
        Assert.True(first.IsBold, "the blue box is read before switching Pan on (#340)");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-5")]
    public async Task Switching_Pan_on_with_a_due_update_runs_the_update_first_and_declined_it_stays_off()
    {
        var steam = OldPan();
        steam.PromptConfirmed = false;
        using var arcadia = await StartAsync(steam, Chosen(panEnabled: false));
        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        PanRowViewModel Pan() => Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

        Pan().Activate();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == PanRows.NotUpdatedToast, "der Toast „bleibt aus“");
        Assert.Equal(1, steam.PanUpdates);
        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.Equal(PanRows.UpdateDueSubtitle, Pan().Subtitle);

        steam.PromptConfirmed = true;
        Pan().Activate();
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.PanEnabled, "Pan ist an");
        Assert.Equal(2, steam.PanUpdates);
        Assert.Equal(PanUpdatePageViewModel.UpdatedToast, arcadia.Main.Toast.Text);
        Assert.False(arcadia.ControllerSupport.IsPanUpdateDue);
        Assert.True(arcadia.ControllerSupport.MayTalkToPan);
        Assert.NotEqual(PanRows.UpdateDueSubtitle, Pan().Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-5")]
    public async Task While_the_prompt_of_the_switch_is_up_the_row_says_so_and_cannot_be_switched_again()
    {
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steam = OldPan();
        steam.PanPromptUp = prompt;
        using var arcadia = await StartAsync(steam, Chosen(panEnabled: false));
        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        PanRowViewModel Pan() => Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

        Pan().Activate();
        await arcadia.WaitUntilAsync(() => steam.PanUpdates == 1, "die Windows-Abfrage");
        Assert.Equal(PanRows.UpdatingSubtitle, Pan().Subtitle);
        Assert.True(Pan().IsLocked);
        Pan().Activate();
        Assert.Equal(1, steam.PanUpdates);
        // No second Windows prompt beside the update's: "Controller wiederherstellen" waits too.
        var restore = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.RestoreKey));
        Assert.False(restore.IsEnabled, "Controller wiederherstellen can start a second prompt while the update's is up");

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.PanEnabled, "Pan ist an");
        Assert.False(Pan().IsLocked);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-5")]
    public async Task A_failed_update_from_the_switch_keeps_Pan_off_and_shows_the_error_in_the_panel()
    {
        const string error = "Pan --install meldete Fehler 3 – Details stehen in pan.log.";
        var steam = OldPan();
        steam.PanUpdateError = error;
        using var arcadia = await StartAsync(steam, Chosen(panEnabled: false));
        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        PanRowViewModel Pan() => Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

        Pan().Activate();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == PanRows.NotUpdatedToast, "der Toast „bleibt aus“");

        Assert.Equal(1, steam.PanUpdates);
        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.False(arcadia.ControllerSupport.MayTalkToPan, "Arcadia still leaves the old Pan alone");
        Assert.False(Pan().IsLocked);
        Assert.Equal(PanRows.UpdateDueSubtitle, Pan().Subtitle);
        Assert.Equal(PanRows.UpdateDueInfo, Assert.IsType<InfoNote>(Pan().Info[0]).Text);
        Assert.Contains(Pan().Info.OfType<InfoError>(), block => block.Text == error);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-5")]
    public async Task An_unexpected_error_in_the_update_from_the_switch_still_leaves_the_row_off_and_unlocked()
    {
        var steam = OldPan();
        steam.PanUpdateBug = new NullReferenceException("Fehler im Test");
        using var arcadia = await StartAsync(steam, Chosen(panEnabled: false));
        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        PanRowViewModel Pan() => Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

        Pan().Activate();
        // Not by the toast: the error's own toast ("Unerwarteter Fehler …") is posted to the UI thread and may come last.
        await arcadia.WaitUntilAsync(() => !Pan().IsLocked, "die Zeile ist wieder frei");

        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.Equal(PanRows.UpdateDueSubtitle, Pan().Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-6")]
    public async Task An_open_firmware_notice_comes_first_and_A_pressed_earlier_waits_for_it()
    {
        var steam = OldPan();
        steam.IsSteamRunning = true;
        using var arcadia = await StartAsync(steam);
        var page = arcadia.Main.PanUpdate!;
        arcadia.Press(UiAction.Accept);
        Assert.Equal(PanUpdateState.Waiting, page.State);

        // One dialog at a time: the notice of a firmware attempt (#295) is closed before the update's Windows prompt.
        arcadia.Main.ShowFirmwareAttempt();
        await arcadia.PanAnsweredAtStartAsync();
        Assert.NotNull(arcadia.Main.FirmwareAttempt);
        Assert.Equal(0, steam.PanUpdates);

        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.FirmwareAttempt);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");
        Assert.Equal(1, steam.PanUpdates);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-7")]
    public async Task An_old_Pan_still_running_after_45_s_gets_the_restore_and_a_confirmed_restore_opens_the_library()
    {
        var steam = OldPan();
        steam.IsPanRunning = true;
        using var arcadia = await StartAsync(steam);

        var page = await StuckAsync(arcadia);

        Assert.Equal(PanUpdatePageViewModel.StuckTitle, page.Title);
        Assert.Equal(PanUpdatePageViewModel.RestoreHint, page.Hint);
        Assert.Equal("Controller wiederherstellen", page.ConfirmText);
        Assert.Equal("Arcadia beenden", page.CancelText);
        Assert.Equal(0, steam.Restores);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Wiederherstellung");
        Assert.Equal(1, steam.Restores);
        Assert.Equal(PanUpdatePageViewModel.RestoredToast, arcadia.Main.Toast.Text);
        Assert.False(arcadia.Settings.Current.PanEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-7")]
    public async Task A_declined_or_failed_restore_shows_the_steps_by_hand_and_checking_again_opens_the_library_once_Pan_ended()
    {
        var steam = OldPan();
        steam.IsPanRunning = true;
        steam.PromptConfirmed = true;
        using var arcadia = await StartAsync(steam);
        var page = await StuckAsync(arcadia);

        steam.PromptConfirmed = false;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => page.State == PanUpdateState.ManualCleanup, "Stufe 2 „Controller von Hand freigeben“");
        Assert.Equal(PanUpdatePageViewModel.ManualTitle, page.Title);
        Assert.Equal(PanUpdatePageViewModel.RestoreDeclined, page.Error);
        Assert.True(page.ShowsSteps);
        Assert.Equal("Erneut prüfen", page.ConfirmText);

        // Still running: checking again counts down and comes back to the steps.
        arcadia.Press(UiAction.Accept);
        Assert.Equal(PanUpdateState.Stopping, page.State);
        Assert.Null(page.Error);
        await RunClockUntilAsync(arcadia, () => page.State == PanUpdateState.Stuck, "wieder Stufe 1");

        steam.RestoreError = "Pan --restore meldete Fehler 5";
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => page.State == PanUpdateState.ManualCleanup, "Stufe 2 mit dem Fehler");
        Assert.Equal("Pan --restore meldete Fehler 5", page.Error);

        // Cleaned up by hand: the old Pan gives up and ends.
        steam.IsPanRunning = false;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek");
        Assert.Equal(PanUpdatePageViewModel.DeclinedToast, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-7")]
    public async Task B_ends_Arcadia_at_either_stage()
    {
        var steam = OldPan();
        steam.IsPanRunning = true;
        using var arcadia = await StartAsync(steam);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;
        var page = await StuckAsync(arcadia);

        arcadia.Press(UiAction.Back);
        Assert.Equal(1, quit);

        steam.PromptConfirmed = false;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => page.State == PanUpdateState.ManualCleanup, "Stufe 2");
        arcadia.Press(UiAction.Back);
        Assert.Equal(2, quit);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T354-1")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task While_the_update_runs_Arcadia_leaves_Pan_alone_and_after_it_talks_only_to_the_new_one(bool succeeds)
    {
        // #354: --install stops the old service and waits for it; a reconnecting client restarted it and the update failed.
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steam = OldPan();
        steam.PanPromptUp = prompt;
        steam.PanUpdateError = succeeds ? null : "Pan --install meldete Fehler 3 – Details stehen in pan.log.";
        using var arcadia = await StartAsync(steam);
        await arcadia.PanAnsweredAtStartAsync();
        Assert.True(arcadia.ControllerSupport.MayTalkToPan, "before [A] Pan's first hello still goes through");

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.PanUpdates == 1, "die Windows-Abfrage der Aktualisierung");
        Assert.Equal(ControllerSupportService.PanUpdatingReason, arcadia.ControllerSupport.WhyPanIsLeftAlone);

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate?.State != PanUpdateState.Updating, "das Ende der Aktualisierung");
        Assert.Equal(succeeds ? null : ControllerSupportService.OldPanOffReason, arcadia.ControllerSupport.WhyPanIsLeftAlone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T354-1")]
    public async Task Declined_the_update_keeps_Pan_left_alone_until_Pan_is_off()
    {
        // #354, the third way out: a declined Windows prompt must not let the client back to the old Pan before Pan is off.
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steam = OldPan();
        steam.PanPromptUp = prompt;
        steam.PromptConfirmed = false;
        using var arcadia = await StartAsync(steam);
        await arcadia.PanAnsweredAtStartAsync();

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.PanUpdates == 1, "die Windows-Abfrage der Aktualisierung");
        Assert.Equal(ControllerSupportService.PanUpdatingReason, arcadia.ControllerSupport.WhyPanIsLeftAlone);

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate?.State != PanUpdateState.Updating, "das Ende der Aktualisierung");
        Assert.False(arcadia.ControllerSupport.IsPanUpdating);
        Assert.Equal(ControllerSupportService.OldPanOffReason, arcadia.ControllerSupport.WhyPanIsLeftAlone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-8")]
    public async Task While_a_prompt_is_up_no_press_quits_starts_a_second_update_or_reaches_the_library()
    {
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steam = OldPan();
        steam.PanPromptUp = prompt;
        using var arcadia = await StartAsync(steam);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;

        await arcadia.PanAnsweredAtStartAsync();
        var page = arcadia.Main.PanUpdate!;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.PanUpdates == 1, "die Windows-Abfrage der Aktualisierung");
        Assert.Equal(PanUpdateState.Updating, page.State);
        Assert.Equal(PanUpdatePageViewModel.PromptHint, page.Hint);

        arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Search).Press(UiAction.Settings);
        page.ConfirmCommand.Execute(null);
        page.CancelCommand.Execute(null);

        Assert.Equal(0, quit);
        Assert.Equal(1, steam.PanUpdates);
        Assert.True(arcadia.Settings.Current.PanEnabled);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(page, arcadia.Main.PanUpdate);

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der bestätigten Abfrage");
        Assert.Equal(1, steam.PanUpdates);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T326-8")]
    public async Task While_the_restore_prompt_is_up_no_press_quits_starts_a_second_restore_or_reaches_the_library()
    {
        var steam = OldPan();
        steam.IsPanRunning = true;
        using var arcadia = await StartAsync(steam);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;
        var page = await StuckAsync(arcadia);
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        steam.PanPromptUp = prompt;

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.Restores == 1, "die Windows-Abfrage der Wiederherstellung");
        Assert.Equal(PanUpdateState.Restoring, page.State);
        Assert.Equal(PanUpdatePageViewModel.PromptHint, page.Hint);

        arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Search).Press(UiAction.Settings);
        page.ConfirmCommand.Execute(null);
        page.CancelCommand.Execute(null);

        Assert.Equal(0, quit);
        Assert.Equal(1, steam.Restores);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(page, arcadia.Main.PanUpdate);

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der bestätigten Abfrage");
        Assert.Equal(1, steam.Restores);
        Assert.Equal(PanUpdatePageViewModel.RestoredToast, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-4")]
    public async Task The_update_installs_no_driver_and_neither_closes_Steam_nor_waits_for_a_Steam_game()
    {
        var steam = OldPan();
        steam.IsSteamRunning = true;
        steam.IsUsbipReady = false;
        steam.HidHideVersion = null;
        using var arcadia = await StartAsync(steam);
        arcadia.RunningSteamAppId = 620;

        await arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");

        Assert.Equal(1, steam.PanUpdates);
        Assert.Equal(0, steam.DriverInstalls);
        Assert.True(steam.IsSteamRunning);
        Assert.False(steam.IsUsbipReady, "a missing driver stays the \"Treiber installieren\" row");
    }
}
