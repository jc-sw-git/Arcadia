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
        _sisr.Installed = false;
        Assert.Equal(ControllerSupportSetup.NeedsInstall, service.Setup);

        _sisr.Installed = true;
        await service.TickAsync();

        Assert.Equal(ControllerSupportSetup.Ready, service.Setup);
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
    }

    /// <summary>SISR's HTTP API: answers like a healthy SISR with one controller, or not at all.</summary>
    private sealed class FakeSisrApi : HttpMessageHandler
    {
        public bool Reachable { get; set; } = true;

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
            var json = path.EndsWith("/devices") ? """[{"viiper_device":{}}]"""
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
            IsSteamRunning = true;
        }

        public Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken)
        {
            UsbipInstalls++;
            IsUsbipReady = !UsbipInstallDeclined;
            return Task.FromResult(!UsbipInstallDeclined);
        }

        public bool IsSteamControllerConnected() => true;

        public void ResetSteamInputLayout()
        {
        }

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
