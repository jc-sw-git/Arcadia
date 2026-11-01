using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>
/// A fictional library for one launcher (<c>--demo</c>); its games can't be started or installed. One of them runs, so
/// "Läuft" can be seen without a launcher (#95).
/// </summary>
public sealed class DemoIntegration : ILauncherIntegration
{
    /// <param name="withOtherTypes">Software, tools and soundtracks besides the games, with a switch for each (#141).</param>
    public DemoIntegration(LauncherInfo launcher, int gameCount, DateTimeOffset now, bool withOtherTypes = false)
    {
        Launcher = launcher;
        var scanner = new DemoScanner(launcher, gameCount, now, withOtherTypes);
        Scanner = scanner;
        RunningGames = new DemoRunningGames(scanner, now);
        LibraryOptions = withOtherTypes ? OtherTypeOptions : [];
    }

    private static readonly IReadOnlyList<LibraryTypeOption> OtherTypeOptions =
    [
        new("tools", "Werkzeuge einbeziehen", "Demo-Werkzeuge", "Zeigt die Werkzeuge der Demo-Bibliothek.", [], [GameType.Tool]),
        new("software", "Software einbeziehen", "Demo-Software", "Zeigt die Software der Demo-Bibliothek.", [], [GameType.Software]),
        new("soundtracks", "Soundtracks einbeziehen", "Demo-Soundtracks", "Zeigt die Soundtracks der Demo-Bibliothek.", [], [GameType.Soundtrack]),
    ];

    public IReadOnlyList<LibraryTypeOption> LibraryOptions { get; }

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

    public IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default) => _running ??= scanner.Generate()
        .Where(game => game.IsInstalled)
        .Take(1)
        .Select(game => new RunningGame(game.Id, now.AddMinutes(-25)))
        .ToList();
}
