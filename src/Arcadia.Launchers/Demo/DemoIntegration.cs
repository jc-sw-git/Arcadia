using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>
/// A fictional library for one launcher (<c>--demo</c>); its games can't be started or installed. One of them runs, so
/// "Läuft" can be seen without a launcher (#95).
/// </summary>
public sealed class DemoIntegration : ILauncherIntegration
{
    public DemoIntegration(LauncherInfo launcher, int gameCount, DateTimeOffset now)
    {
        Launcher = launcher;
        var scanner = new DemoScanner(launcher, gameCount, now);
        Scanner = scanner;
        RunningGames = new DemoRunningGames(scanner, now);
    }

    public LauncherInfo Launcher { get; }

    public ILauncherScanner Scanner { get; }

    public ILauncherClient? Client => null;

    public IInstallMonitor? InstallMonitor => null;

    public IRunningGamesMonitor? RunningGames { get; }

    public IReadOnlyCollection<string> ProgramNames => [];

    public string? Locate() => "Demo-Daten";
}

/// <summary>The first installed game of a demo library runs, since 25 minutes before Arcadia started.</summary>
public sealed class DemoRunningGames(DemoScanner scanner, DateTimeOffset now) : IRunningGamesMonitor
{
    private IReadOnlyList<RunningGame>? _running;

    public string LauncherId => scanner.Launcher.Id;

    public bool ReportsSimulatedGames => true;

    public IReadOnlyList<RunningGame> Poll() => _running ??= scanner.Generate()
        .Where(game => game.IsInstalled)
        .Take(1)
        .Select(game => new RunningGame(game.Id, now.AddMinutes(-25)))
        .ToList();
}
