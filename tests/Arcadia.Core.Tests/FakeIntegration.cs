using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

/// <summary>A launcher made of whatever parts a test needs.</summary>
internal sealed class FakeIntegration(LauncherInfo launcher) : ILauncherIntegration
{
    public LauncherInfo Launcher => launcher;

    public ILauncherScanner Scanner { get; init; } = new FakeScanner(launcher);

    public ILauncherClient? Client { get; init; }

    public IInstallMonitor? InstallMonitor { get; init; }

    public IRunningGamesMonitor? RunningGames { get; init; }

    public IReadOnlyCollection<string> ProgramNames { get; init; } = [];

    /// <summary>Where <see cref="Locate"/> finds the launcher; null: not installed.</summary>
    public string? Location { get; set; } = $@"C:\Launchers\{launcher.Id}";

    public string? Locate() => Location;

    public ILauncherFolderChoice? FolderChoice { get; init; }

    public IReadOnlyList<LibraryTypeOption> LibraryOptions { get; init; } = [];

    /// <summary>Without a choice of the user: every launcher found is active.</summary>
    public static LauncherRegistry Registry(params ILauncherIntegration[] integrations) => new(integrations, () => null);

    /// <summary>The registered launchers that pass <paramref name="isActive"/> are active, in registered order.</summary>
    public static LauncherRegistry Registry(IReadOnlyList<ILauncherIntegration> integrations, Func<string, bool> isActive) =>
        new(integrations, () => integrations.Select(i => i.Launcher.Id).Where(isActive).ToList());

    public static LauncherInfo LauncherOf(string id) => new(id, id, id);
}

/// <summary>Returns <see cref="Games"/>; with a <see cref="Gate"/>, only once it is opened.</summary>
internal sealed class FakeScanner(LauncherInfo launcher) : ILauncherScanner
{
    public IReadOnlyList<Game> Games { get; set; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public int Scans { get; private set; }

    /// <summary>While set, a scan throws it (#369).</summary>
    public Exception? Failure { get; set; }

    public LauncherInfo Launcher => launcher;

    public async Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        Scans++;
        if (Gate is { } gate)
        {
            await gate.Task;
        }

        return Failure is { } failure ? throw failure : Games;
    }
}

/// <summary>Reports <see cref="Active"/> and <see cref="Finished"/>; with a <see cref="Gate"/>, only once it is set.</summary>
internal sealed class FakeMonitor(string launcherId) : IInstallMonitor
{
    public Dictionary<string, InstallProgress> Active { get; } = [];

    public Dictionary<string, InstallOutcome> Finished { get; } = [];

    public ManualResetEventSlim? Gate { get; init; }

    public bool Fails { get; set; }

    public int Polls => _polls;

    /// <summary>The token of the latest poll.</summary>
    public CancellationToken Token { get; private set; }

    private int _polls;

    public string LauncherId => launcherId;

    public InstallSnapshot Poll(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _polls);
        Token = cancellationToken;
        Gate?.Wait(TimeSpan.FromSeconds(10));
        if (Fails)
        {
            throw new InvalidOperationException("broken plugin");
        }

        return new InstallSnapshot(new Dictionary<string, InstallProgress>(Active), new Dictionary<string, InstallOutcome>(Finished));
    }
}

/// <summary>Reports <see cref="Running"/>; counts its polls.</summary>
internal sealed class FakeRunningGames(string launcherId) : IRunningGamesMonitor
{
    public List<RunningGame> Running { get; } = [];

    public int Polls { get; private set; }

    public string LauncherId => launcherId;

    public bool ReportsSimulatedGames { get; init; }

    public IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default)
    {
        Polls++;
        return [.. Running];
    }

    /// <summary>The starts the launcher aborted; each is reported once, as a log is read on.</summary>
    public List<RefusedStart> Refused { get; } = [];

    public IReadOnlyList<RefusedStart> RefusedStarts()
    {
        var refused = Refused.ToList();
        Refused.Clear();
        return refused;
    }
}
