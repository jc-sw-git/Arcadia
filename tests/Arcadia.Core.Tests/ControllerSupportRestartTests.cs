using Arcadia.ControllerSupport;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>
/// #382: one restart row for every driver change that waits for the Windows restart, Pan paused while a removal waits,
/// "Windows neu starten", HidHide not asked about while it is missing, and the setup's log line.
/// </summary>
public sealed partial class ControllerSupportServiceTests
{
    [Theory]
    [Trait("TestCase", "T382-8")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Every_driver_removal_waiting_for_the_restart_shows_one_restart_row_and_pauses_Pan(bool hidHide, bool usbip)
    {
        var service = Create();
        _platform.HidHideRemovalPending = hidHide;
        _platform.HidHideVersion = hidHide ? null : HidHideRelease.Version;
        _platform.UsbipRemovalPending = usbip;
        _platform.IsUsbipReady = !usbip;

        await service.TickAsync();

        // No part row offers "Installieren" and none of the old restart rows comes back.
        var row = Assert.Single(service.Issues);
        Assert.Same(EmulationParts.WindowsRestartRequired, row);
        Assert.Equal(HealthFix.RestartWindows, row.Fix);
        Assert.Equal("Windows neu starten", row.FixLabel);
        Assert.Equal("Starte Windows neu, danach kannst du die fehlenden Teile installieren.", row.ShortText);
        Assert.True(service.PausesForWindowsRestart);
        Assert.Equal(PendingRestart.Required, service.PendingRestart);
    }

    [Fact]
    [Trait("TestCase", "T382-10")]
    public async Task A_fresh_usbip_win2_only_recommends_the_restart_and_Pan_keeps_running()
    {
        var service = Create();
        _platform.UsbipFresh = true;
        _platform.HidHideVersion = null;

        await service.TickAsync();

        Assert.Equal(["hidhide-missing", EmulationParts.WindowsRestartId], service.Issues.Select(issue => issue.Id));
        Assert.Same(EmulationParts.WindowsRestartRecommended, service.Issues[1]);
        Assert.Equal("Starte Windows neu, damit der neue Treiber ganz aktiv ist.", service.Issues[1].ShortText);
        Assert.False(service.PausesForWindowsRestart);
    }

    [Fact]
    [Trait("TestCase", "T382-11")]
    public async Task A_removal_wins_over_a_fresh_usbip_win2()
    {
        var service = Create();
        _platform.UsbipFresh = true;
        _platform.HidHideRemovalPending = true;

        await service.TickAsync();

        Assert.Same(EmulationParts.WindowsRestartRequired, Assert.Single(service.Issues));
        Assert.True(service.PausesForWindowsRestart);
    }

    [Fact]
    [Trait("TestCase", "T382-15")]
    [Trait("TestCase", "T382-17")]
    public async Task A_removal_outside_Arcadia_pauses_Pan_also_with_the_settings_closed_and_its_end_lets_Pan_run_again()
    {
        var service = Create();
        await service.TickAsync();
        Assert.False(service.PausesForWindowsRestart);

        // The settings are closed: the parts aren't checked again, the pending restart is (Q1).
        _platform.HidHideRemovalPending = true;
        await service.TickAsync();
        Assert.True(service.PausesForWindowsRestart);
        Assert.Same(EmulationParts.WindowsRestartRequired, Assert.Single(service.Issues));

        _platform.HidHideRemovalPending = false;
        await service.TickAsync();
        Assert.False(service.PausesForWindowsRestart);
        Assert.Empty(service.Issues);
    }

    [Fact]
    [Trait("TestCase", "T382-28")]
    public async Task A_failing_pending_restart_check_counts_as_none_and_is_logged()
    {
        var service = Create();
        _platform.PendingRestartError = new UnauthorizedAccessException("Registry gesperrt");

        await service.TickAsync();

        Assert.Empty(service.Issues);
        Assert.False(service.PausesForWindowsRestart);
        Assert.Contains(_log, line => line.StartsWith("Nicht geprüft, ob ein Windows-Neustart aussteht", StringComparison.Ordinal) && line.Contains("Registry gesperrt", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T382-2")]
    public async Task HidHide_counts_as_usable_only_while_a_supported_version_is_installed()
    {
        var service = Create();
        await service.TickAsync();
        Assert.True(service.HidHideUsable);

        _platform.HidHideVersion = null;
        await service.RefreshAsync();
        Assert.False(service.HidHideUsable);

        _platform.HidHideVersion = new Version(1, 2, 0);
        await service.RefreshAsync();
        Assert.Equal("hidhide-unsupported", Assert.Single(service.Issues).Id);
        Assert.False(service.HidHideUsable);
    }

    [Fact]
    [Trait("TestCase", "T382-2")]
    public async Task Pan_is_not_asked_about_hiding_while_a_removal_waits_for_the_restart()
    {
        var service = Create();
        var pan = new Arcadia.Tests.Pads.FakeEmulationGuardian { Hiding = PanReason.HidHideMissing };
        service.HidingCheck = pan;
        service.WatchSetup = true;
        _platform.HidHideRemovalPending = true;

        await service.TickAsync();

        Assert.Equal(0, pan.HidingChecks);
        Assert.DoesNotContain(service.Issues, issue => issue.Id == "hidhide-inactive");
    }

    [Fact]
    [Trait("TestCase", "T382-22")]
    [Trait("TestCase", "T382-24")]
    public async Task Windows_neu_starten_asks_Windows_once_and_a_refusal_is_reported_and_logged()
    {
        var service = Create();
        _platform.WindowsRestartError = new InvalidOperationException("shutdown.exe endete mit 1190");

        Assert.False(await service.RestartWindowsAsync());
        Assert.Contains(_log, line => line == "Windows-Neustart abgelehnt: shutdown.exe endete mit 1190");

        // Refused, the row stays and the player can try again; accepted, a second press asks for nothing more.
        _platform.WindowsRestartError = null;
        Assert.True(await service.RestartWindowsAsync());
        Assert.True(await service.RestartWindowsAsync());
        Assert.Equal(2, _platform.WindowsRestarts);
    }

    [Fact]
    [Trait("TestCase", "T382-22")]
    public async Task A_restart_Windows_doesn_t_answer_in_time_is_a_refusal()
    {
        var service = Create();
        _platform.WindowsRestartError = new TimeoutException("shutdown.exe hat nicht innerhalb von 15 Sekunden geantwortet");

        Assert.False(await service.RestartWindowsAsync());
    }

    [Theory]
    [Trait("TestCase", "T382-29")]
    [InlineData(false, false, false, "Pan")]
    [InlineData(false, true, false, "HidHide und Pan")]
    [InlineData(true, true, false, "usbip-win2, HidHide und Pan")]
    [InlineData(false, true, true, "HidHide")]
    [InlineData(true, false, true, "usbip-win2")]
    public void The_setup_log_line_names_exactly_what_the_run_installed(bool usbip, bool hidHide, bool panWasReady, string expected)
    {
        var plan = new DriverInstallPlan(Prompt: true, Viiper: false, SteamCloses: false, Usbip: usbip, HidHide: hidHide, HidHideRepair: false, Restarts: hidHide);

        Assert.Equal(expected, ControllerSupportService.InstalledParts(plan, panWasReady));
    }

    [Fact]
    [Trait("TestCase", "T382-29")]
    [Trait("TestCase", "T382-5")]
    public async Task Installing_HidHide_over_a_ready_Pan_logs_only_HidHide()
    {
        var service = Create();
        _platform.IsSteamRunning = false;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Null(service.LastError);
        Assert.Contains(_log, line => line == "Treiber eingerichtet (HidHide)");
        Assert.DoesNotContain(_log, line => line.StartsWith("Treiber eingerichtet (usbip", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T382-29")]
    public async Task A_failed_run_writes_no_setup_log_line()
    {
        var service = Create();
        _platform.IsSteamRunning = false;
        _platform.HidHideVersion = null;
        _platform.Pan.ExitCode = 3;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.NotNull(service.LastError);
        Assert.DoesNotContain(_log, line => line.StartsWith("Treiber eingerichtet", StringComparison.Ordinal));
    }
}
