using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Arcadia.Tests.Pan;
using static Arcadia.ControllerSupport.DriverInstallStepKind;

namespace Arcadia.Core.Tests;

/// <summary>
/// A driver install from the settings with the Windows prompt first and its steps shown (#413): which steps a run has,
/// their order, the bar, Pan's report (display only), and how failures, timeouts and Pan's own end finish the run.
/// </summary>
public sealed partial class ControllerSupportServiceTests
{
    private readonly List<string> _restarts = [];

    private ControllerSupportService CreateForInstall(IViiperInstallation? viiper = null)
    {
        var service = Create(viiper);
        service.RestartArcadia = reason => _restarts.Add(reason);
        return service;
    }

    private static IReadOnlyList<DriverInstallStepKind> Kinds(ControllerSupportService service) => [.. service.Installation!.Steps.Select(step => step.Kind)];

    private static DriverInstallStepState State(ControllerSupportService service, DriverInstallStepKind kind) => service.Installation!.StateOf(kind);

    private static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, $"not reached in 10 s: {what}");
            await Task.Delay(5);
        }
    }

    [Fact]
    [Trait("TestCase", "T413-1")]
    [Trait("TestCase", "T413-10")]
    public async Task A_full_install_asks_first_then_runs_every_step_in_order_and_restarts_Arcadia()
    {
        var viiper = new NotingViiper(_platform.Pan);
        var service = CreateForInstall(viiper);
        _platform.IsSteamRunning = true;
        _platform.IsUsbipReady = false;
        _platform.IsPanReady = false;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Null(service.LastError);
        Assert.Equal([Prompt, DriverInstallStepKind.Viiper, SteamClose, UsbipDownload, HidHideDownload, UsbipInstall, HidHideInstall, DriverInstallStepKind.PanSetup, SteamStart, ArcadiaRestart], Kinds(service));
        // The prompt is the first thing that happens; nothing is downloaded, closed or set up before it (D7).
        Assert.Equal(["prompt", "viiper", "steam close", "download usbip-win2", "download HidHide", "go", "exit 0", "steam start"], _platform.Pan.Calls);
        Assert.All(service.Installation!.Steps.SkipLast(1), step => Assert.Equal(DriverInstallStepState.Done, step.State));
        Assert.Equal(DriverInstallStepState.Running, State(service, ArcadiaRestart));
        Assert.True(service.Installation.IsOver);
        Assert.True(service.Installation.ShowsUsbHint);
        Assert.Single(_restarts);
        // Only the restart is left: it ends with Arcadia, so the bar stops one step short of full.
        Assert.InRange(service.Installation.Value, 0.5, 0.99);
    }

    [Fact]
    [Trait("TestCase", "T413-2")]
    public async Task Without_Steam_running_there_is_no_Steam_step_and_Steam_isn_t_started()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = false;
        _platform.IsUsbipReady = false;
        _platform.IsPanReady = false;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal([Prompt, UsbipDownload, HidHideDownload, UsbipInstall, HidHideInstall, DriverInstallStepKind.PanSetup, ArcadiaRestart], Kinds(service));
        Assert.Equal((0, 0), (_platform.SteamStops, _platform.SteamStarts));
    }

    [Fact]
    [Trait("TestCase", "T413-3")]
    public async Task Only_Pan_missing_lists_the_prompt_and_Pan_and_no_USB_hint()
    {
        var service = CreateForInstall();
        _platform.IsPanReady = false;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal([Prompt, DriverInstallStepKind.PanSetup], Kinds(service));
        Assert.False(service.Installation!.ShowsUsbHint);
        Assert.False(service.Installation.Plan.Restarts);
        Assert.Empty(_restarts);
        Assert.Equal((0, 0), (_platform.SteamStops, _platform.SteamStarts));
    }

    [Fact]
    [Trait("TestCase", "T413-4")]
    public async Task Only_HidHide_missing_with_Steam_running_closes_and_starts_Steam_and_restarts_Arcadia()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal([Prompt, SteamClose, HidHideDownload, HidHideInstall, DriverInstallStepKind.PanSetup, SteamStart, ArcadiaRestart], Kinds(service));
        Assert.False(service.Installation!.ShowsUsbHint);
        Assert.False(service.InstallsUsbip(HealthFix.InstallDrivers));
    }

    [Fact]
    [Trait("TestCase", "T413-5")]
    public async Task Only_usbip_missing_shows_the_USB_hint_and_no_Steam_or_restart_step()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.IsUsbipReady = false;
        Assert.True(service.InstallsUsbip(HealthFix.InstallDrivers));

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal([Prompt, UsbipDownload, UsbipInstall, DriverInstallStepKind.PanSetup], Kinds(service));
        Assert.True(service.Installation!.ShowsUsbHint);
        Assert.Empty(_restarts);
    }

    [Fact]
    [Trait("TestCase", "T413-6")]
    public async Task Only_VIIPER_missing_shows_its_one_step_and_asks_Windows_nothing()
    {
        var service = CreateForInstall(new NotingViiper(_platform.Pan));

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal([DriverInstallStepKind.Viiper], Kinds(service));
        Assert.Equal(["viiper"], _platform.Pan.Calls);
        Assert.Equal(DriverInstallStepState.Done, State(service, DriverInstallStepKind.Viiper));
    }

    [Fact]
    [Trait("TestCase", "T413-7")]
    public async Task Neu_installieren_shows_HidHide_s_steps_with_the_prompt_first()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = PinnedHidHideDriver;

        await service.ApplyFixAsync(HealthFix.ReinstallHidHide);

        Assert.Equal([Prompt, SteamClose, HidHideDownload, HidHideInstall, DriverInstallStepKind.PanSetup, SteamStart, ArcadiaRestart], Kinds(service));
        Assert.Equal("prompt", _platform.Pan.Calls[0]);
    }

    [Fact]
    [Trait("TestCase", "T413-8")]
    public async Task Pan_s_update_and_the_restore_show_no_progress()
    {
        var service = CreateForInstall();
        _platform.PanCompatibility = PanCompatibility.UpdateRequired;

        await service.UpdatePanAsync();
        await service.RestoreControllersAsync();

        Assert.Null(service.Installation);
        Assert.Equal(0, _platform.Pan.Prompts);
    }

    [Theory]
    [Trait("TestCase", "T413-13")]
    [InlineData(3, PanStepState.Done, true)]
    [InlineData(0, PanStepState.Failed, false)]
    public async Task Pan_s_report_never_decides_how_the_install_ends(int exitCode, PanStepState reported, bool fails)
    {
        var service = CreateForInstall();
        _platform.IsUsbipReady = false;
        _platform.Pan.ExitCode = exitCode;
        _platform.Pan.FinalReport = new Dictionary<PanInstallStep, PanStepState> { [PanInstallStep.Usbip] = reported, [PanInstallStep.Pan] = reported };

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(fails, service.LastError is not null);
        Assert.Equal(fails, service.Installation!.Failed);
        Assert.Equal(!fails, _platform.IsUsbipReady);
    }

    [Theory]
    [Trait("TestCase", "T413-14")]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Without_a_report_Pan_s_steps_show_as_one_that_ends_with_Pan_s_exit(int exitCode)
    {
        var service = CreateForInstall();
        _platform.IsUsbipReady = false;
        _platform.HidHideVersion = null;
        _platform.IsSteamRunning = false;
        _platform.Pan.WritesNoReport = true;
        _platform.Pan.ExitCode = exitCode;
        _platform.Pan.Running = new();

        var install = service.ApplyFixAsync(HealthFix.InstallDrivers);
        await WaitForAsync(() => _platform.Pan.HandOvers == 1, "die Übergabe");
        await WaitForAsync(() => { _time.Advance(DriverInstallation.ReportInterval); return service.Installation!.Has(PanFallback); }, "der eine Schritt");
        Assert.Equal([Prompt, UsbipDownload, HidHideDownload, PanFallback, ArcadiaRestart], Kinds(service));
        Assert.Equal(DriverInstallStepState.Running, State(service, PanFallback));
        _platform.Pan.Running.SetResult();
        await install;

        Assert.Equal(exitCode == 0 ? DriverInstallStepState.Done : DriverInstallStepState.Failed, State(service, PanFallback));
        Assert.True(service.Installation!.IsOver);
    }

    [Fact]
    [Trait("TestCase", "T413-15")]
    public async Task A_failed_usbip_install_turns_its_step_red_leaves_the_later_ones_pending_and_starts_Steam()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.IsUsbipReady = false;
        _platform.HidHideVersion = null;
        _platform.Pan.ExitCode = 3;
        _platform.Pan.ReportFailsAt = PanInstallStep.Usbip;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Contains("pan.log", service.LastError);
        Assert.Equal(service.LastError, service.Installation!.Error);
        Assert.Equal(DriverInstallStepState.Failed, State(service, UsbipInstall));
        Assert.Equal(DriverInstallStepState.Pending, State(service, HidHideInstall));
        Assert.Equal(DriverInstallStepState.Pending, State(service, DriverInstallStepKind.PanSetup));
        Assert.Equal(DriverInstallStepState.Done, State(service, SteamStart));
        Assert.Equal(DriverInstallStepState.Pending, State(service, ArcadiaRestart));
        Assert.Empty(_restarts);
        Assert.True(_platform.IsSteamRunning);
    }

    [Fact]
    [Trait("TestCase", "T413-15")]
    public async Task A_failed_HidHide_turns_only_its_step_red_while_Pan_is_set_up()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;
        _platform.Pan.ExitCode = PanExitCodes.HidHideFailed;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(PanExitCodes.ErrorFor(PanExitCodes.HidHideFailed, "--install").Message, service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, HidHideInstall));
        Assert.Equal(DriverInstallStepState.Done, State(service, DriverInstallStepKind.PanSetup));
        Assert.Equal(DriverInstallStepState.Done, State(service, SteamStart));
        Assert.Empty(_restarts);
    }

    [Theory]
    [Trait("TestCase", "T413-16")]
    [InlineData("network")]
    [InlineData("hash")]
    public async Task A_failed_download_after_the_prompt_ends_Pan_first_then_starts_Steam(string failure)
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;
        _platform.Pan.HidHideDownloadError = failure == "network"
            ? new HttpRequestException("Netzwerk nicht erreichbar")
            : new InvalidOperationException("Prüfsumme von HidHide.exe stimmt nicht – Download verworfen.");
        _platform.UsbipInstallError = _platform.Pan.HidHideDownloadError;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(_platform.Pan.HidHideDownloadError.Message, service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, HidHideDownload));
        Assert.Equal(["prompt", "steam close", "download HidHide", "abort", "exit 9", "steam start"], _platform.Pan.Calls);
        Assert.Equal(0, _platform.Pan.HandOvers);
        Assert.Equal(DriverInstallStepState.Done, State(service, SteamStart));
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    public async Task A_Steam_that_won_t_close_after_the_prompt_ends_Pan_and_installs_nothing()
    {
        var service = CreateForInstall();
        _platform.IsSteamRunning = true;
        _platform.SteamRefusesToExit = true;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(ControllerSupportService.SteamWontCloseText, service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, SteamClose));
        Assert.Equal(["prompt", "abort", "exit 9"], _platform.Pan.Calls);
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    public async Task A_Steam_game_started_during_the_VIIPER_download_keeps_Steam_open_and_ends_the_run()
    {
        var viiper = new NotingViiper(_platform.Pan) { During = () => _platform.SteamGame = "Hollow Knight" };
        var service = CreateForInstall(viiper);
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(ControllerSupportService.DriverInstallBlocker("Hollow Knight"), service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, SteamClose));
        Assert.Equal(0, _platform.SteamStops);
        Assert.True(_platform.IsSteamRunning);
        Assert.Equal(["prompt", "viiper", "abort", "exit 9"], _platform.Pan.Calls);
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    public async Task A_firmware_update_begun_during_the_VIIPER_download_ends_the_run_before_Steam_closes()
    {
        var viiper = new NotingViiper(_platform.Pan) { During = () => _platform.FirmwareUpdateRuns = true };
        var service = CreateForInstall(viiper);
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(ControllerSupportService.FirmwareUpdateBlocksInstallText, service.LastError);
        Assert.Equal(0, _platform.SteamStops);
        Assert.True(_platform.Pan.Aborted);
    }

    [Theory]
    [Trait("TestCase", "T413-16")]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task A_Pan_that_ends_before_the_hand_over_ends_the_run_before_VIIPER_and_Steam(int exitCode)
    {
        var viiper = new NotingViiper(_platform.Pan);
        var service = CreateForInstall(viiper);
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;
        _platform.Pan.EndsBeforeConnect = exitCode;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(PanExitCodes.ErrorFor(exitCode, "--install").Message, service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, Prompt));
        Assert.Equal(["prompt", $"exit {exitCode}"], _platform.Pan.Calls);
        Assert.Equal(0, _platform.SteamStops);
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    public async Task An_exit_9_Arcadia_didn_t_cause_has_its_own_message()
    {
        var service = CreateForInstall();
        _platform.IsUsbipReady = false;
        _platform.Pan.EndsWhileWaiting = InstallHandOver.EndedExitCode;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(PanExitCodes.HandOverEndedText, service.LastError);
        Assert.DoesNotContain("Fehler 9", service.LastError, StringComparison.Ordinal);
        Assert.False(_platform.Pan.Aborted);
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    public async Task A_cancelled_run_before_the_hand_over_ends_Pan_too()
    {
        using var cancel = new CancellationTokenSource();
        var viiper = new NotingViiper(_platform.Pan) { During = cancel.Cancel };
        var service = CreateForInstall(viiper);
        _platform.IsUsbipReady = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyFixAsync(HealthFix.InstallDrivers, cancel.Token));

        Assert.True(_platform.Pan.Aborted);
        Assert.Equal(0, _platform.Pan.HandOvers);
    }

    [Fact]
    [Trait("TestCase", "T413-16")]
    [Trait("TestCase", "T413-17")]
    public async Task A_download_that_hangs_runs_into_its_own_timeout_with_a_message_naming_it()
    {
        var service = CreateForInstall();
        _platform.IsUsbipReady = false;
        _platform.Pan.DownloadsHang = true;

        var install = service.ApplyFixAsync(HealthFix.InstallDrivers);
        await WaitForAsync(() => _platform.Pan.Calls.Contains("download usbip-win2"), "der Download");
        var value = service.Installation!.Value;
        _time.Advance(DriverInstallation.UsbipDownloadTimeout);
        await install;

        Assert.Contains("usbip-win2-Download", service.LastError);
        Assert.Equal(DriverInstallStepState.Failed, State(service, UsbipDownload));
        Assert.Equal(DriverInstallStepState.Pending, State(service, UsbipInstall));
        Assert.Equal(value, service.Installation.Value);
        Assert.True(_platform.Pan.Aborted);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public void Arcadia_s_steps_up_to_the_hand_over_fit_into_Pan_s_wait_with_room_to_spare()
    {
        var margin = TimeSpan.FromMinutes(5);

        Assert.True(DriverInstallation.UpToHandOver + margin <= PanTimeouts.HandOver,
            $"{DriverInstallation.UpToHandOver} + {margin} must stay within Pan's {PanTimeouts.HandOver}");
    }

    [Fact]
    [Trait("TestCase", "T413-18")]
    public void A_new_Arcadia_shows_no_install_from_before()
    {
        Assert.Null(CreateForInstall().Installation);
    }

    [Fact]
    [Trait("TestCase", "T413-11")]
    public async Task Downloads_move_the_bar_by_their_bytes_and_it_never_goes_back()
    {
        var service = CreateForInstall();
        _platform.IsUsbipReady = false;
        _platform.HidHideVersion = null;
        _platform.IsSteamRunning = false;
        var values = new List<double>();
        _platform.Pan.OnDownload = () => values.Add(service.Installation!.Value);
        _platform.Pan.Running = new();

        var install = service.ApplyFixAsync(HealthFix.InstallDrivers);
        await WaitForAsync(() => _platform.Pan.HandOvers == 1, "die Übergabe");
        values.Add(service.Installation!.Value);
        _platform.Pan.Report(PanInstallStep.Usbip, PanStepState.Done);
        _platform.Pan.Report(PanInstallStep.HidHide, PanStepState.Running);
        await WaitForAsync(() => { _time.Advance(DriverInstallation.ReportInterval); return State(service, UsbipInstall) == DriverInstallStepState.Done; }, "usbip-win2 ✓");
        values.Add(service.Installation.Value);
        _platform.Pan.Running.SetResult();
        await install;
        values.Add(service.Installation.Value);

        Assert.Equal(values.Order(), values);
        Assert.True(values[^1] > values[^2], "Pan's end moves the bar on");
    }

    /// <summary>VIIPER's download as the run sees it, noted in Pan's list of calls so the order shows.</summary>
    private sealed class NotingViiper(FakeElevatedPan pan) : IViiperInstallation
    {
        public bool IsInstalled { get; private set; }

        public Action? During { get; init; }

        public Task InstallAsync(HttpClient http, CancellationToken cancellationToken)
        {
            pan.Note("viiper");
            During?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            IsInstalled = true;
            return Task.CompletedTask;
        }
    }
}
