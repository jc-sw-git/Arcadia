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
        : new DemoScanner(spec.Launcher, spec.Games, now, canUninstall: spec.CanUninstall);

    public ILauncherClient? Client { get; } = new RecordingClient(spec.Launcher.Id, starts);

    public IInstallMonitor? InstallMonitor { get; } = new TestInstallMonitor(spec.Launcher.Id, installs);

    public IRunningGamesMonitor? RunningGames { get; } = spec.Running is { } running ? new FixedRunningGames(spec.Launcher.Id, running, now) : null;

    public IReadOnlyCollection<string> ProgramNames => [];

    public IReadOnlyList<LibraryTypeOption> LibraryOptions => spec.Options ?? [];

    public string? Locate() => spec.IsInstalled ? "Test" : null;
}

/// <summary>
/// The launcher reports these games as running, since 25 minutes before Arcadia started; a test can start more
/// (<see cref="Run"/>) and have the launcher abort a start (<see cref="Refuse"/>, #277). Read on the thread pool.
/// </summary>
internal sealed class FixedRunningGames(string launcherId, IReadOnlyList<string> gameIds, DateTimeOffset now) : IRunningGamesMonitor
{
    private readonly List<RunningGame> _running = [.. gameIds.Select(id => new RunningGame(id, now.AddMinutes(-25)))];
    private readonly List<RefusedStart> _refused = [];

    public string LauncherId => launcherId;

    public IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default)
    {
        lock (_running)
        {
            return [.. _running];
        }
    }

    public IReadOnlyList<RefusedStart> RefusedStarts()
    {
        lock (_refused)
        {
            var refused = _refused.ToList();
            _refused.Clear();
            return refused;
        }
    }

    public void Run(string gameId, DateTimeOffset since)
    {
        lock (_running)
        {
            _running.Add(new RunningGame(gameId, since));
        }
    }

    public void Refuse(RefusedStart refusal)
    {
        lock (_refused)
        {
            _refused.Add(refusal);
        }
    }
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

    public InstallSnapshot Poll(CancellationToken cancellationToken = default) => installs.Of(launcherId);
}

/// <summary>Reads <paramref name="games"/> as they are at each scan: a test may pass a list it changes later, as a launcher's library.</summary>
internal sealed class FixedScanner(LauncherInfo launcher, IReadOnlyList<Game> games) : ILauncherScanner
{
    public LauncherInfo Launcher => launcher;

    private int _scans;

    /// <summary>How often the launcher was read.</summary>
    public int Scans => Volatile.Read(ref _scans);

    /// <summary>While set, a scan throws it, as a locked or unreachable file does (#369).</summary>
    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _scans);
        return Failure is { } failure ? Task.FromException<IReadOnlyList<Game>>(failure) : Task.FromResult<IReadOnlyList<Game>>([.. games]);
    }
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

    /// <summary>Called as the launcher gets the start, before it returns: what a test sees at that moment.</summary>
    public Action<string>? OnStart { get; set; }

    public void Add(string gameKey)
    {
        OnStart?.Invoke(gameKey);
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

    public bool IsUsbipReady => false;

    public bool IsPanReady => false;

    public Arcadia.ControllerSupport.Pan.PanCompatibility PanCompatibility => Arcadia.ControllerSupport.Pan.PanCompatibility.NotInstalled;

    public Version? HidHideVersion => null;

    public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) => [];
}

/// <summary>
/// The disk as Arcadia's install folder check sees it: the real one, except for folders a test removed or made hang (a
/// network share that doesn't answer). Counts the checks, as they run off the UI thread.
/// </summary>
internal sealed class FakeInstallFolders
{
    private readonly HashSet<string> _removed = [];
    private readonly HashSet<string> _failing = [];
    private readonly ManualResetEventSlim _answer = new(initialState: true);
    private int _checks;

    /// <summary>How many checks were asked of the disk.</summary>
    public int Checks => Volatile.Read(ref _checks);

    public bool Exists(string folder)
    {
        Interlocked.Increment(ref _checks);
        _answer.Wait(TimeSpan.FromSeconds(30));
        lock (_removed)
        {
            if (_failing.Contains(folder))
            {
                throw new IOException($"Test: {Path.GetFileName(folder)} can't be read.");
            }

            return !_removed.Contains(folder) && Directory.Exists(folder);
        }
    }

    /// <summary>Asking for the folder throws, as a file system error would.</summary>
    public void Fail(string folder)
    {
        lock (_removed)
        {
            _failing.Add(folder);
        }
    }

    /// <summary>The game was uninstalled in its launcher: the folder is gone.</summary>
    public void Remove(string folder)
    {
        lock (_removed)
        {
            _removed.Add(folder);
        }
    }

    /// <summary>From now on the disk doesn't answer until <see cref="Answer"/>.</summary>
    public void Hang() => _answer.Reset();

    public void Answer() => _answer.Set();
}
