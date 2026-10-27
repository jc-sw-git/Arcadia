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

    private static SisrRowViewModel Switch(ControllerSettingsViewModel controller) =>
        Assert.IsType<SisrRowViewModel>(controller.Rows.Single(row => row.Key == "sisr"));

    private static IEnumerable<string> StatusTexts(SisrRowViewModel row) =>
        row.Info.OfType<InfoSection>().SelectMany(section => section.Blocks).OfType<InfoStatus>().Select(status => status.Text);

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
        var sisr = Switch(controller);
        Assert.True(sisr.IsDimmed);
        Assert.False(sisr.IsSwitch);
        Assert.False(sisr.IsInstall);
        Assert.Equal("Aus – es fehlt: HidHide", sisr.Subtitle);
        Assert.Contains(StatusTexts(sisr), text => text.Contains("es fehlt: HidHide") && text.Contains("Controller-Emulation aus"));

        hidHide.Activate();
        await arcadia.WaitUntilAsync(() => controller.Rows.All(row => row.Key != "issue:hidhide-missing"), "die Meldung verschwindet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.Equal(HidHideRelease.Version, steam.HidHideVersion);
        Assert.Equal("Nicht mehr nötig – Arcadia bringt den Steam Controller selbst in Spiele.", Switch(controller).Subtitle);
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
        var sisr = Switch(controller);
        Assert.True(sisr.IsDimmed);
        Assert.False(sisr.HasProblem);
        Assert.Equal("Aus – es fehlt: HidHide", sisr.Subtitle);
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
        var sisr = Switch(controller);
        Assert.Equal("Treiber installieren", sisr.ActivateText);
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.Contains(StatusTexts(sisr), text => text.Contains("usbip-Treiber, VIIPER, Wächterdienst Pan, HidHide"));
        Assert.Equal(HealthSeverity.Warning, controller.Marker);

        sisr.Activate();
        await arcadia.WaitUntilAsync(() => Switch(controller).IsDimmed, "eingerichtet");

        Assert.Equal(1, steam.DriverInstalls);
        Assert.True(steam.Sisr.IsInstalled, "VIIPER kommt bis #214 mit SISR");
        Assert.DoesNotContain(controller.Rows, row => row.Key.StartsWith("issue:", StringComparison.Ordinal));
        Assert.Equal("Nicht mehr nötig – Arcadia bringt den Steam Controller selbst in Spiele.", Switch(controller).Subtitle);
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
        var sisr = Switch(controller);
        Assert.True(sisr.IsDimmed);
        Assert.False(sisr.HasProblem);
        Assert.Equal("Aus – es fehlt: usbip-Treiber, Wächterdienst Pan, HidHide", sisr.Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-34")]
    public async Task An_old_HidHide_gets_its_row_with_Aktualisieren()
    {
        var steam = new FakeSteam { HidHideVersion = new Version(1, 1, 0, 0) };
        var (arcadia, controller) = await OpenControllerSettingsAsync(steam);
        using var _ = arcadia;

        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:hidhide-unsupported"), "die Meldung zur HidHide-Version");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "issue:hidhide-unsupported"));
        Assert.Equal("HidHide-Version wird nicht unterstützt", row.Title);
        Assert.Equal("Aktualisieren", row.ActionText);
        Assert.Contains("Installiert ist HidHide 1.1.0", row.Info.OfType<InfoText>().Single().Text);

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
        Assert.Equal("Windows entfernt HidHide beim nächsten Neustart.", row.Subtitle);
        Assert.Equal(string.Empty, row.ActionText);
        Assert.Equal("Aus – es fehlt: HidHide", Switch(controller).Subtitle);
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
        Assert.Equal("Bis zum Neustart kann der Steam Controller in Spielen aussetzen.", row.Subtitle);
        Assert.Equal(string.Empty, row.ActionText);
        Assert.DoesNotContain("SISR", row.Info.OfType<InfoText>().Single().Text);
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
