using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Arcadia runs only with the Pan it brings (#297): an installed Pan that differs is replaced at start without a question,
/// and until that worked or Pan is removed the library can't be reached. Pan's <c>--install</c> and the UAC prompt are
/// faked (<see cref="FakeSteam.UpdatePanAsync"/>); the real exe and prompt are the manual part
/// (docs/testplans/iteration-2/297-arcadia-runs-only-with-a-compatible-pan.md).
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

    [AvaloniaFact]
    [Trait("TestCase", "T297-1")]
    public async Task Another_Pan_is_replaced_at_start_without_a_question_and_then_the_library_shows()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired };
        using var arcadia = await StartAsync(steam);

        var page = arcadia.Main.PanUpdate;
        Assert.NotNull(page);
        Assert.Equal(PanUpdatePageViewModel.UpdatingTitle, page.Title);
        Assert.Contains("dauert deshalb etwas länger", page.Text);
        Assert.Null(arcadia.Main.Confirmation);

        await arcadia.PanAnsweredAtStartAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");

        Assert.Equal(1, steam.PanUpdates);
        Assert.Equal(PanUpdatePageViewModel.UpdatedToast, arcadia.Main.Toast.Text);
        Assert.False(arcadia.ControllerSupport.IsPanUpdateDue);
        arcadia.Press(UiAction.Settings);
        Assert.True(arcadia.Main.IsSettingsOpen, "die Bibliothek nimmt Eingaben wieder an");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-2")]
    public async Task Without_Pan_or_with_the_bundled_one_Arcadia_starts_without_a_page()
    {
        var none = new FakeSteam { PanCompatibility = PanCompatibility.NotInstalled, IsPanReady = false };
        using (var arcadia = await StartAsync(none))
        {
            await arcadia.PanAnsweredAtStartAsync();
            Assert.Null(arcadia.Main.PanUpdate);
            Assert.Equal(0, none.PanUpdates);
        }

        var same = new FakeSteam();
        using (var arcadia = await StartAsync(same))
        {
            await arcadia.PanAnsweredAtStartAsync();
            Assert.Null(arcadia.Main.PanUpdate);
            Assert.Equal(0, same.PanUpdates);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-3")]
    public async Task A_Pan_switched_off_in_Arcadia_is_replaced_all_the_same()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired };
        using var arcadia = await StartAsync(steam, Chosen(panEnabled: false));

        Assert.NotNull(arcadia.Main.PanUpdate);
        await arcadia.PanAnsweredAtStartAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");
        Assert.Equal(1, steam.PanUpdates);
        Assert.False(arcadia.Settings.Current.PanEnabled, "the update doesn't switch Pan on");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-4")]
    public async Task The_update_installs_no_driver_and_neither_closes_Steam_nor_waits_for_a_Steam_game()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, IsSteamRunning = true, IsUsbipReady = false, HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        arcadia.RunningSteamAppId = 620;

        await arcadia.PanAnsweredAtStartAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");

        Assert.Equal(1, steam.PanUpdates);
        Assert.Equal(0, steam.DriverInstalls);
        Assert.True(steam.IsSteamRunning);
        Assert.False(steam.IsUsbipReady, "a missing driver stays the \"Treiber installieren\" row");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-5")]
    public async Task A_firmware_hold_from_before_shows_its_dialog_first_and_the_update_runs_after_it()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, IsSteamRunning = true };
        using var arcadia = await StartAsync(steam);
        arcadia.Pan.HeldForFirmware = true;

        await arcadia.PanAnsweredAtStartAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Dialog „Steam will die Firmware aktualisieren“");
        var dialog = arcadia.Main.FirmwareAttempt!;
        Assert.Equal(0, steam.PanUpdates);
        Assert.Equal(PanUpdateState.Waiting, arcadia.Main.PanUpdate!.State);

        // Steam stays quiet: it gave up, "Schließen" has the focus and closes the dialog.
        arcadia.Wait(FirmwareWait);
        Assert.True(dialog.IsReady);
        Assert.Equal(0, steam.PanUpdates);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.FirmwareAttempt);

        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der Aktualisierung");
        Assert.Equal(1, steam.PanUpdates);
    }

    private static readonly TimeSpan FirmwareWait =
        Arcadia.ControllerSupport.Copy.FirmwareAttemptWatch.Quiet + Arcadia.ControllerSupport.Copy.FirmwareAttemptWatch.Countdown + TimeSpan.FromSeconds(1);

    [AvaloniaFact]
    [Trait("TestCase", "T297-6")]
    public async Task A_declined_prompt_keeps_the_library_closed_until_A_tries_again()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, PromptConfirmed = false };
        using var arcadia = await StartAsync(steam);

        await arcadia.PanAnsweredAtStartAsync();
        var page = arcadia.Main.PanUpdate!;
        await arcadia.WaitUntilAsync(() => page.IsFailed, "die Seite „Pan muss aktualisiert werden“");
        Assert.Equal(PanUpdatePageViewModel.FailedTitle, page.Title);
        Assert.Equal(PanUpdatePageViewModel.UpdateDeclined, page.Error);

        // Nothing below the page takes a press: no settings, no search, no game.
        arcadia.Press(UiAction.Settings).Press(UiAction.Right).Press(UiAction.Options);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.Same(page, arcadia.Main.PanUpdate);

        steam.PromptConfirmed = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach dem zweiten Versuch");
        Assert.Equal(2, steam.PanUpdates);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-7")]
    public async Task A_failed_update_shows_the_error_and_X_removes_Pan_unless_that_prompt_is_declined_too()
    {
        const string error = "Pan --install meldete Fehler 3 – Details stehen in pan.log.";
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, PanUpdateError = error };
        using var arcadia = await StartAsync(steam);

        await arcadia.PanAnsweredAtStartAsync();
        var page = arcadia.Main.PanUpdate!;
        await arcadia.WaitUntilAsync(() => page.IsFailed, "die Seite mit dem Fehler");
        Assert.Equal(error, page.Error);

        steam.PromptConfirmed = false;
        arcadia.Press(UiAction.Search);
        await arcadia.WaitUntilAsync(() => page.IsFailed && page.Error == PanUpdatePageViewModel.RemoveDeclined, "die Seite bleibt");
        Assert.Same(page, arcadia.Main.PanUpdate);
        Assert.Equal(1, steam.PanUninstalls);

        steam.PromptConfirmed = true;
        arcadia.Press(UiAction.Search);
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek ohne Pan");
        Assert.Equal(2, steam.PanUninstalls);
        Assert.False(arcadia.Settings.Current.PanEnabled);
        Assert.Equal(PanUpdatePageViewModel.RemovedToast, arcadia.Main.Toast.Text);
        Assert.Equal(1, steam.PanUpdates);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-8")]
    public async Task B_on_the_failed_page_ends_Arcadia_and_does_nothing_while_the_prompt_is_up()
    {
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, PromptConfirmed = false };
        using var arcadia = await StartAsync(steam);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;

        // Before the update ran (waiting for Pan), B ends nothing.
        arcadia.Press(UiAction.Back);
        Assert.Equal(0, quit);

        await arcadia.PanAnsweredAtStartAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate!.IsFailed, "die Seite „Pan muss aktualisiert werden“");
        arcadia.Press(UiAction.Back);
        Assert.Equal(1, quit);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T297-8")]
    public async Task While_the_prompt_is_up_no_press_quits_starts_a_second_update_or_removes_Pan()
    {
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steam = new FakeSteam { PanCompatibility = PanCompatibility.UpdateRequired, PanPromptUp = prompt };
        using var arcadia = await StartAsync(steam);
        var quit = 0;
        arcadia.Main.QuitRequested += (_, _) => quit++;

        await arcadia.PanAnsweredAtStartAsync();
        var page = arcadia.Main.PanUpdate!;
        await arcadia.WaitUntilAsync(() => steam.PanUpdates == 1, "die Windows-Abfrage der Aktualisierung");
        Assert.Equal(PanUpdateState.Updating, page.State);
        Assert.True(page.IsAskingWindows);

        arcadia.Press(UiAction.Back).Press(UiAction.Accept).Press(UiAction.Search).Press(UiAction.Settings);
        page.QuitCommand.Execute(null);
        page.RetryCommand.Execute(null);
        page.RemoveCommand.Execute(null);

        Assert.Equal(0, quit);
        Assert.Equal(1, steam.PanUpdates);
        Assert.Equal(0, steam.PanUninstalls);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(page, arcadia.Main.PanUpdate);

        prompt.SetResult();
        await arcadia.WaitUntilAsync(() => arcadia.Main.PanUpdate is null, "die Bibliothek nach der bestätigten Abfrage");
        Assert.Equal(1, steam.PanUpdates);
    }
}
