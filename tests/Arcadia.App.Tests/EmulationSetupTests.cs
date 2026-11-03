using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// What the controller settings say about the emulation's parts (#243 part B2): usbip-win2, VIIPER, Pan and HidHide,
/// each with its row and one install for all; HidHide's states as Pan reports them; and where Pan's notices go. The
/// Windows side (UAC, the real installers, HidHide's configuration client) is the manual part (T243-32 to T243-38).
/// </summary>
public sealed class EmulationSetupTests
{
    private static async Task<(ArcadiaDriver Arcadia, ControllerSettingsViewModel Controller)> OpenControllerSettingsAsync(FakeSteam steam)
    {
        var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return (arcadia, Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected));
    }

    /// <summary>"Controller-Wächter (Pan)" (#242), which took the place of "Steam Controller Kompatibilität".</summary>
    private static PanRowViewModel Switch(ControllerSettingsViewModel controller) =>
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));

    private const string ProblemText = "Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter.";

    /// <summary>HidHide's installer runs: the settings say first that Arcadia restarts afterwards (#396); [A] goes on.</summary>
    private static void ConfirmHidHideInstall(ArcadiaDriver arcadia, ControllerSettingsViewModel controller)
    {
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);
        arcadia.Press(UiAction.Accept);
    }

    /// <summary>"Treiber installieren" with every part missing asks first what it installs (#407).</summary>
    private static void ConfirmDriversInstall(ArcadiaDriver arcadia, ControllerSettingsViewModel controller)
    {
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal(ControllerSettingsViewModel.DriversInstallTitle, controller.SteamStopTitle);
        Assert.Equal(ControllerSettingsViewModel.DriversInstallText, controller.SteamStopText);
        arcadia.Press(UiAction.Accept);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-32")]
    public async Task A_missing_HidHide_greys_out_the_switch_and_its_row_installs_it()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");
        var hidHide = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing"));
        Assert.Equal("HidHide fehlt", hidHide.Title);
        Assert.Equal("Installieren", hidHide.ActionText);
        Assert.Equal(HealthSeverity.Warning, hidHide.Severity);
        // The switch stays a switch (on by default); the row below says what is missing (#242).
        var pan = Switch(controller);
        Assert.True(pan.IsSwitch);
        Assert.True(pan.IsOn);
        Assert.Equal(ProblemText, pan.Subtitle);
        Assert.True(pan.IsLampProblem);
        Assert.Equal("Versteckt die echten Controller vor anderen Programmen", hidHide.Subtitle);

        hidHide.Activate();
        ConfirmHidHideInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => row.Key != "issue:hidhide-missing"), "die Meldung verschwindet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
        Assert.NotEqual(ProblemText, Switch(controller).Subtitle);
        Assert.Null(controller.Marker);
    }

    /// <summary>#396: HidHide doesn't let an Arcadia that ran before its install see what Pan hides.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T396-3")]
    public async Task Before_HidHide_is_installed_the_settings_say_Arcadia_restarts_and_after_it_Arcadia_restarts_before_Pan_hides()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");
        var hidHide = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing"));

        // Also without Steam running, the question comes first; [B] installs nothing.
        hidHide.Activate();
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);
        Assert.Equal(ControllerSettingsViewModel.HidHideRestartText, controller.SteamStopText);
        Assert.DoesNotContain("Steam beendet", controller.SteamStopDetail, StringComparison.Ordinal);
        Assert.Equal("Installieren", controller.ConfirmSteamStopText);
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);
        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal(0, steam.DriverInstalls);
        Assert.Empty(arcadia.Restarts);

        // [A] installs; HidHide came, so Arcadia restarts, and Pan hides nothing until then.
        hidHide.Activate();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && arcadia.ControllerSupport.BusyText is null, "installiert");
        await arcadia.WaitUntilAsync(() => arcadia.Restarts.Count == 1, "der Neustart von Arcadia");
        Assert.Equal(ControllerSupportService.RestartPendingText, arcadia.ControllerSupport.HoldsEmulation);

        // Until the restart, Pan hides nothing (here the restart is only counted, as if it had failed).
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        for (var tick = 0; tick < 3; tick++)
        {
            arcadia.Time.Advance(TimeSpan.FromSeconds(10));
            await arcadia.TickSteamControllerAsync();
        }

        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Equal(ControllerSupportService.RestartPendingText, arcadia.ForGames.Status.Reason);

        // A restart that didn't happen can be asked for again from its row.
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "pan:restart-pending"), "die Zeile „Arcadia startet nach der HidHide-Installation neu“");
        var pending = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "pan:restart-pending"));
        Assert.Equal(PanRows.RestartText, pending.ActionText);
        pending.Activate();
        Assert.Equal(2, arcadia.Restarts.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T396-3")]
    public async Task An_install_without_HidHide_s_installer_asks_nothing_and_needs_no_restart()
    {
        var steam = new FakeSteam { IsPanReady = false };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:pan-missing"), "die Meldung „Wächterdienst Pan fehlt“");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:pan-missing")).Activate();
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && arcadia.ControllerSupport.BusyText is null, "Pan installiert");

        Assert.Empty(arcadia.Restarts);
        Assert.Null(arcadia.ControllerSupport.HoldsEmulation);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-32")]
    public async Task A_HidHide_setup_that_fails_inside_Pan_s_install_keeps_its_row_with_the_error_while_Pan_is_set_up()
    {
        const string error = "Pan ist eingerichtet, HidHide aber nicht – Details stehen in pan.log. HidHide lässt sich auch selbst installieren: https://github.com/nefarius/HidHide/releases";
        var steam = new FakeSteam { IsPanReady = false, HidHideVersion = null, HidHideInstallError = error };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:pan-missing"), "die Meldung „Wächterdienst Pan fehlt“");
        Assert.Contains(controller.Rows, row => row.Key == "issue:hidhide-missing");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:pan-missing")).Activate();
        ConfirmHidHideInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.Rows.OfType<ActionRowViewModel>().Any(row => row.HasProblem), "der Fehler an einer Zeile");

        // No HidHide came, so Arcadia doesn't restart.
        Assert.Empty(arcadia.Restarts);
        // One prompt; Pan came, HidHide didn't: its row stays and carries the error, nothing else does.
        Assert.Equal(1, steam.DriverInstalls);
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:pan-missing");
        var failed = Assert.Single(controller.Rows.OfType<ActionRowViewModel>(), row => row.HasProblem);
        Assert.Equal("issue:hidhide-missing", failed.Key);
        Assert.Equal(error, failed.Problem);
        var pan = Switch(controller);
        Assert.False(pan.HasProblem);
        Assert.Equal(ProblemText, pan.Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-33")]
    public async Task With_every_part_missing_the_switch_itself_offers_Treiber_installieren_instead_of_the_rows()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall, "„Treiber installieren“ an der Einstellung");
        var pan = Switch(controller);
        Assert.Equal("Treiber installieren", pan.ActivateText);
        Assert.Equal("Pan verwaltet, wie Spiele, Programme und Launcher deine Controller nutzen", pan.Subtitle);
        Assert.False(pan.HasLamp);
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.DoesNotContain(controller.Rows, row => row.Key == PanRows.RestoreKey);
        Assert.Equal(HealthSeverity.Warning, controller.Marker);

        pan.Activate();
        ConfirmDriversInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => Switch(controller).IsSwitch, "eingerichtet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(1, steam.Viiper.Installs);
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.True(Switch(controller).IsOn);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-33")]
    public async Task A_declined_install_of_every_part_keeps_VIIPER_and_shows_its_error_at_the_first_part_left()
    {
        const string error = "Die Installation wurde in der Windows-Abfrage abgelehnt – noch einmal „Installieren“ wählen und die Abfrage bestätigen.";
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null, InstallError = error };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall, "„Treiber installieren“ an der Einstellung");

        Switch(controller).Activate();
        ConfirmDriversInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.Rows.OfType<ActionRowViewModel>().Any(row => row.HasProblem), "der Fehler an einer Zeile");

        // VIIPER needs no Windows prompt and came before it: the rest is three parts with one error, at the first.
        Assert.True(steam.Viiper.IsInstalled);
        Assert.Equal(["issue:usbip-missing", "issue:pan-missing", "issue:hidhide-missing"], controller.Rows.Where(row => row.Key.StartsWith("issue:", StringComparison.Ordinal)).Select(row => row.Key));
        var failed = Assert.Single(controller.Rows.OfType<ActionRowViewModel>(), row => row.HasProblem);
        Assert.Equal("issue:usbip-missing", failed.Key);
        Assert.Equal(error, failed.Problem);
        var pan = Switch(controller);
        Assert.False(pan.HasProblem);
        Assert.Equal(ProblemText, pan.Subtitle);
    }

    /// <summary>
    /// #407 (owner's rc.2 test): Arcadia uninstalled with its drivers, installed again before the PC restarted. The
    /// install would meet the half removed HidHide, so "Treiber installieren" is greyed out until the restart.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T407-2")]
    public async Task While_a_driver_change_waits_for_the_restart_Treiber_installieren_is_greyed_out_and_says_why()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideRemovalPending = true };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:install-restart-pending"), "die Zeile „Windows-Neustart ausstehend“");
        var pan = Switch(controller);
        Assert.True(pan.IsInstall);
        Assert.True(pan.IsLocked);
        Assert.True(pan.IsDimmed);
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:install-restart-pending"));
        Assert.Equal("Windows-Neustart ausstehend", row.Title);
        Assert.Equal("Treiberänderungen stehen noch aus", row.Subtitle);
        Assert.True(row.IsInfo);
        Assert.Equal(string.Empty, row.ActionText);
        Assert.Equal(row.Key, controller.PanWarningKey);

        pan.Activate();
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.DriverInstalls);

        // After the restart HidHide is gone: every part is missing and nothing waits any more.
        steam.HidHideRemovalPending = false;
        steam.HidHideVersion = null;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => !Switch(controller).IsLocked, "„Treiber installieren“ wieder wählbar");
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:install-restart-pending");
        Switch(controller).Activate();
        ConfirmDriversInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => Switch(controller).IsSwitch, "eingerichtet");
        Assert.Equal(1, steam.DriverInstalls);
    }

    /// <summary>#407: an install restarts the puck and controllers, so it waits while a firmware update runs.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T407-3")]
    public async Task While_a_firmware_update_runs_Treiber_installieren_is_greyed_out_and_says_why()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null, FirmwareUpdateRuns = true };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:install-firmware-update"), "die Zeile „Controller-Update wird durchgeführt“");
        Assert.True(Switch(controller).IsLocked);
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:install-firmware-update"));
        Assert.Equal("Controller-Update wird durchgeführt", row.Title);
        Assert.Equal("Treiber kann während eines Firmware-Updates nicht installiert werden", row.Subtitle);
        Assert.True(row.IsInfo);
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:install-restart-pending");

        steam.FirmwareUpdateRuns = false;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => !Switch(controller).IsLocked, "„Treiber installieren“ wieder wählbar");
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:install-firmware-update");
    }

    /// <summary>#407: a row's "Installieren" (Pan on, one part missing) doesn't run during a firmware update either.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T407-3")]
    public async Task During_a_firmware_update_a_rows_install_stops_before_the_Windows_prompt()
    {
        var steam = new FakeSteam { HidHideVersion = null, FirmwareUpdateRuns = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Zeile „HidHide fehlt“");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing")).Activate();
        ConfirmHidHideInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.Rows.OfType<ActionRowViewModel>().Any(row => row.HasProblem), "der Fehler an der Zeile");

        Assert.Equal(0, steam.DriverInstalls);
        Assert.Equal(ControllerSupportService.FirmwareUpdateBlocksInstallText, controller.Rows.OfType<ActionRowViewModel>().Single(row => row.HasProblem).Problem);
    }

    /// <summary>#407: "Treiber installieren?" names the puck's restart only when a Steam Controller or puck is plugged in.</summary>
    [AvaloniaTheory]
    [Trait("TestCase", "T407-4")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Treiber_installieren_asks_first_and_names_the_puck_only_when_one_is_plugged_in(bool plugged)
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null, SteamControllerPlugged = plugged };
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall && !Switch(controller).IsLocked, "„Treiber installieren“ an der Einstellung");

        Switch(controller).Activate();

        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal("Treiber installieren?", controller.SteamStopTitle);
        Assert.Contains("Nach der Installation ist ein Neustart des PCs empfohlen.", controller.SteamStopText);
        Assert.Equal(plugged, controller.SteamStopDetail.Contains(ControllerSettingsViewModel.PuckRestartText, StringComparison.Ordinal));
        Assert.Equal("Installieren", controller.ConfirmSteamStopText);

        // B goes back: nothing installed.
        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.DriverInstalls);
    }

    /// <summary>#402: a fresh install has none of Pan's parts, Pan is off, and the one thing shown is the install button.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T402-1")]
    public async Task A_fresh_install_has_Pan_off_no_warnings_and_the_button_to_install_every_part()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        var fresh = new AppSettings { ActiveLaunchers = ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id).ToList() };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: fresh, steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);

        Assert.False(arcadia.Settings.Current.PanEnabled);
        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall, "„Treiber installieren“ an der Einstellung");
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.False(arcadia.Main.Status.HasControllerStatus);
        Assert.Null(controller.Marker);
    }

    /// <summary>#402: with Pan off and only some parts missing, nothing warns (the warnings belong to Pan being on).</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T402-2")]
    public async Task With_Pan_off_a_missing_part_shows_no_warning_row_and_no_triangle()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        var off = new AppSettings { ActiveLaunchers = ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id).ToList() };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: off, steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);

        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.False(arcadia.Main.Status.HasControllerStatus);
    }

    /// <summary>#402 (QE): Pan starts off, so "Treiber installieren" has to switch it on, or the player has set up Pan for nothing.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T402-2")]
    public async Task On_a_fresh_install_Treiber_installieren_sets_up_every_part_and_switches_Pan_on()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        var fresh = new AppSettings { ActiveLaunchers = ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id).ToList() };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: fresh, steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall, "„Treiber installieren“ an der Einstellung");

        Switch(controller).Activate();
        ConfirmDriversInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => Switch(controller).IsSwitch, "eingerichtet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(1, steam.Viiper.Installs);
        Assert.True(arcadia.Settings.Current.PanEnabled);
        Assert.True(Switch(controller).IsOn);
    }

    /// <summary>
    /// #402 (QE): an update from a version that shipped VIIPER leaves only <c>viiper.exe</c> missing. With Pan on, its
    /// row says so and "Installieren" fetches it without the Windows prompt; with Pan off, nothing warns.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T402-3")]
    public async Task After_an_update_without_VIIPER_Pan_on_shows_VIIPER_fehlt_and_Installieren_fetches_only_VIIPER()
    {
        var steam = new FakeSteam();
        steam.Viiper.IsInstalled = false;
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:viiper-missing"), "die Meldung „VIIPER fehlt“");
        var viiper = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:viiper-missing"));
        Assert.Equal("VIIPER fehlt", viiper.Title);
        Assert.Equal("Installieren", viiper.ActionText);
        Assert.True(Switch(controller).IsOn);
        Assert.True(arcadia.Main.Status.HasControllerStatus);

        viiper.Activate();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => row.Key != "issue:viiper-missing"), "VIIPER eingerichtet");

        Assert.Equal(1, steam.Viiper.Installs);
        Assert.Equal(0, steam.DriverInstalls);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T402-3")]
    public async Task After_an_update_without_VIIPER_Pan_off_stays_a_switch_without_warnings()
    {
        var steam = new FakeSteam();
        steam.Viiper.IsInstalled = false;
        var off = new AppSettings { ActiveLaunchers = ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id).ToList() };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: off, steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);

        await arcadia.WaitUntilAsync(() => Switch(controller).IsSwitch, "der Schalter");
        Assert.False(Switch(controller).IsOn);
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.False(arcadia.Main.Status.HasControllerStatus);
        Assert.Equal(0, steam.Viiper.Installs);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-34")]
    public async Task An_old_HidHide_gets_its_row_with_Aktualisieren()
    {
        // The owner's B2 test had 1.2.93 (owner, 2026-10-27: only the tested 1.5.230 and newer 1.x count).
        var steam = new FakeSteam { HidHideVersion = new Version(1, 2, 93, 0) };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-unsupported"), "die Meldung zur HidHide-Version");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-unsupported"));
        Assert.Equal("HidHide-Version wird nicht unterstützt", row.Title);
        Assert.Equal("Aktualisieren", row.ActionText);
        Assert.Contains("Installiert ist ein älteres HidHide (Treiber 1.2.93), Arcadia braucht HidHide 1.5.230 oder neuer.", row.Info.OfType<InfoText>().Single().Text);

        row.Activate();
        ConfirmHidHideInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => controller.Rows.All(r => r.Key != "issue:hidhide-unsupported"), "die Meldung verschwindet");
        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-41")]
    [Trait("TestCase", "T407-2")]
    public async Task An_uninstalled_HidHide_waiting_for_the_restart_says_so_and_offers_no_install()
    {
        // Owner's B2 test (2026-10-27): until the restart HidHide's service and driver file stay, so its version read as
        // installed and the settings showed nothing; its setup would fail while the old service waits for deletion.
        var steam = new FakeSteam { HidHideRemovalPending = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        arcadia.Pan.Hiding = PanReason.HidHideBusy;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-removal-pending"), "die Meldung „HidHide: Neustart ausstehend“");
        await arcadia.TickControllerSupportAsync();
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-removal-pending"));
        Assert.Equal("HidHide: Neustart ausstehend", row.Title);
        // #407: says what happened and what comes after the restart.
        Assert.Equal("HidHide wurde deinstalliert, Windows entfernt es beim Neustart", row.Subtitle);
        Assert.Contains(row.Info, block => block is InfoText { Text: var text } && text.Contains("Nach dem Neustart bietet Arcadia hier „Installieren“ an.", StringComparison.Ordinal));
        Assert.Equal(string.Empty, row.ActionText);
        // Nothing to do but restart: the blue i (#242).
        Assert.True(row.IsInfo);
        // Pan isn't asked about a HidHide on its way out: one row, not a second one from its state.
        Assert.Single(controller.Rows, row => row.Key.StartsWith("issue:hidhide", StringComparison.Ordinal));

        // After the restart HidHide is gone and can be installed again.
        steam.HidHideRemovalPending = false;
        steam.HidHideVersion = null;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:hidhide-removal-pending");
        Assert.Equal("Installieren", Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing")).ActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-42")]
    public async Task A_pending_restart_after_the_usbip_update_offers_no_SISR_restart_any_more()
    {
        var steam = new FakeSteam { UsbipRebootPending = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:reboot-pending"), "die Meldung „Windows-Neustart ausstehend“");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:reboot-pending"));
        Assert.Equal("Windows-Neustart ausstehend", row.Title);
        Assert.Equal("Bis dahin können Controller in Spielen kurz aussetzen", row.Subtitle);
        Assert.Equal(string.Empty, row.ActionText);
        Assert.True(row.IsInfo);
        Assert.DoesNotContain("SISR", row.Info.OfType<InfoText>().Single().Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-44")]
    public async Task A_Pan_that_ends_at_once_gets_a_row_whose_reinstall_repairs_it()
    {
        // Owner's B2 test (2026-10-27): Pan refused its data folder after Explorer's "Fortsetzen" and nothing said so.
        var steam = new FakeSteam { PanFailing = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:pan-failing"), "die Meldung „Wächterdienst Pan startet nicht“");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:pan-failing"));
        Assert.Equal("Wächterdienst Pan startet nicht", row.Title);
        Assert.Equal("Neu einrichten", row.ActionText);
        Assert.Contains(PanPaths.DataDirectory, row.Info.OfType<InfoText>().Single().Text);
        Assert.Equal(ProblemText, Switch(controller).Subtitle);
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.ControllerStatusText.Contains("Wächterdienst Pan startet nicht"), "das Dreieck im Header");

        row.Activate();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(r => r.Key != "issue:pan-failing"), "die Meldung verschwindet");
        Assert.Equal(1, steam.DriverInstalls);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-35")]
    public async Task While_HidHide_s_configuration_is_open_the_settings_ask_to_close_it()
    {
        var steam = new FakeSteam();
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.TickControllerSupportAsync();
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:hidhide", StringComparison.Ordinal));

        arcadia.Pan.Hiding = PanReason.HidHideBusy;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-busy"), "die Bitte, die HidHide-Konfiguration zu schließen");
        var busy = controller.Rows.Single(row => row.Key == "issue:hidhide-busy");
        Assert.Equal("HidHide-Konfiguration ist geöffnet", busy.Title);
        Assert.Contains("Bitte schließe sie", busy.Subtitle);
        // Nothing to install: HidHide is there, the user closes its window.
        Assert.Equal(string.Empty, Assert.IsType<ActionRowViewModel>(busy).ActionText);
        Assert.False(Switch(controller).HasProblem);

        arcadia.Pan.Hiding = PanReason.HidHideInverseMode;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-inverse"), "die Meldung zur umgekehrten Liste");
        Assert.DoesNotContain(controller.Rows, row => row.Key == "issue:hidhide-busy");

        arcadia.Pan.Hiding = PanReason.None;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => !row.Key.StartsWith("issue:hidhide", StringComparison.Ordinal)), "die Meldung verschwindet");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-26")]
    public async Task A_HidHide_that_answers_unexpectedly_is_reinstalled_from_its_row()
    {
        var steam = new FakeSteam { HidHideVersion = new Version(1, 4, 181, 0) };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        steam.HidHideReinstalled = () => arcadia.Pan.Hiding = PanReason.None;
        arcadia.Pan.Hiding = PanReason.HidHideUnsupported;

        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-interface"), "die Meldung „HidHide antwortet unerwartet“");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-interface"));
        Assert.Equal("HidHide antwortet unerwartet", row.Title);
        Assert.Equal($"HidHide {HidHideRelease.Version} neu installieren hilft", row.Subtitle);
        Assert.Equal("Neu installieren", row.ActionText);

        row.Activate();
        ConfirmHidHideInstall(arcadia, controller);
        await arcadia.WaitUntilAsync(() => steam.HidHideReinstalls == 1, "HidHide wird neu installiert");
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(r => r.Key != "issue:hidhide-interface"), "die Meldung verschwindet");
        Assert.Equal(0, steam.DriverInstalls);
        Assert.Null(arcadia.ControllerSupport.LastError);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T296-3")]
    public async Task While_Steam_runs_Neu_installieren_closes_Steam_after_asking_and_starts_it_again()
    {
        var steam = new FakeSteam { HidHideVersion = new Version(1, 4, 181, 0) };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        arcadia.Pan.Hiding = PanReason.HidHideUnsupported;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-interface"), "die Meldung „HidHide antwortet unerwartet“");
        steam.IsSteamRunning = true;

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-interface")).Activate();
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Contains("Die Neuinstallation von HidHide startet den Puck", controller.SteamStopDetail, StringComparison.Ordinal);
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.HidHideReinstalls == 1 && steam.IsSteamRunning && arcadia.ControllerSupport.BusyText is null, "HidHide neu installiert, Steam wieder da");

        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.PuckRestartsWhileSteamRuns);
        Assert.Equal(1, steam.SteamStops);
        Assert.Null(arcadia.ControllerSupport.LastError);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T296-1")]
    public async Task While_Steam_runs_a_missing_HidHide_is_installed_only_after_A_closes_Steam_and_B_installs_nothing()
    {
        var steam = new FakeSteam { HidHideVersion = null, IsSteamRunning = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");
        var hidHide = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing"));

        // [B] cancels: nothing is installed, Steam keeps running.
        hidHide.Activate();
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Contains("Die Treiber-Installation startet den Puck", controller.SteamStopDetail, StringComparison.Ordinal);
        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.True(arcadia.Main.IsSettingsOpen);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal((0, 0), (steam.DriverInstalls, steam.SteamStops));

        // [A] closes Steam, installs, and starts Steam again.
        hidHide.Activate();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && steam.IsSteamRunning && arcadia.ControllerSupport.BusyText is null, "installiert, Steam wieder da");

        Assert.Equal(0, steam.PuckRestartsWhileSteamRuns);
        Assert.Equal(1, steam.SteamStops);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
        Assert.Null(arcadia.ControllerSupport.LastError);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T296-2")]
    public async Task Without_HidHide_s_installer_the_driver_install_leaves_a_running_Steam_alone()
    {
        var steam = new FakeSteam { IsPanReady = false, IsSteamRunning = true };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:pan-missing"), "die Meldung „Wächterdienst Pan fehlt“");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:pan-missing")).Activate();
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && arcadia.ControllerSupport.BusyText is null, "Pan installiert");

        Assert.Equal(0, steam.SteamStops);
        Assert.True(steam.IsSteamRunning);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T296-4")]
    public async Task With_a_Steam_game_running_the_driver_install_names_the_game_and_B_closes_the_notice()
    {
        var steam = new FakeSteam { HidHideVersion = null, IsSteamRunning = true, SteamGame = "Hollow Knight" };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing")).Activate();
        Assert.Equal(SettingsPrompt.SteamGameRunning, controller.Prompt);
        Assert.Equal("Hollow Knight", controller.RunningGame);

        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal((0, 0), (steam.DriverInstalls, steam.SteamStops));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T296-4")]
    public async Task When_the_Steam_game_ends_the_driver_notice_turns_into_the_Steam_question()
    {
        var steam = new FakeSteam { HidHideVersion = null, IsSteamRunning = true, SteamGame = "Hollow Knight" };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-missing"), "die Meldung „HidHide fehlt“");

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-missing")).Activate();
        Assert.Equal(SettingsPrompt.SteamGameRunning, controller.Prompt);

        steam.SteamGame = null;
        await arcadia.WaitUntilAsync(() => controller.Prompt == SettingsPrompt.ConfirmSteamStop, "„Steam beenden?“ nach dem Spielende");
        Assert.Contains("Die Treiber-Installation startet den Puck", controller.SteamStopDetail, StringComparison.Ordinal);
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);
        Assert.Equal((0, 0), (steam.DriverInstalls, steam.SteamStops));

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.DriverInstalls == 1 && steam.IsSteamRunning && arcadia.ControllerSupport.BusyText is null, "installiert, Steam wieder da");
        Assert.Equal(0, steam.PuckRestartsWhileSteamRuns);
        Assert.Equal(1, steam.SteamStops);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-36")]
    public async Task Pan_s_notice_is_a_toast_while_Arcadia_is_in_front_and_the_overlay_s_while_a_game_is()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();

        arcadia.Pan.TearDown(PanReason.Hang);
        await arcadia.WaitUntilAsync(() => arcadia.OverlayNotices.Count == 1, "der Hinweis über dem Spiel");
        Assert.Equal(SteamControllerForGames.TornDownText(PanReason.Hang), arcadia.OverlayNotices[0]);
        Assert.Null(arcadia.Main.Toast.Text);

        await arcadia.TickSteamControllerAsync();
        arcadia.IsArcadiaInFront = true;
        arcadia.Pan.Lose("Pan antwortet nicht");
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == SteamControllerForGames.PanLostText, "der Toast in Arcadia");
        Assert.Single(arcadia.OverlayNotices);
    }
}
