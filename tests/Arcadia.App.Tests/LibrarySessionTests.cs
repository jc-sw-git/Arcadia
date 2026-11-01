using System.Reflection;
using Arcadia.App.Services;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>
/// The library session on its own (#228): it reads the launchers, follows installs and says what it noticed through
/// events, with no window, page, cover or toast around it, as another view would use it.
/// </summary>
public sealed class LibrarySessionTests
{
    private static readonly LauncherInfo Launcher = KnownLaunchers.Gog;

    [Fact]
    [Trait("TestCase", "T228-2")]
    public void The_session_refers_to_no_view_model_of_the_App()
    {
        const BindingFlags Everything = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var session = typeof(LibrarySession);
        var used = session.GetFields(Everything).Select(f => f.FieldType)
            .Concat(session.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
            .Concat(session.GetMethods(Everything).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
            .Concat(session.GetProperties(Everything).Select(p => p.PropertyType))
            .Concat(session.GetEvents(Everything).Select(e => e.EventHandlerType!));

        var presentation = used.Where(t => t.FullName is { } name && name.Contains("Arcadia.App.", StringComparison.Ordinal)
            && !name.Contains("Arcadia.App.Services.", StringComparison.Ordinal)).Select(t => t.FullName).Distinct().ToList();
        Assert.Empty(presentation);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T228-2")]
    public async Task The_session_hands_out_what_it_read_and_says_when_a_rescan_starts_and_ends()
    {
        using var setup = new SessionSetup([Installed(Launcher, "Alpha", ArcadiaDriver.Now), Installed(Launcher, "Beta", ArcadiaDriver.Now)]);
        var session = setup.Session;
        var changes = 0;
        var notices = new List<LibraryNotice>();
        session.LibrariesChanged += (_, _) => changes++;
        session.Noticed += (_, notice) => notices.Add(notice);

        await session.LoadAsync();
        Assert.Equal(1, changes);
        Assert.Equal(["Alpha", "Beta"], session.LibraryOf(Launcher.Id)!.Games.Select(g => g.Title));

        await session.RescanAsync(Launcher);
        Assert.Equal([new LibraryNotice.RescanStarted(Launcher), new LibraryNotice.RescanFinished(Launcher)], notices);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-3")]
    public async Task A_type_switched_on_or_off_shows_or_hides_its_entries_at_once_without_reading_the_launcher_again()
    {
        var preferences = new LauncherPreferences();
        LibraryTypeOption tools = new("tools", "Werkzeuge einbeziehen", "", "", [], [GameType.Tool]);
        Game[] games = [Installed(Launcher, "Alpha", ArcadiaDriver.Now), Installed(Launcher, "Blender", ArcadiaDriver.Now) with { Type = GameType.Tool }];
        using var setup = new SessionSetup(games, [tools], _ => preferences);
        var session = setup.Session;
        await session.LoadAsync();
        var changes = 0;
        session.LibrariesChanged += (_, _) => changes++;

        Assert.Equal(["Alpha"], session.LibraryOf(Launcher.Id)!.Games.Select(g => g.Title));

        preferences = preferences.WithLibraryOption("tools", true);
        session.ShowChosenTypes();

        Assert.Equal(["Alpha", "Blender"], session.LibraryOf(Launcher.Id)!.Games.Select(g => g.Title));
        Assert.Equal(1, changes);

        preferences = preferences.WithLibraryOption("tools", false);
        session.ShowChosenTypes();

        Assert.Equal(["Alpha"], session.LibraryOf(Launcher.Id)!.Games.Select(g => g.Title));
        Assert.Equal(1, setup.Launcher.Scanner.Scans);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T228-3")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_uninstall_is_read_again_after_a_moment_unless_the_session_ended_meanwhile(bool endSession)
    {
        var games = new List<Game> { Installed(Launcher, "Alpha", ArcadiaDriver.Now), Installed(Launcher, "Beta", ArcadiaDriver.Now) };
        using var setup = new SessionSetup(games);
        var session = setup.Session;
        await session.LoadAsync();
        var notices = new List<LibraryNotice>();
        var reads = 0;
        session.Noticed += (_, notice) => notices.Add(notice);
        session.LaunchersRead += (_, _) => reads++;

        // The launcher reports the uninstall when it starts; the game leaves its library a moment later.
        setup.Monitor.Finish("Beta", InstallOutcome.Uninstalled);
        await session.PollInstallsAsync();
        Assert.Equal([new LibraryNotice.InstallEnded(InstallOutcome.Uninstalled, "Beta", 0)], notices);
        var readsAtOnce = reads;
        games.RemoveAt(1);

        if (endSession)
        {
            session.Dispose();
        }

        setup.Time.Advance(TimeSpan.FromSeconds(6));
        await setup.SettleAsync();
        if (endSession)
        {
            Assert.Equal(readsAtOnce, reads);
            Assert.Equal(2, session.LibraryOf(Launcher.Id)!.Games.Count);
        }
        else
        {
            Assert.Equal(readsAtOnce + 1, reads);
            Assert.Equal(["Alpha"], session.LibraryOf(Launcher.Id)!.Games.Select(g => g.Title));
        }
    }

    /// <summary>A session over one launcher whose games and install reports the test sets, on a fake clock.</summary>
    private sealed class SessionSetup : IDisposable
    {
        public SessionSetup(IReadOnlyList<Game> games, IReadOnlyList<LibraryTypeOption>? options = null, Func<string, LauncherPreferences>? preferences = null)
        {
            Launcher = new ReportingLauncher(games, Monitor) { LibraryOptions = options ?? [] };
            var registry = new LauncherRegistry([Launcher], () => [LibrarySessionTests.Launcher.Id], preferences);
            var launchedGames = new LaunchedGames();
            Session = new LibrarySession(
                new LibraryService(registry, Time),
                new InstallTracker(registry, Time),
                new RunningGamesTracker(registry, new ProcessGames(new NoProcesses(), launchedGames), Time),
                launchedGames,
                Time);
        }

        public FakeTimeProvider Time { get; } = new(ArcadiaDriver.Now);

        public ReportingMonitor Monitor { get; } = new();

        public ReportingLauncher Launcher { get; }

        public LibrarySession Session { get; }

        /// <summary>Lets the work the session handed to the thread pool and back to the UI thread finish.</summary>
        public async Task SettleAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Session.Dispose();
    }

    private sealed class ReportingLauncher(IReadOnlyList<Game> games, IInstallMonitor monitor) : ILauncherIntegration
    {
        public LauncherInfo Launcher => LibrarySessionTests.Launcher;

        public FixedScanner Scanner { get; } = new(LibrarySessionTests.Launcher, games);

        ILauncherScanner ILauncherIntegration.Scanner => Scanner;

        public IReadOnlyList<LibraryTypeOption> LibraryOptions { get; init; } = [];

        public ILauncherClient? Client => null;

        public IInstallMonitor? InstallMonitor => monitor;

        public IRunningGamesMonitor? RunningGames => null;

        public IReadOnlyCollection<string> ProgramNames => [];

        public string? Locate() => "Test";
    }

    /// <summary>Reports an install's end once, at the next poll, as a launcher's log does.</summary>
    private sealed class ReportingMonitor : IInstallMonitor
    {
        private readonly Dictionary<string, InstallOutcome> _finished = [];

        public string LauncherId => Launcher.Id;

        public void Finish(string gameId, InstallOutcome outcome)
        {
            lock (_finished)
            {
                _finished[gameId] = outcome;
            }
        }

        public InstallSnapshot Poll(CancellationToken cancellationToken = default)
        {
            lock (_finished)
            {
                var finished = new Dictionary<string, InstallOutcome>(_finished);
                _finished.Clear();
                return new InstallSnapshot(new Dictionary<string, InstallProgress>(), finished);
            }
        }
    }
}
