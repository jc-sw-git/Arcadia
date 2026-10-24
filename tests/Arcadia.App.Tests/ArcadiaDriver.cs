using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>
/// Arcadia as <c>App.axaml.cs</c> wires it, without a window: the user's controller is <see cref="Press"/>, the clock is
/// fake, the launchers are <see cref="TestLauncher"/>s and the settings live in a folder of the test's own. The
/// acceptance tests speak only through this class and <see cref="Main"/>, as the user sees it.
/// </summary>
internal sealed class ArcadiaDriver : IDisposable
{
    /// <summary>A Saturday evening, so "zuletzt gespielt" and the clock read the same on every machine.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 24, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Three launchers with page counts that differ at 25 per page: two pages, three pages, one page.</summary>
    public static readonly IReadOnlyList<LauncherSpec> DefaultLaunchers =
    [
        new(KnownLaunchers.Gog, 31),
        new(KnownLaunchers.Xbox, 62),
        new(KnownLaunchers.Amazon, 6),
    ];

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ClockStep = TimeSpan.FromMilliseconds(50);

    private readonly HttpClient _http = new();
    private readonly bool _ownsFolder;

    static ArcadiaDriver()
    {
        // Never into the user's own arcadia.log, also when the tests run on the owner's PC.
        AppLog.Directory = Directory.CreateTempSubdirectory("arcadia-app-tests-log-").FullName;

        // The update service needs Velopack set up, as Program.Main does; not installed, it never updates anything.
        Velopack.VelopackApp.Build().Run();
    }

    private ArcadiaDriver(string folder, bool ownsFolder, IReadOnlyList<LauncherSpec> launchers, FakeTimeProvider time, RecordedStarts starts)
    {
        Folder = folder;
        _ownsFolder = ownsFolder;
        Specs = launchers;
        Time = time;
        Starts = starts;
        Launchers = launchers.Select(spec => new TestLauncher(spec, Now, starts)).ToList();

        Settings = new SettingsService(new SettingsStore(SettingsPath));
        var firstRun = new FirstRunSetup(Settings, demo: false, forced: false);
        var registry = new LauncherRegistry(Launchers, firstRun.ChosenLaunchers, id => Settings.Current.PreferencesOf(id));
        var controllerSupport = new ControllerSupportService(Settings, new NoSisr(_http), _http, time, new NoControllerSupportPlatform());
        var launchedGames = new LaunchedGames();
        var installs = new InstallTracker(registry, time);
        var running = new RunningGamesTracker(registry, new ProcessGames(new NoProcesses(), launchedGames), time);
        Main = new MainViewModel(
            new LibraryService(registry, time),
            new GameLaunchService(registry),
            controllerSupport,
            new ArcadiaUpdateService(_http, time),
            installs,
            running,
            launchedGames,
            Settings,
            new LauncherLogos(Settings, Path.Combine(folder, "logos")),
            firstRun,
            time);
    }

    public string Folder { get; }

    public string SettingsPath => Path.Combine(Folder, "settings.json");

    public IReadOnlyList<LauncherSpec> Specs { get; }

    public FakeTimeProvider Time { get; }

    public RecordedStarts Starts { get; }

    public IReadOnlyList<TestLauncher> Launchers { get; }

    public SettingsService Settings { get; }

    public MainViewModel Main { get; }

    /// <summary>
    /// Starts Arcadia and waits until the library shows. Without <paramref name="settings"/>, the launchers are already
    /// chosen (as after the first start); pass settings without <see cref="AppSettings.ActiveLaunchers"/> for a fresh install.
    /// </summary>
    public static async Task<ArcadiaDriver> StartAsync(IReadOnlyList<LauncherSpec>? launchers = null, AppSettings? settings = null)
    {
        launchers ??= DefaultLaunchers;
        var folder = Directory.CreateTempSubdirectory("arcadia-app-tests-").FullName;
        new SettingsStore(Path.Combine(folder, "settings.json")).Save(settings ?? new AppSettings
        {
            ActiveLaunchers = launchers.Select(spec => spec.Launcher.Id).ToList(),
        });
        var time = new FakeTimeProvider(Now);
        var driver = new ArcadiaDriver(folder, ownsFolder: true, launchers, time, new RecordedStarts());
        await driver.LoadAsync();
        return driver;
    }

    /// <summary>Arcadia closed and started again on the same settings, as the user does after a change.</summary>
    public async Task<ArcadiaDriver> RestartAsync(IReadOnlyList<LauncherSpec>? launchers = null)
    {
        var restarted = new ArcadiaDriver(Folder, ownsFolder: false, launchers ?? Specs, Time, Starts);
        await restarted.LoadAsync();
        return restarted;
    }

    /// <summary>A button pressed and let go at once, like a tap on the controller.</summary>
    public ArcadiaDriver Press(UiAction action, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            Main.HandleGamepad(action);
            Main.Release(action);
            Dispatcher.UIThread.RunJobs();
        }

        return this;
    }

    /// <summary>A button held for <paramref name="duration"/> on the fake clock, then let go.</summary>
    public ArcadiaDriver Hold(UiAction action, TimeSpan duration) => PressDown(action).Wait(duration).LetGo(action);

    /// <summary>A button pressed and kept down, so a test can look at what happens while it is held.</summary>
    public ArcadiaDriver PressDown(UiAction action)
    {
        Main.HandleGamepad(action);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    public ArcadiaDriver LetGo(UiAction action)
    {
        Main.Release(action);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>Time passes on the fake clock.</summary>
    public ArcadiaDriver Wait(TimeSpan duration)
    {
        // In small steps, as real time passes: a timer may start the next one from its callback on the UI thread.
        for (var waited = TimeSpan.Zero; waited < duration; waited += ClockStep)
        {
            Time.Advance(ClockStep);
            Dispatcher.UIThread.RunJobs();
        }

        return this;
    }

    /// <summary>
    /// Waits for work Arcadia hands to the thread pool (a game start, a rescan) to show; fails the test with
    /// <paramref name="what"/> if it never does.
    /// </summary>
    public async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > WaitLimit)
            {
                Assert.Fail($"Nach {WaitLimit.TotalSeconds:0} s noch nicht eingetreten: {what}");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        if (_ownsFolder)
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
                // A settings save still running; the temp folder is cleaned up by the system.
            }
        }
    }

    private async Task LoadAsync()
    {
        await Main.LoadAsync();
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// A launcher of the test: <paramref name="Games"/> games of the demo library, or exactly <paramref name="Library"/> where
/// a test needs to know every title and date. <paramref name="IsInstalled"/> false: the launcher isn't found.
/// </summary>
internal sealed record LauncherSpec(LauncherInfo Launcher, int Games, bool IsInstalled = true, IReadOnlyList<Game>? Library = null)
{
    public static LauncherSpec With(LauncherInfo launcher, params Game[] library) => new(launcher, library.Length, Library: library);
}
