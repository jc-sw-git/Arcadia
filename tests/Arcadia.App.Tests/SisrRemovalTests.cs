using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The group "SISR" in the controller settings (#214): "SISR entfernen" with Steam closed or running, a Steam game or a
/// Steam that won't close in the way, an SISR that won't stop, "SISR behalten", and the warning under Pan while SISR
/// runs. SISR's files and Steam's shortcuts.vdf are real files of the test; Windows (processes, the registry) is faked.
/// </summary>
public sealed class SisrRemovalTests
{
    private const string Removed = "SISR ist vollständig entfernt.";

    [AvaloniaFact]
    [Trait("TestCase", "T214-1")]
    [Trait("TestCase", "T214-2")]
    public async Task The_group_shows_only_while_SISR_is_there_below_Steam_and_opening_it_removes_nothing()
    {
        using (var without = await ArcadiaDriver.StartAsync(steam: new FakeSteam()))
        {
            var controller = await OpenControllerAsync(without);
            Assert.DoesNotContain(controller.Rows, row => row.Group == SisrRows.Group);
        }

        // SISR uninstalled by hand, its marker left in Steam: the group stays for the marker.
        var markerOnly = new FakeSteam();
        markerOnly.Sisr.WithSisr(program: false);
        using (var leftover = await ArcadiaDriver.StartAsync(steam: markerOnly))
        {
            var controller = await OpenControllerAsync(leftover);
            Assert.Equal("Übrig ist noch ein Eintrag in Steam", controller.Rows.Single(row => row.Key == SisrRows.RemoveKey).Subtitle);
        }

        var steam = new FakeSteam();
        steam.Sisr.WithSisr();
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var settings = await OpenControllerAsync(arcadia);
        await arcadia.TickControllerSupportAsync();
        arcadia.Wait(TimeSpan.FromSeconds(30));
        await arcadia.TickControllerSupportAsync();

        var keys = settings.Rows.Select(row => row.Key).ToList();
        Assert.Equal(["steam-desktop-layout", SisrRows.RemoveKey, SisrRows.KeepKey], keys.SkipWhile(key => key != "steam-desktop-layout").Take(3));
        var keep = settings.Rows.Single(row => row.Key == SisrRows.KeepKey);
        Assert.Equal((SisrRows.Group, 1), (keep.Group, keep.Indent));
        Assert.Equal("Pan ersetzt SISR. Arcadia braucht es nicht mehr.", settings.Rows.Single(row => row.Key == SisrRows.RemoveKey).Subtitle);
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.Equal(1, steam.Sisr.Installation().Find().MarkerAccounts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-3")]
    [Trait("TestCase", "T214-11")]
    public async Task With_Steam_closed_A_removes_SISR_at_once_shows_the_toast_and_the_focus_moves_up()
    {
        var steam = new FakeSteam { IsSteamRunning = false };
        steam.Sisr.WithSisr();
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);

        arcadia.FocusRow(SisrRows.RemoveKey);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == Removed, "der Toast „SISR ist vollständig entfernt.“");

        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.DoesNotContain(controller.Rows, row => row.Group == SisrRows.Group);
        Assert.Equal("steam-desktop-layout", arcadia.Main.Settings!.FocusedRow?.Key);
        Assert.False(Directory.Exists(Path.GetDirectoryName(steam.Sisr.SisrExe)));
        Assert.False(File.Exists(steam.Sisr.CefFlag));
        Assert.Equal(0, steam.SteamStops + steam.StartRequests);

        // B still leaves the settings as always.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-4")]
    [Trait("TestCase", "T214-11")]
    public async Task With_Steam_running_A_asks_Steam_beenden_B_cancels_and_Weiter_removes_with_Steam_closed_and_starts_it_again()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        steam.Sisr.WithSisr();
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal("Den Eintrag „SISR Marker“ kann Arcadia nur entfernen, solange Steam geschlossen ist. Steam wird dafür kurz beendet und danach wieder gestartet.", controller.SteamStopText);
        Assert.Equal(("Weiter", "Abbrechen"), (controller.ConfirmSteamStopText, controller.PromptBackText));
        Assert.False(controller.HasSteamStopDetail);
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);

        // [B] Abbrechen: nothing changes.
        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.Equal(0, steam.SteamStops);

        // [A] Weiter: Steam closes, everything goes, Steam comes back.
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == Removed, "der Toast „SISR ist vollständig entfernt.“");

        Assert.Equal((1, 1), (steam.SteamStops, steam.StartRequests));
        Assert.True(steam.IsSteamRunning);
        Assert.Equal(0, steam.Sisr.Installation().Find().MarkerAccounts);
        Assert.DoesNotContain(controller.Rows, row => row.Group == SisrRows.Group);
        Assert.Equal("steam-desktop-layout", arcadia.Main.Settings!.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-4")]
    public async Task A_running_Steam_game_is_named_with_only_Schliessen_and_nothing_is_removed()
    {
        var steam = new FakeSteam { IsSteamRunning = true, SteamGame = "Hades II" };
        steam.Sisr.WithSisr();
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe, id: 5);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept);

        Assert.Equal(SettingsPrompt.SteamGameRunning, controller.Prompt);
        Assert.Equal("Hades II", controller.RunningGame);
        Assert.Equal("Beende das Spiel zuerst, dann kann Arcadia SISR entfernen.", controller.GamePromptText);
        Assert.False(controller.CanRetryGamePrompt);
        Assert.True(controller.CloseGamePromptItem.IsFocused);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.SteamStops);
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.Empty(steam.Sisr.System.Stopped);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-4")]
    public async Task A_Steam_that_won_t_close_shows_the_known_message_and_nothing_is_removed()
    {
        var steam = new FakeSteam { IsSteamRunning = true, RefusesToExit = true };
        steam.Sisr.WithSisr();
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.ControllerSupport.LastError is not null, "die Meldung, dass Steam nicht beendet wurde");

        Assert.Equal(ControllerSupportService.SteamWontCloseText, arcadia.ControllerSupport.LastError);
        await arcadia.WaitUntilAsync(() => controller.Prompt == SettingsPrompt.None, "der Dialog geht zu");
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.Equal(1, steam.Sisr.Installation().Find().MarkerAccounts);

        // The player sees why: the row says so, the cause in its info panel.
        var row = controller.Rows.Single(r => r.Key == SisrRows.RemoveKey);
        await arcadia.WaitUntilAsync(() => row.Subtitle == "SISR wurde nicht entfernt", "die Zeile sagt, dass nichts entfernt wurde");
        Assert.Equal(HealthSeverity.Warning, row.Severity);
        Assert.Contains(row.Info, block => block is InfoNote { Text: ControllerSupportService.SteamWontCloseText });
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-4")]
    public async Task A_Steam_that_won_t_close_leaves_a_running_SISR_running()
    {
        // "Nothing removed" includes the SISR a game may be using right now: it is stopped only once Steam is closed.
        var steam = new FakeSteam { IsSteamRunning = true, RefusesToExit = true };
        steam.Sisr.WithSisr();
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe, id: 5);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.ControllerSupport.LastError is not null, "die Meldung, dass Steam nicht beendet wurde");

        Assert.Equal(ControllerSupportService.SteamWontCloseText, arcadia.ControllerSupport.LastError);
        Assert.Empty(steam.Sisr.System.Stopped);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-8")]
    public async Task An_SISR_that_won_t_stop_leaves_the_row_saying_why_and_the_next_try_finishes()
    {
        var steam = new FakeSteam { IsSteamRunning = false };
        steam.Sisr.WithSisr();
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe, id: 5);
        steam.Sisr.System.Unstoppable.Add(5);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept);
        var row = controller.Rows.Single(r => r.Key == SisrRows.RemoveKey);
        await arcadia.WaitUntilAsync(() => row.Subtitle == "SISR wurde nicht entfernt", "die Zeile sagt, dass nichts entfernt wurde");

        Assert.Equal(HealthSeverity.Warning, row.Severity);
        Assert.Contains(row.Info, block => block is InfoNote { Text: SisrInstallation.StopFailedCause });
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.NotEqual(Removed, arcadia.Main.Toast.Text);

        // SISR closed by the user: the same row, A again, finishes the job.
        steam.Sisr.System.Unstoppable.Clear();
        arcadia.ControllerSupport.ClearError();
        arcadia.FocusRow(SisrRows.RemoveKey);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == Removed, "der Toast „SISR ist vollständig entfernt.“");
        Assert.DoesNotContain(controller.Rows, r => r.Group == SisrRows.Group);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-8")]
    public async Task What_is_left_after_SISR_and_its_marker_went_keeps_the_row_saying_why_without_a_toast()
    {
        var steam = new FakeSteam { IsSteamRunning = false };
        steam.Sisr.WithSisr();
        steam.Sisr.System.HasUninstallEntry = true;
        steam.Sisr.System.UninstallEntryLocked = true;
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);
        arcadia.FocusRow(SisrRows.RemoveKey);

        arcadia.Press(UiAction.Accept);
        var row = controller.Rows.Single(r => r.Key == SisrRows.RemoveKey);
        await arcadia.WaitUntilAsync(() => row.Subtitle == "Nicht alles ließ sich entfernen", "die Zeile sagt, dass etwas übrig ist");

        Assert.False(File.Exists(steam.Sisr.SisrExe));
        Assert.Contains(row.Info, block => block is InfoNote { Text: var cause } && cause.StartsWith("SISR-Deinstallationseintrag", StringComparison.Ordinal));
        Assert.NotEqual(Removed, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-6")]
    [Trait("TestCase", "T214-11")]
    public async Task SISR_behalten_hides_the_group_moves_the_focus_up_keeps_SISR_and_stays_hidden_after_a_restart()
    {
        var steam = new FakeSteam();
        steam.Sisr.WithSisr();
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        var controller = await OpenControllerAsync(arcadia);

        var keep = arcadia.FocusRow(SisrRows.KeepKey);
        Assert.Equal("Ausblenden", Assert.IsType<ActionRowViewModel>(keep).ActionText);
        arcadia.Press(UiAction.Accept);

        Assert.True(arcadia.Settings.Current.KeepSisr);
        Assert.DoesNotContain(controller.Rows, row => row.Group == SisrRows.Group);
        Assert.Equal("steam-desktop-layout", arcadia.Main.Settings!.FocusedRow?.Key);
        Assert.True(File.Exists(steam.Sisr.SisrExe));
        Assert.Equal(0, steam.SteamStops);

        using var restarted = await arcadia.RestartAsync();
        var again = await OpenControllerAsync(restarted);
        Assert.DoesNotContain(again.Rows, row => row.Group == SisrRows.Group);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T214-7")]
    public async Task While_SISR_runs_beside_Pan_a_warning_stands_under_Pan_also_with_the_group_hidden()
    {
        var steam = new FakeSteam();
        steam.Sisr.WithSisr();
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, settings: new AppSettings
        {
            ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
            KeepSisr = true,
        });
        var controller = await OpenControllerAsync(arcadia);
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:sisr-running"), "die Warnung „SISR läuft“");

        var warning = controller.Rows.Single(row => row.Key == "issue:sisr-running");
        Assert.Equal(("SISR läuft", 1, HealthSeverity.Warning), (warning.Title, warning.Indent, warning.Severity));
        Assert.Equal(PanRows.Group, warning.Group);
        Assert.DoesNotContain(controller.Rows, row => row.Group == SisrRows.Group);

        // SISR ended: the warning goes with the next look.
        steam.Sisr.System.Running.Clear();
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => row.Key != "issue:sisr-running"), "die Warnung geht");
    }

    /// <summary>≡, then RB to "Controller", with what the settings found of SISR shown.</summary>
    private static async Task<ControllerSettingsViewModel> OpenControllerAsync(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        // The look runs off the UI thread; its rows come with the next refresh on it.
        await arcadia.WaitUntilAsync(
            () => arcadia.ControllerSupport.SisrFound is not null && controller.Rows.Any(row => row.Group == SisrRows.Group) == arcadia.ControllerSupport.ShowsSisrGroup,
            "die Einstellungen haben nach SISR gesehen");
        return controller;
    }
}
