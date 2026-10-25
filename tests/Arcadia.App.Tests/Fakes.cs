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
internal sealed class TestLauncher(LauncherSpec spec, DateTimeOffset now, RecordedStarts starts, LauncherInstalls installs) : ILauncherIntegration
{
    public LauncherInfo Launcher => spec.Launcher;

    public ILauncherScanner Scanner { get; } = spec.Library is { } library
        ? new FixedScanner(spec.Launcher, library)
        : new DemoScanner(spec.Launcher, spec.Games, now);

    public ILauncherClient? Client { get; } = new RecordingClient(spec.Launcher.Id, starts);

    public IInstallMonitor? InstallMonitor { get; } = new TestInstallMonitor(spec.Launcher.Id, installs);

    public IRunningGamesMonitor? RunningGames => null;

    public IReadOnlyCollection<string> ProgramNames => [];

    public string? Locate() => spec.IsInstalled ? "Test" : null;
}

/// <summary>
/// What the launchers' own windows are installing, as a test sets it: Arcadia learns it when it polls the install
/// monitors (<see cref="ArcadiaDriver.FollowInstallsAsync"/>), as from a download started in the launcher.
/// </summary>
internal sealed class LauncherInstalls
{
    private readonly Dictionary<string, InstallProgress> _active = [];

    public void Set(Game game, InstallProgress? progress)
    {
        lock (_active)
        {
            if (progress is null)
            {
                _active.Remove(game.Key);
            }
            else
            {
                _active[game.Key] = progress;
            }
        }
    }

    public InstallSnapshot Of(string launcherId)
    {
        lock (_active)
        {
            var active = _active.Where(entry => Game.LauncherIdOf(entry.Key) == launcherId)
                .ToDictionary(entry => entry.Key[(launcherId.Length + 1)..], entry => entry.Value);
            return new InstallSnapshot(active, new Dictionary<string, InstallOutcome>());
        }
    }
}

internal sealed class TestInstallMonitor(string launcherId, LauncherInstalls installs) : IInstallMonitor
{
    public string LauncherId => launcherId;

    public InstallSnapshot Poll() => installs.Of(launcherId);
}

internal sealed class FixedScanner(LauncherInfo launcher, IReadOnlyList<Game> games) : ILauncherScanner
{
    public LauncherInfo Launcher => launcher;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default) => Task.FromResult(games);
}

/// <summary>Games whose every title and date a test names, so its expectations don't come from Arcadia's own code.</summary>
internal static class TestGames
{
    public static Game Installed(LauncherInfo launcher, string title, DateTimeOffset installedAt, DateTimeOffset? lastPlayedAt = null) => new()
    {
        Id = title,
        LauncherId = launcher.Id,
        Title = title,
        Launch = new LaunchTarget.None(),
        InstalledAt = installedAt,
        LastPlayedAt = lastPlayedAt,
    };

    public static Game NotInstalled(LauncherInfo launcher, string title) => new()
    {
        Id = title,
        LauncherId = launcher.Id,
        Title = title,
        Launch = new LaunchTarget.None(),
        IsInstalled = false,
        Install = new LaunchTarget.None(),
    };
}

/// <summary>The games the launchers were asked to start, in order, failed attempts included.</summary>
internal sealed class RecordedStarts
{
    private readonly List<string> _keys = [];

    /// <summary>Thrown by the launcher at every start while set, as by a launcher whose start fails.</summary>
    public Exception? Failure { get; set; }

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
        if (starts.Failure is { } failure)
        {
            throw failure;
        }

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
