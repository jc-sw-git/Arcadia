using System.ComponentModel;
using System.Net;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// Start, stop, pause and the periodic check of SISR, against a fake SISR process, a fake SISR API
/// and fake time: whatever runs in parallel, SISR never runs while the support is off or paused.
/// </summary>
public sealed class ControllerSupportServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeSisr _sisr;
    private readonly FakeSisrApi _api = new();
    private readonly FakePlatform _platform = new();
    private readonly List<string> _log = [];
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-sisr-").FullName;
    private readonly HttpClient _http;

    public ControllerSupportServiceTests()
    {
        _http = new HttpClient(_api);
        _sisr = new FakeSisr(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private ControllerSupportService Create(bool enabled = true)
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings { SteamControllerSupport = enabled });
        return new ControllerSupportService(store, _sisr, _http, _time, _platform) { Log = Log };
    }

    private void Log(string line)
    {
        lock (_log)
        {
            _log.Add(line);
        }
    }

    private void AssertNoUnexpectedTransition()
    {
        lock (_log)
        {
            Assert.DoesNotContain(_log, line => line.Contains("unerwarteter Zustandswechsel"));
        }
    }

    private Game SteamGame()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_directory, "game")).FullName;
        return new Game
        {
            Id = "1",
            LauncherId = "steam",
            Title = "Steam-Spiel",
            Launch = new LaunchTarget.ProtocolUri("steam://rungameid/1"),
            IsInstalled = true,
            InstallDirectory = directory,
        };
    }

    /// <summary>Advances fake time until <paramref name="done"/> holds (bounded, so a bug can't hang the test).</summary>
    private async Task AdvanceUntil(Func<bool> done)
    {
        for (var i = 0; i < 1_000 && !done(); i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        Assert.True(done());
    }

    [Fact]
    public async Task A_tick_starts_a_stopped_SISR()
    {
        var service = Create();

        await service.TickAsync();

        Assert.Equal(1, _sisr.Starts);
        Assert.Equal(SisrState.Starting, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Switching_on_with_Steam_closed_starts_Steam_before_SISR()
    {
        var service = Create(enabled: false);
        _platform.IsSteamRunning = false;

        Assert.True(await service.SetEnabledAsync(true));

        Assert.Equal(1, _platform.SteamStarts);
        Assert.Equal(1, _sisr.Starts);
        Assert.True(service.Enabled);
    }

    [Fact]
    public async Task If_Steam_does_not_start_within_a_minute_the_setting_is_off_again()
    {
        var service = Create(enabled: false);
        _platform.IsSteamRunning = false;
        _platform.SteamWontStart = true;

        var on = service.SetEnabledAsync(true);
        await AdvanceUntil(() => on.IsCompleted);

        Assert.False(await on);
        Assert.False(service.Enabled);
        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(SisrState.Off, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Without_Steam_a_tick_neither_starts_SISR_nor_counts_a_crash()
    {
        var service = Create();
        _platform.IsSteamRunning = false;

        for (var i = 0; i < ControllerSupportService.MaxRestartsPerWindow + 1; i++)
        {
            await service.TickAsync();
        }

        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(0, _platform.SteamStarts);
        Assert.Equal(SisrState.Stopped, service.State);

        _platform.IsSteamRunning = true;
        await service.TickAsync();
        Assert.Equal(1, _sisr.Starts);
    }

    [Fact]
    public async Task Closing_Steam_stops_SISR_and_switching_off_then_leaves_Steam_closed()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        Assert.Equal(SisrState.Running, service.State);

        _platform.IsSteamRunning = false;
        await service.WatchAsync();
        Assert.False(_sisr.Running);
        Assert.Equal(1, _sisr.Kills);
        Assert.Equal(0, _sisr.Stops);
        Assert.Equal(SisrState.Stopped, service.State);

        await service.SetEnabledAsync(false);

        Assert.Equal(0, _platform.SteamStarts);
        Assert.Equal(0, _platform.LayoutResets);
        Assert.Equal(SisrState.Off, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task After_SISR_is_killed_leftover_VIIPER_processes_and_devices_are_cleaned_up_and_logged()
    {
        var log = new List<string>();
        var service = Create();
        service.Log = log.Add;
        _sisr.Running = true;
        await service.TickAsync();

        _sisr.ViiperLeftovers = 1;
        _platform.IsSteamRunning = false;
        await service.WatchAsync();

        Assert.Equal(1, _sisr.ViiperCleanUps);
        Assert.Contains(log, line => line.Contains("1 übrig gebliebene VIIPER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stopping_SISR_in_the_background_counts_as_settling()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        Assert.False(service.IsSettling);

        var settlingWhileStopping = false;
        service.Changed += (_, _) => settlingWhileStopping |= service.State == SisrState.Stopping && service.IsSettling;
        _platform.IsSteamRunning = false;
        await service.WatchAsync();

        Assert.True(settlingWhileStopping);
        Assert.False(service.IsSettling);
    }

    [Fact]
    public async Task A_Steam_that_hangs_before_its_login_completes_does_not_lock_the_settings()
    {
        var service = Create();
        _platform.SteamLoginHangs = true;

        await service.TickAsync();

        Assert.Equal(0, _sisr.Starts);
        Assert.False(service.IsSettling);
    }

    [Fact]
    public async Task Switching_off_also_cleans_up_VIIPER()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();

        await service.SetEnabledAsync(false);

        Assert.Equal(1, _sisr.Stops);
        Assert.Equal(1, _sisr.ViiperCleanUps);
    }

    [Fact]
    public async Task While_Steam_shuts_down_SISR_is_killed_so_its_cleanup_cannot_restart_Steam()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();

        _platform.SteamNotReady = true;
        await service.WatchAsync();

        Assert.Equal(1, _sisr.Kills);
        Assert.Equal(0, _sisr.Stops);
        Assert.Equal(SisrState.Stopped, service.State);
    }

    [Fact]
    public async Task SISR_held_for_Steam_starts_again_with_Steam_even_after_earlier_crashes()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();

        // Use up the crash budget.
        for (var i = 0; i < ControllerSupportService.MaxRestartsPerWindow; i++)
        {
            _sisr.Running = false;
            await service.TickAsync();
            _sisr.Running = true;
        }

        _platform.IsSteamRunning = false;
        await service.WatchAsync();
        _platform.IsSteamRunning = true;
        var starts = _sisr.Starts;
        await service.TickAsync();

        Assert.Equal(starts + 1, _sisr.Starts);
        Assert.NotEqual(SisrState.Failed, service.State);
    }

    [Fact]
    public async Task Install_adds_the_marker_while_Steam_is_closed_and_SISR_skips_its_setup()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;

        await service.InstallAsync();

        Assert.Equal(1, _platform.MarkerChecks);
        Assert.Contains(_log, line => line.Contains("„SISR Marker“ vorhanden"));
        Assert.True(_sisr.SetupMarkedDone);
    }

    [Fact]
    public async Task Without_its_marker_SISR_is_not_started_and_the_fix_installs_it()
    {
        var service = Create();
        _platform.MarkerPresent = false;

        await service.TickAsync();

        Assert.Equal(0, _sisr.Starts);
        Assert.Contains(service.Issues, i => i.Fix == HealthFix.CreateMarker);

        // The settings ask before Steam closes, then call the install.
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyFixAsync(HealthFix.CreateMarker));
        await service.InstallAsync();

        Assert.True(_platform.MarkerPresent);
        Assert.Equal(1, _platform.SteamStops);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.Equal(1, _sisr.Starts);
    }

    [Fact]
    public void A_missing_marker_means_SISR_still_needs_installing()
    {
        var service = Create(enabled: false);
        _platform.MarkerPresent = false;

        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);
    }

    [Fact]
    public async Task The_controller_counts_as_connected_only_while_SISR_emulates_it()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        Assert.True(service.SteamControllerConnected);

        // Only the dongle is left: SISR emulates nothing.
        _api.Controllers = 0;
        await service.WatchAsync();
        Assert.Equal(SisrState.NoController, service.State);
        Assert.False(service.SteamControllerConnected);

        _api.Controllers = 1;
        await service.TickAsync();
        Assert.True(service.SteamControllerConnected);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Overlapping_ticks_start_SISR_at_most_once()
    {
        var service = Create();
        _sisr.StartLeavesItStopped = true;
        _sisr.ReleaseLookupGate = new TaskCompletionSource();

        var first = service.TickAsync();
        var second = service.TickAsync();
        Assert.True(second.IsCompleted);
        _sisr.ReleaseLookupGate.SetResult();
        await first;

        Assert.Equal(1, _sisr.Starts);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Switching_off_during_a_tick_keeps_SISR_off()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        Assert.Equal(SisrState.Running, service.State);

        // The tick waits for SISR's API; meanwhile the user switches off. The API then doesn't
        // answer, so the tick would restart SISR to take it over.
        _api.Gate = new TaskCompletionSource();
        var tick = service.TickAsync();
        var off = service.SetEnabledAsync(false);
        _api.Reachable = false;
        _api.Gate.SetResult();
        _api.Gate = null;
        await Task.WhenAll(tick, off);

        Assert.False(_sisr.Running);
        Assert.Equal(1, _sisr.Stops);
        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(SisrState.Off, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task SISR_stays_stopped_while_a_game_has_it_paused()
    {
        var service = Create();
        _sisr.Running = true;
        var game = SteamGame();
        _platform.RunningDirectories.Add(game.InstallDirectory!);

        await service.BeginGameAsync(game);
        await service.TickAsync();

        Assert.False(_sisr.Running);
        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(SisrState.Paused, service.State);
        Assert.Equal([game.InstallDirectory!], service.PausedFor);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task SISR_runs_again_when_the_game_ends()
    {
        var service = Create();
        _sisr.Running = true;
        var game = SteamGame();
        _platform.RunningDirectories.Add(game.InstallDirectory!);
        await service.BeginGameAsync(game);

        _platform.RunningDirectories.Clear();
        await AdvanceUntil(() => _sisr.Starts == 1);

        Assert.Empty(service.PausedFor);
        Assert.Equal(SisrState.Starting, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task A_pause_that_ends_after_switching_off_does_not_restart_SISR()
    {
        var service = Create();
        _sisr.Running = true;
        var game = SteamGame();
        _platform.RunningDirectories.Add(game.InstallDirectory!);
        await service.BeginGameAsync(game);

        await service.SetEnabledAsync(false);
        _platform.RunningDirectories.Clear();
        await AdvanceUntil(() => service.PausedFor.Count == 0);
        await Task.Delay(50);

        Assert.False(_sisr.Running);
        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(SisrState.Off, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Switching_on_again_while_the_game_still_runs_keeps_the_pause()
    {
        var service = Create();
        _sisr.Running = true;
        var game = SteamGame();
        _platform.RunningDirectories.Add(game.InstallDirectory!);
        await service.BeginGameAsync(game);

        await service.SetEnabledAsync(false);
        await service.SetEnabledAsync(true);

        Assert.False(_sisr.Running);
        Assert.Equal(SisrState.Paused, service.State);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task A_failing_process_start_is_reported_not_thrown()
    {
        var service = Create();
        _sisr.StartError = new Win32Exception(5, "Zugriff verweigert");

        await service.TickAsync();

        Assert.Equal(SisrState.Stopped, service.State);
        Assert.Contains("Zugriff verweigert", service.LastError);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task After_three_crashes_in_five_minutes_Arcadia_stops_restarting()
    {
        var service = Create();
        _sisr.StartLeavesItStopped = true;

        for (var i = 0; i < 5; i++)
        {
            await service.TickAsync();
            _time.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(ControllerSupportService.MaxRestartsPerWindow, _sisr.Starts);
        Assert.Equal(SisrState.Failed, service.State);
        Assert.Contains(service.Issues, i => i.Id == "sisr-crashing");

        // Once the window has passed, Arcadia tries again.
        _time.Advance(ControllerSupportService.CrashWindow);
        await service.TickAsync();
        Assert.Equal(ControllerSupportService.MaxRestartsPerWindow + 1, _sisr.Starts);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task An_unreachable_API_does_not_break_the_tick()
    {
        var service = Create();
        _sisr.Running = true;
        _api.Reachable = false;
        await service.TickAsync();

        // A SISR that doesn't answer on Arcadia's port is taken over by restarting it.
        await service.TickAsync();

        Assert.Equal(1, _sisr.Stops);
        Assert.Equal(1, _sisr.Starts);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task A_hanging_GitHub_request_times_out_with_a_message()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        _sisr.ReleaseLookupHangs = true;

        var install = service.InstallAsync();
        await AdvanceUntil(() => install.IsCompleted);
        await install;

        Assert.Contains("GitHub", service.LastError);
        Assert.Null(service.BusyText);
        Assert.False(service.Enabled);
        Assert.Equal(SisrState.Off, service.State);
        Assert.Equal(0, _sisr.Starts);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task A_hanging_GitHub_request_delays_only_its_own_tick()
    {
        var service = Create();
        _sisr.Running = true;
        _sisr.ReleaseLookupHangs = true;

        var tick = service.TickAsync();
        await AdvanceUntil(() => tick.IsCompleted);
        await tick;

        Assert.Equal(SisrState.Running, service.State);
    }

    [Fact]
    public async Task A_broken_download_leaves_SISR_off_with_a_message()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        _sisr.InstallError = new HttpRequestException("Verbindung abgebrochen");

        await service.InstallAsync();

        Assert.Equal("Verbindung abgebrochen", service.LastError);
        Assert.False(service.Enabled);
        Assert.Equal(SisrState.Off, service.State);
        Assert.Equal(0, _sisr.Starts);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Nothing_starts_SISR_after_shutdown()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();

        await service.ShutdownAsync();
        await service.TickAsync();

        Assert.False(_sisr.Running);
        Assert.Equal(0, _sisr.Starts);
    }

    [Fact]
    public async Task Installing_closes_Steam_sets_everything_up_and_switches_on()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        _platform.IsUsbipReady = false;
        _platform.IsCefDebuggingEnabled = false;
        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);
        Assert.True(service.InstallStopsSteam);

        await service.InstallAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.SteamStops);
        Assert.True(_platform.IsCefDebuggingEnabled);
        Assert.True(_sisr.Installed);
        Assert.Equal(1, _platform.UsbipInstalls);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.True(service.Enabled);
        Assert.Equal(1, _sisr.Starts);
        Assert.Equal(ControllerSupportSetup.Ready, service.Setup);
        AssertNoUnexpectedTransition();
    }

    [Fact]
    public async Task Installing_while_Steam_is_closed_leaves_it_closed()
    {
        var service = Create(enabled: false);
        _platform.IsSteamRunning = false;
        _platform.IsCefDebuggingEnabled = false;

        await service.InstallAsync();

        Assert.Equal(0, _platform.SteamStops);
        Assert.Equal(0, _platform.SteamStarts);
        Assert.Equal(0, _platform.UsbipInstalls);
        Assert.True(_platform.IsCefDebuggingEnabled);
        Assert.True(service.Enabled);
    }

    [Fact]
    public async Task A_running_Steam_game_blocks_the_install_and_is_named()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        _platform.SteamGame = "Hollow Knight";

        Assert.Contains("Hollow Knight", service.InstallBlocker());
        await service.InstallAsync();

        Assert.Contains("Ein laufendes Steam-Spiel (Hollow Knight) wurde erkannt", service.LastError);
        Assert.Equal(0, _platform.SteamStops);
        Assert.False(_sisr.Installed);
        Assert.False(service.Enabled);
    }

    [Fact]
    public void The_blocking_game_is_read_fresh_so_its_end_is_noticed()
    {
        var service = Create(enabled: false);
        _platform.SteamGame = "Hollow Knight";
        Assert.Equal("Hollow Knight", service.SteamGameBlockingInstall());

        _platform.SteamGame = null;

        Assert.Null(service.SteamGameBlockingInstall());
        Assert.Null(service.InstallBlocker());
    }

    [Fact]
    public void A_game_counts_only_while_Steam_runs()
    {
        var service = Create(enabled: false);
        _platform.SteamGame = "Hollow Knight";
        _platform.IsSteamRunning = false;

        Assert.Null(service.SteamGameBlockingInstall());
    }

    [Fact]
    public async Task ClearError_forgets_the_last_failure()
    {
        var service = Create(enabled: false);
        _platform.SteamRefusesToExit = true;
        await service.InstallAsync();
        Assert.NotNull(service.LastError);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.ClearError();

        Assert.Null(service.LastError);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_declined_driver_install_starts_Steam_again_and_stays_off()
    {
        var service = Create(enabled: false);
        _platform.IsUsbipReady = false;
        _platform.UsbipInstallDeclined = true;

        await service.InstallAsync();

        Assert.Contains("abgelehnt", service.LastError);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.True(_platform.IsSteamRunning);
        Assert.False(service.Enabled);
        Assert.Equal(0, _sisr.Starts);
        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);
    }

    [Fact]
    public async Task If_Steam_does_not_exit_nothing_is_installed()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        _platform.SteamRefusesToExit = true;

        await service.InstallAsync();

        Assert.Contains("Steam ließ sich nicht beenden", service.LastError);
        Assert.False(_sisr.Installed);
        Assert.Equal(0, _platform.SteamStarts);
        Assert.False(service.Enabled);
    }

    [Fact]
    public async Task Switching_on_no_longer_installs_anything()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;

        await service.SetEnabledAsync(true);

        Assert.False(_sisr.Installed);
        Assert.Equal(0, _sisr.Starts);
    }

    [Theory]
    [InlineData(false, true, true, ControllerSupportSetup.UnsupportedPlatform)]
    [InlineData(true, false, true, ControllerSupportSetup.SteamMissing)]
    [InlineData(true, true, false, ControllerSupportSetup.NeedsInstall)]
    [InlineData(true, true, true, ControllerSupportSetup.Ready)]
    public async Task Setup_says_what_the_settings_offer(bool windows, bool steam, bool sisrInstalled, ControllerSupportSetup expected)
    {
        var service = Create(enabled: false);
        _platform.IsSupported = windows;
        _platform.IsSteamInstalled = steam;
        _sisr.Installed = sisrInstalled;

        await service.RefreshAsync();

        Assert.Equal(expected, service.Setup);
    }

    [Fact]
    public async Task A_tick_while_off_notices_a_change_in_the_setup()
    {
        var service = Create(enabled: false);
        service.WatchSetup = true;
        _sisr.Installed = false;
        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);

        _sisr.Installed = true;
        await service.TickAsync();

        Assert.Equal(ControllerSupportSetup.Ready, service.Setup);
    }

    [Fact]
    public async Task While_off_and_the_settings_closed_the_tick_leaves_the_setup_alone()
    {
        var service = Create(enabled: false);
        _sisr.Installed = false;
        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);

        _sisr.Installed = true;
        await service.TickAsync();

        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);
    }

    [Fact]
    public async Task A_missing_marker_is_not_counted_as_SISR_crashing()
    {
        var service = Create();
        _platform.MarkerPresent = false;

        for (var i = 0; i < 5; i++)
        {
            await service.TickAsync();
            _time.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(0, _sisr.Starts);
        Assert.NotEqual(SisrState.Failed, service.State);
        Assert.DoesNotContain(service.Issues, i => i.Id == "sisr-crashing");
    }

    [Fact]
    public async Task Installing_stops_a_running_SISR_before_it_closes_Steam()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        _platform.MarkerPresent = false;

        await service.InstallAsync();

        Assert.Equal(1, _sisr.Stops);
        Assert.Equal(0, _sisr.Kills);
        Assert.Equal(1, _platform.SteamStops);
        Assert.True(_platform.MarkerPresent);
        Assert.Equal(1, _sisr.Starts);
    }

    [Fact]
    public async Task Without_a_known_Steam_account_the_install_says_so_and_stays_off()
    {
        var service = Create(enabled: false);
        _platform.MarkerPresent = false;
        _platform.NoSteamAccount = true;

        await service.InstallAsync();

        Assert.False(service.Enabled);
        Assert.Contains("Steam-Konto", service.LastError);
        // Found out before Steam closes or the driver installs.
        Assert.Equal(0, _platform.SteamStops);
        Assert.Equal(0, _platform.UsbipInstalls);
    }

    [Fact]
    public async Task A_marker_only_for_another_account_leaves_SISR_off_and_offers_to_add_it()
    {
        var service = Create(enabled: false);
        _platform.MarkerPresent = false;
        _platform.Accounts.Add(new MarkerAccount(111, "Anna", HasMarker: false));
        _platform.Accounts.Add(new MarkerAccount(222, "Ben", HasMarker: false));

        await service.InstallAsync(markerAccounts: [222]);
        await service.RefreshAsync();

        Assert.True(service.Enabled);
        Assert.Equal(0, _sisr.Starts);
        Assert.Contains(service.Issues, i => i.Fix == HealthFix.CreateMarker);
    }

    [Fact]
    public async Task With_several_accounts_that_all_have_the_marker_the_install_writes_none()
    {
        var service = Create(enabled: false);
        _platform.Accounts.Add(new MarkerAccount(111, "Anna", HasMarker: true));
        _platform.Accounts.Add(new MarkerAccount(222, "Ben", HasMarker: true));

        await service.InstallAsync();

        Assert.True(service.Enabled);
        Assert.Equal([], _platform.MarkerWrittenFor);
    }

    [Fact]
    public void With_one_Steam_account_nobody_is_asked_about_the_marker()
    {
        var service = Create(enabled: false);
        _platform.Accounts.Add(new MarkerAccount(111, "Anna", HasMarker: false));

        Assert.Empty(service.MarkerAccountsToChoose());
    }

    [Fact]
    public void With_several_Steam_accounts_the_user_chooses_until_all_have_the_marker()
    {
        var service = Create(enabled: false);
        _platform.Accounts.Add(new MarkerAccount(111, "Anna", HasMarker: true));
        _platform.Accounts.Add(new MarkerAccount(222, "Ben", HasMarker: false));

        Assert.Equal(2, service.MarkerAccountsToChoose().Count);

        _platform.Accounts[1] = _platform.Accounts[1] with { HasMarker = true };
        Assert.Empty(service.MarkerAccountsToChoose());
    }

    [Fact]
    public async Task The_install_writes_the_marker_for_the_chosen_accounts()
    {
        var service = Create(enabled: false);
        _platform.MarkerPresent = false;
        _platform.Accounts.Add(new MarkerAccount(111, "Anna", HasMarker: false));
        _platform.Accounts.Add(new MarkerAccount(222, "Ben", HasMarker: false));

        await service.InstallAsync(markerAccounts: [222]);

        Assert.Equal([222u], _platform.MarkerWrittenFor);
        Assert.True(service.Enabled);
        Assert.Equal([false, true], _platform.Accounts.Select(a => a.HasMarker));
    }

    [Fact]
    public async Task A_hanging_Steam_login_does_not_stop_a_running_SISR()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();

        _platform.SteamLoginHangs = true;
        await service.WatchAsync();
        await service.TickAsync();

        Assert.True(_sisr.Running);
        Assert.Equal(0, _sisr.Kills);
        Assert.Equal(0, _sisr.Stops);
    }

    [Fact]
    public async Task A_Steam_login_that_hangs_for_long_is_shown_with_a_restart()
    {
        var service = Create();
        _platform.SteamLoginHangs = true;

        await service.TickAsync();
        Assert.DoesNotContain(service.Issues, i => i.Id == "steam-login");

        _time.Advance(TimeSpan.FromSeconds(31));
        await service.TickAsync();

        Assert.Contains(service.Issues, i => i.Id == "steam-login" && i.Fix == HealthFix.RestartSteam);
        Assert.Equal(0, _sisr.Starts);
        Assert.NotEqual(SisrState.Failed, service.State);
    }

    [Fact]
    public async Task Restarting_Steam_goes_through_Arcadia_and_brings_SISR_back()
    {
        var service = Create();
        _sisr.Running = true;
        await service.TickAsync();
        _platform.IsCefDebuggingEnabled = false;

        await service.ApplyFixAsync(HealthFix.EnableCefDebugging);

        Assert.Equal(1, _sisr.Stops);
        Assert.Equal(0, _sisr.Kills);
        Assert.Equal(1, _platform.SteamStops);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.True(_platform.IsCefDebuggingEnabled);
        Assert.Equal(1, _sisr.Starts);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task A_failed_switch_on_stays_unseen_until_the_settings_show_it()
    {
        var service = Create(enabled: false);
        _platform.IsSteamRunning = false;
        _platform.SteamWontStart = true;

        var on = service.SetEnabledAsync(true);
        await AdvanceUntil(() => on.IsCompleted);
        Assert.True(service.UnseenSteamStartFailure);

        service.AcknowledgeSteamStartFailure();
        Assert.False(service.UnseenSteamStartFailure);
    }

    [Fact]
    public async Task Settings_changed_from_several_threads_at_once_are_all_kept()
    {
        var service = Create();
        var games = Enumerable.Range(0, 40).Select(i => SteamGame() with { Id = $"g{i}" }).ToList();

        await Task.WhenAll(games.Select(game => Task.Run(() => service.SetModeFor(game, useSisr: true))));

        var saved = new SettingsStore(Path.Combine(_directory, "settings.json")).Load();
        Assert.All(games, game => Assert.True(saved.ControllerSupportFor(game)));
    }

    private sealed class FakeSisr(HttpClient http) : ISisrHost
    {
        public SisrApiClient Api { get; } = new(http, SisrHost.ApiPort);

        public bool Installed { get; set; } = true;

        public bool Running { get; set; }

        /// <summary>The process exits right away (or never appears).</summary>
        public bool StartLeavesItStopped { get; set; }

        public Exception? StartError { get; set; }

        public Exception? InstallError { get; set; }

        public bool ReleaseLookupHangs { get; set; }

        public TaskCompletionSource? ReleaseLookupGate { get; set; }

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public int ViiperCleanUps { get; private set; }

        /// <summary>What the next clean-up finds left over (a VIIPER process or an attached device).</summary>
        public int ViiperLeftovers { get; set; }

        public Task<int> CleanUpViiperAsync(CancellationToken cancellationToken = default)
        {
            ViiperCleanUps++;
            var found = ViiperLeftovers;
            ViiperLeftovers = 0;
            return Task.FromResult(found);
        }

        public bool IsInstalled => Installed;

        public Version? InstalledVersion => Installed ? new Version(1, 0) : null;

        public bool IsRunning => Running;

        public IReadOnlyList<int> ExternalInstances() => [];

        public void StopExternalInstances()
        {
        }

        public async Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (ReleaseLookupHangs)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (ReleaseLookupGate is { } gate)
            {
                await gate.Task;
            }

            return new Release("v1.0", new Version(1, 0), []);
        }

        public Task InstallAsync(Release release, CancellationToken cancellationToken = default)
        {
            if (InstallError is { } error)
            {
                throw error;
            }

            Installed = true;
            return Task.CompletedTask;
        }

        public void Start()
        {
            if (StartError is { } error)
            {
                throw error;
            }

            Starts++;
            Running = !StartLeavesItStopped;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (Running)
            {
                Stops++;
            }

            Running = false;
            return Task.CompletedTask;
        }

        public int Kills { get; private set; }

        public bool SetupMarkedDone { get; private set; }

        public string ExecutablePath => @"C:\SISR\SISR.exe";

        public void Kill()
        {
            Kills++;
            Running = false;
        }

        public void MarkInitialSetupDone() => SetupMarkedDone = true;
    }

    /// <summary>SISR's HTTP API: answers like a healthy SISR with one controller, or not at all.</summary>
    private sealed class FakeSisrApi : HttpMessageHandler
    {
        public bool Reachable { get; set; } = true;

        /// <summary>Controllers SISR emulates; 0 is a dongle without a switched-on controller.</summary>
        public int Controllers { get; set; } = 1;

        public TaskCompletionSource? Gate { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (!Reachable)
            {
                throw new HttpRequestException("SISR antwortet nicht");
            }

            var path = request.RequestUri!.AbsolutePath;
            var json = path.EndsWith("/devices") ? (Controllers > 0 ? """[{"viiper_device":{}}]""" : "[]")
                : path.EndsWith("/version/info") ? """{"version":"1.0"}"""
                : path.EndsWith("/steam/status") ? """{"steam_running":true,"cef_debug_enabled":true,"cef_debug_reachable":true,"marker_shortcut_present":true}"""
                : path.EndsWith("/viiper/ping") ? """{"status":{"version":"1.0"}}"""
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FakePlatform : IControllerSupportPlatform
    {
        public HashSet<string> RunningDirectories { get; } = [];

        public bool IsSupported { get; set; } = true;

        public bool IsSteamInstalled { get; set; } = true;

        public bool IsSteamRunning { get; set; } = true;

        /// <summary>steam.exe still runs but Steam goes away: it shuts down, the user is logged out.</summary>
        public bool SteamNotReady { get; set; }

        /// <summary>Steam is up with a user, but its login never completes ("loading user data").</summary>
        public bool SteamLoginHangs { get; set; }

        public bool IsSteamUp => IsSteamRunning && !SteamNotReady;

        public bool IsSteamReady => IsSteamUp && !SteamLoginHangs;

        /// <summary>"Steam starten" does nothing, as with a broken Steam.</summary>
        public bool SteamWontStart { get; set; }

        public int LayoutResets { get; private set; }

        public bool IsUsbipReady { get; set; } = true;

        public bool IsCefDebuggingEnabled { get; set; } = true;

        public string? SteamGame { get; set; }

        public bool SteamRefusesToExit { get; set; }

        public bool UsbipInstallDeclined { get; set; }

        public int SteamStops { get; private set; }

        public int SteamStarts { get; private set; }

        public int UsbipInstalls { get; private set; }

        public void EnableCefDebugging() => IsCefDebuggingEnabled = true;

        public string? RunningSteamGame() => SteamGame;

        public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken)
        {
            if (SteamRefusesToExit)
            {
                return Task.FromResult(false);
            }

            SteamStops++;
            IsSteamRunning = false;
            return Task.FromResult(true);
        }

        public void StartSteam()
        {
            SteamStarts++;
            IsSteamRunning = !SteamWontStart;
        }

        public Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken)
        {
            UsbipInstalls++;
            IsUsbipReady = !UsbipInstallDeclined;
            return Task.FromResult(!UsbipInstallDeclined);
        }

        public void ResetSteamInputLayout() => LayoutResets++;

        public int MarkerChecks { get; private set; }

        public bool MarkerPresent { get; set; } = true;

        public bool HasSisrMarker(string sisrExecutable) => MarkerPresent;

        /// <summary>No Steam account is known (never logged in), so the marker can't be written.</summary>
        public bool NoSteamAccount { get; set; }

        /// <summary>The Steam accounts on this PC, the logged-in one first; empty stands for one account whose marker is <see cref="MarkerPresent"/>.</summary>
        public List<MarkerAccount> Accounts { get; } = [];

        /// <summary>The accounts passed to the last <see cref="EnsureSisrMarker"/>; null when none were chosen.</summary>
        public IReadOnlyCollection<uint>? MarkerWrittenFor { get; private set; }

        /// <summary>Only while Steam is closed and an account is known, like the real one.</summary>
        public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null)
        {
            MarkerChecks++;
            var written = !IsSteamRunning && !NoSteamAccount;
            if (!written)
            {
                return false;
            }

            MarkerWrittenFor = accountIds;
            for (var i = 0; i < Accounts.Count; i++)
            {
                if (accountIds?.Contains(Accounts[i].Id) == true)
                {
                    Accounts[i] = Accounts[i] with { HasMarker = true };
                }
            }

            // SISR reads the marker of the logged-in account, the first one here.
            MarkerPresent |= accountIds is null || (Accounts.Count > 0 && accountIds.Contains(Accounts[0].Id));
            return true;
        }

        public IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable) =>
            NoSteamAccount ? [] : Accounts.Count > 0 ? Accounts.ToList() : [new MarkerAccount(1, "Anna", MarkerPresent)];

        public bool AnyRunningFrom(string directory)
        {
            lock (RunningDirectories)
            {
                return RunningDirectories.Contains(directory);
            }
        }

        public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now) => [];
    }
}
