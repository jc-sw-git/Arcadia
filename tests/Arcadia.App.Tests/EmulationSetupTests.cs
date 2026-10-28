using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
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
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => row.Key != "issue:hidhide-missing"), "die Meldung verschwindet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
        Assert.NotEqual(ProblemText, Switch(controller).Subtitle);
        Assert.Null(controller.Marker);
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
        await arcadia.WaitUntilAsync(() => controller.Rows.OfType<ActionRowViewModel>().Any(row => row.HasProblem), "der Fehler an einer Zeile");

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
        var steam = new FakeSteam { ReportsUsbip = true, IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Sisr.IsInstalled = false;
        steam.Sisr.LatestRelease = new Release("v1.0.0", new Version(1, 0, 0), []);
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
        await arcadia.WaitUntilAsync(() => Switch(controller).IsSwitch, "eingerichtet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.True(steam.Sisr.IsInstalled, "VIIPER kommt bis #214 mit SISR");
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.True(Switch(controller).IsOn);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-33")]
    public async Task A_declined_install_of_every_part_keeps_VIIPER_and_shows_its_error_at_the_first_part_left()
    {
        const string error = "Die Installation wurde in der Windows-Abfrage abgelehnt – noch einmal „Installieren“ wählen und die Abfrage bestätigen.";
        var steam = new FakeSteam { ReportsUsbip = true, IsUsbipReady = false, IsPanReady = false, HidHideVersion = null, InstallError = error };
        steam.Sisr.IsInstalled = false;
        steam.Sisr.LatestRelease = new Release("v1.0.0", new Version(1, 0, 0), []);
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        await arcadia.WaitUntilAsync(() => Switch(controller).IsInstall, "„Treiber installieren“ an der Einstellung");

        Switch(controller).Activate();
        await arcadia.WaitUntilAsync(() => controller.Rows.OfType<ActionRowViewModel>().Any(row => row.HasProblem), "der Fehler an einer Zeile");

        // VIIPER needs no Windows prompt and came before it: the rest is three parts with one error, at the first.
        Assert.True(steam.Sisr.IsInstalled);
        Assert.Equal(["issue:usbip-missing", "issue:pan-missing", "issue:hidhide-missing"], controller.Rows.Where(row => row.Key.StartsWith("issue:", StringComparison.Ordinal)).Select(row => row.Key));
        var failed = Assert.Single(controller.Rows.OfType<ActionRowViewModel>(), row => row.HasProblem);
        Assert.Equal("issue:usbip-missing", failed.Key);
        Assert.Equal(error, failed.Problem);
        var pan = Switch(controller);
        Assert.False(pan.HasProblem);
        Assert.Equal(ProblemText, pan.Subtitle);
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
        await arcadia.WaitUntilAsync(() => controller.Rows.All(r => r.Key != "issue:hidhide-unsupported"), "die Meldung verschwindet");
        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-41")]
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
        Assert.Equal("Windows entfernt HidHide beim nächsten Neustart", row.Subtitle);
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
        await arcadia.WaitUntilAsync(() => steam.HidHideReinstalls == 1, "HidHide wird neu installiert");
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(r => r.Key != "issue:hidhide-interface"), "die Meldung verschwindet");
        Assert.Equal(0, steam.DriverInstalls);
        Assert.Null(arcadia.ControllerSupport.LastError);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-26")]
    public async Task While_Steam_runs_Neu_installieren_asks_to_close_Steam_and_leaves_the_controller_alone()
    {
        var steam = new FakeSteam { HidHideVersion = new Version(1, 4, 181, 0) };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;
        arcadia.Pan.Hiding = PanReason.HidHideUnsupported;
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-interface"), "die Meldung „HidHide antwortet unerwartet“");
        steam.IsSteamRunning = true;

        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-interface")).Activate();

        await arcadia.WaitUntilAsync(() => arcadia.ControllerSupport.LastError == ControllerSupportService.HidHideReinstallWhileSteamRunsText, "der Hinweis, Steam zu beenden");
        Assert.Equal(0, steam.HidHideReinstalls);
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
