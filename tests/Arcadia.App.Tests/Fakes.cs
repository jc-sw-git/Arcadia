using Arcadia.ControllerSupport;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;
using Arcadia.Launchers.Steam;

namespace Arcadia.App.Tests;

/// <summary>
/// A launcher with the demo library's games whose starts are recorded instead of run, so a test sees which game the
/// controller started, on every platform and without a launcher installed.
/// </summary>
internal sealed class TestLauncher(LauncherInfo launcher, int gameCount, DateTimeOffset now, RecordedStarts starts) : ILauncherIntegration
{
    public LauncherInfo Launcher => launcher;

    public ILauncherScanner Scanner { get; } = new DemoScanner(launcher, gameCount, now);

    public ILauncherClient? Client { get; } = new RecordingClient(launcher.Id, starts);

    public IInstallMonitor? InstallMonitor => null;

    public IRunningGamesMonitor? RunningGames => null;

    public IReadOnlyCollection<string> ProgramNames => [];

    /// <summary>Null: the launcher isn't installed, e.g. to see that a tab of an uninstalled launcher goes away.</summary>
    public string? Location { get; set; } = "Test";

    public string? Locate() => Location;
}

/// <summary>The games the launchers were asked to start, in order.</summary>
internal sealed class RecordedStarts
{
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_keys)
            {
                return [.. _keys];
            }
        }
    }

    public void Add(string gameKey)
    {
        lock (_keys)
        {
            _keys.Add(gameKey);
        }
    }
}

internal sealed class RecordingClient(string launcherId, RecordedStarts starts) : ILauncherClient
{
    public string? LauncherId => launcherId;

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        starts.Add(game.Key);
        return Task.CompletedTask;
    }
}

/// <summary>No process is a game: the running games come only from what Arcadia started.</summary>
internal sealed class NoProcesses : IProcessTable
{
    public IReadOnlyCollection<int> Ids() => [];

    public ProcessDetails? Describe(int processId) => null;
}

/// <summary>The Steam Controller Support as on a PC without Windows or Steam: shown, but not available.</summary>
internal sealed class NoControllerSupportPlatform : IControllerSupportPlatform
{
    public bool IsSupported => false;

    public bool IsSteamInstalled => false;

    public bool IsSteamRunning => false;

    public bool IsSteamReady => false;

    public bool IsSteamUp => false;

    public bool IsUsbipReady => false;

    public bool IsCefDebuggingEnabled => false;

    public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null) => false;

    public IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable) => [];

    public bool HasSisrMarker(string sisrExecutable) => false;

    public void EnableCefDebugging()
    {
    }

    public string? RunningSteamGame() => null;

    public int? RunningSteamAppId() => null;

    public SteamInputSetting? SteamInputFor(int appId) => null;

    public SteamInputSetting? SteamInputDefaults() => null;

    public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken) => Task.FromResult(false);

    public void StartSteam()
    {
    }

    public Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken) => Task.FromResult(false);

    public void ResetSteamInputLayout()
    {
    }

    public bool AnyRunningFrom(string directory) => false;

    public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now) => [];
}

/// <summary>SISR is not installed and nothing tries to start it.</summary>
internal sealed class NoSisr(HttpClient http) : ISisrHost
{
    public SisrApiClient Api { get; } = new(http, 1);

    public bool IsInstalled => false;

    public string ExecutablePath => "sisr.exe";

    public Version? InstalledVersion => null;

    public bool IsRunning => false;

    public IReadOnlyList<int> ExternalInstances() => [];

    public void StopExternalInstances()
    {
    }

    public Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default) => Task.FromResult<Release?>(null);

    public Task InstallAsync(Release release, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Die Abnahmetests installieren kein SISR.");

    public void Start() => throw new InvalidOperationException("Die Abnahmetests starten kein SISR.");

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Kill()
    {
    }

    public void MarkInitialSetupDone()
    {
    }

    public Task<int> CleanUpViiperAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
