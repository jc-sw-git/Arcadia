using Arcadia.App.Input;
using Arcadia.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>
/// Arcadia as <c>App.axaml.cs</c> wires it, without a window: the user's controller is <see cref="Press"/>, the clock is
/// fake, the launchers are <see cref="TestLauncher"/>s (or the real Steam and Epic over files of the test,
/// <see cref="StartWithLaunchersAsync"/>) and the settings live in a folder of the test's own. The acceptance tests speak
/// only through this class and <see cref="Main"/>, as the user sees it.
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

    /// <summary>How often the App reads the controllers (XInputGamepadSource).</summary>
    private static readonly TimeSpan PadPoll = TimeSpan.FromMilliseconds(8);

    private readonly HttpClient _http;
    private readonly bool _ownsFolder;
    private readonly Fixtures.LauncherFiles? _files;
    private readonly List<LaunchTarget> _opened;
    private readonly GamepadRouting _gamepad;

    static ArcadiaDriver()
    {
        // Never into the user's own arcadia.log, also when the tests run on the owner's PC.
        AppLog.Directory = Directory.CreateTempSubdirectory("arcadia-app-tests-log-").FullName;

        // The update service needs Velopack set up, as Program.Main does; not installed, it never updates anything.
        Velopack.VelopackApp.Build().Run();
    }

    private readonly GamepadPolls _pad = new();

    private ArcadiaDriver(
        string folder, bool ownsFolder, IReadOnlyList<LauncherSpec> launchers, FakeTimeProvider time, RecordedStarts starts,
        LauncherInstalls installs, FakeSteam? steam, FakeReleases? releases, Fixtures.LauncherFiles? files = null, List<LaunchTarget>? opened = null)
    {
        Folder = folder;
        Steam = steam;
        Releases = releases;
        _http = new HttpClient(new FakeNetwork(steam?.Sisr, releases));
        steam?.Sisr.UseNetwork(_http);
        _ownsFolder = ownsFolder;
        _files = files;
        _opened = opened ?? [];
        Specs = launchers;
        Time = time;
        Starts = starts;
        Installs = installs;
        Launchers = launchers.Select(spec => spec.IsDemo
            ? new DemoIntegration(spec.Launcher, spec.Games, Now)
            : (ILauncherIntegration)new TestLauncher(spec, Now, starts, installs)).ToList();
        _pad.ButtonPressed += button => PressButton(button);
        _pad.ButtonReleased += button => ReleaseButton(button);

        Settings = new SettingsService(new SettingsStore(SettingsPath));
        var firstRun = new FirstRunSetup(Settings, demo: files?.Demo ?? false, forced: false);
        IReadOnlyList<ILauncherIntegration> integrations = files is null
            ? Launchers
            : BuiltInLaunchers.Create(time, files.Demo, folders: files.Folders);
        var registry = new LauncherRegistry(integrations, firstRun.ChosenLaunchers, id => Settings.Current.PreferencesOf(id));
        ControllerSupport = (steam, files?.ControllerSupport) switch
        {
            ({ } fake, _) => new ControllerSupportService(Settings, fake.Sisr, _http, time, fake) { Log = AppLog.Write },
            (_, { } steamPc) => new ControllerSupportService(Settings, steamPc.Sisr, _http, time, steamPc) { Log = AppLog.Write },
            _ => new ControllerSupportService(Settings, new NoSisr(_http), _http, time, new NoControllerSupportPlatform()) { Log = AppLog.Write },
        };
        Updates = releases is null ? new ArcadiaUpdateService(_http, time) : new ArcadiaUpdateService(_http, time, releases.Manager(folder));
        Updates.Log = AppLog.Write;
        var launchedGames = new LaunchedGames();
        var installTracker = new InstallTracker(registry, time);
        var running = new RunningGamesTracker(registry, new ProcessGames(new NoProcesses(), launchedGames), time)
        {
            Log = AppLog.Write,
            IsInstalling = key => installTracker.StateOf(key) is not null,
        };
        Main = new MainViewModel(
            new LibraryService(registry, time),
            new GameLaunchService(registry, Open) { Log = AppLog.Write },
            ControllerSupport,
            Updates,
            installTracker,
            running,
            launchedGames,
            Settings,
            new LauncherLogos(Settings, Path.Combine(folder, "logos")),
            firstRun,
            time);
        _gamepad = new GamepadRouting(Main, isArcadiaActive: () => IsArcadiaInFront, isMouseModeOn: () => IsMouseModeOn, time);
    }

    public string Folder { get; }

    public string SettingsPath => Path.Combine(Folder, "settings.json");

    public IReadOnlyList<LauncherSpec> Specs { get; }

    public FakeTimeProvider Time { get; }

    public RecordedStarts Starts { get; }

    /// <summary>What the launchers install in their own windows; kept across a restart, as the launchers keep running.</summary>
    public LauncherInstalls Installs { get; }

    public IReadOnlyList<ILauncherIntegration> Launchers { get; }

    /// <summary>The App's <c>arcadia.log</c> as far as written; the tests share one log, so look for what is a test's own.</summary>
    public static string Log
    {
        get
        {
            var path = Path.Combine(AppLog.Directory, "arcadia.log");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new StreamReader(stream).ReadToEnd();
        }
    }

    /// <summary>What Arcadia handed to a launcher (install, uninstall, a game start of a launcher without client), in order.</summary>
    public IReadOnlyList<LaunchTarget> Opened
    {
        get
        {
            lock (_opened)
            {
                return [.. _opened];
            }
        }
    }

    public SettingsService Settings { get; }

    public MainViewModel Main { get; }

    /// <summary>Windows, Steam and SISR of the test; null: a PC without them, as on Linux.</summary>
    public FakeSteam? Steam { get; }

    /// <summary>The releases on GitHub; null: Arcadia runs from bin/, not installed.</summary>
    public FakeReleases? Releases { get; }

    public ControllerSupportService ControllerSupport { get; }

    /// <summary>False: a game or another window is in front, and the controller's buttons leave Arcadia alone.</summary>
    public bool IsArcadiaInFront { get; set; } = true;

    /// <summary>True: the controller is a mouse, and its buttons don't drive Arcadia's navigation.</summary>
    public bool IsMouseModeOn { get; set; }

    public ArcadiaUpdateService Updates { get; }

    /// <summary>
    /// Starts Arcadia and waits until the library shows. Without <paramref name="settings"/>, the launchers are already
    /// chosen (as after the first start); pass settings without <see cref="AppSettings.ActiveLaunchers"/> for a fresh install.
    /// </summary>
    public static async Task<ArcadiaDriver> StartAsync(
        IReadOnlyList<LauncherSpec>? launchers = null, AppSettings? settings = null, FakeSteam? steam = null, FakeReleases? releases = null)
    {
        launchers ??= DefaultLaunchers;
        var folder = Directory.CreateTempSubdirectory("arcadia-app-tests-").FullName;
        new SettingsStore(Path.Combine(folder, "settings.json")).Save(settings ?? new AppSettings
        {
            ActiveLaunchers = launchers.Select(spec => spec.Launcher.Id).ToList(),
        });
        var time = new FakeTimeProvider(Now);
        var driver = new ArcadiaDriver(folder, ownsFolder: true, launchers, time, new RecordedStarts(), new LauncherInstalls(), steam, releases);
        await driver.LoadAsync();
        return driver;
    }

    /// <summary>
    /// Starts Arcadia with the real launchers of <see cref="BuiltInLaunchers"/>, Steam and Epic reading the files in
    /// <paramref name="files"/> instead of the PC's, and waits until the library shows. Without <paramref name="settings"/>,
    /// the launchers the test installed are already chosen.
    /// </summary>
    public static async Task<ArcadiaDriver> StartWithLaunchersAsync(Fixtures.LauncherFiles files, AppSettings? settings = null)
    {
        var folder = Directory.CreateTempSubdirectory("arcadia-app-tests-").FullName;
        new SettingsStore(Path.Combine(folder, "settings.json")).Save(settings ?? new AppSettings
        {
            ActiveLaunchers = new[] { (KnownLaunchers.Steam.Id, files.Folders.SteamRoot), (KnownLaunchers.Epic.Id, files.Folders.Epic.Data) }
                .Where(launcher => Directory.Exists(launcher.Item2))
                .Select(launcher => launcher.Item1)
                .ToList(),
        });
        var driver = new ArcadiaDriver(folder, ownsFolder: true, [], new FakeTimeProvider(Now), new RecordedStarts(), new LauncherInstalls(), steam: null, releases: null, files);
        await driver.LoadAsync();
        return driver;
    }

    /// <summary>Arcadia closed and started again on the same settings, as the user does after a change.</summary>
    public async Task<ArcadiaDriver> RestartAsync(IReadOnlyList<LauncherSpec>? launchers = null)
    {
        var restarted = new ArcadiaDriver(Folder, ownsFolder: false, launchers ?? Specs, Time, Starts, Installs, Steam, Releases, _files, _opened);
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

    /// <summary>
    /// A controller button pressed down, through the same routing as the real controller: ⧉ and ≡ act only alone (on
    /// release or held alone a moment), so a test can press both together. Let go with <see cref="ReleaseButton"/>.
    /// </summary>
    public ArcadiaDriver PressButton(GamepadButton button)
    {
        _gamepad.Press(button);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    public ArcadiaDriver ReleaseButton(GamepadButton button)
    {
        _gamepad.Release(button);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>A click on something the controller can't reach, e.g. a symbol in the header (#166).</summary>
    public ArcadiaDriver Click(System.Windows.Input.ICommand command)
    {
        Assert.True(command.CanExecute(null), "Der Klick wirkt nicht: der Befehl ist gesperrt.");
        command.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>The controller support's regular check (every few seconds in the App), e.g. SISR's update or Steam's login.</summary>
    public async Task TickControllerSupportAsync()
    {
        await Task.Run(() => ControllerSupport.TickAsync());
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Lets time pass on the fake clock until <paramref name="condition"/> holds, for work that waits on the clock off the UI
    /// thread (Steam starting, SISR coming up); fails the test with <paramref name="what"/> after <paramref name="limit"/>.
    /// </summary>
    public async Task WaitOnClockUntilAsync(Func<bool> condition, string what, TimeSpan limit)
    {
        for (var waited = TimeSpan.Zero; !condition(); waited += ClockStep)
        {
            if (waited > limit)
            {
                Assert.Fail($"Nach {limit.TotalSeconds:0} s auf der Uhr noch nicht eingetreten: {what}");
            }

            Wait(ClockStep);
            await Task.Delay(1);
            Dispatcher.UIThread.RunJobs();
        }
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

    /// <summary>
    /// The controller held as <paramref name="buttons"/> and <paramref name="leftStick"/> say for
    /// <paramref name="duration"/>, then let go: read every 8 ms as the App reads it, held directions repeat.
    /// </summary>
    public ArcadiaDriver HoldPad(TimeSpan duration, PadButtons buttons = PadButtons.None, StickPosition leftStick = default)
    {
        var held = new GamepadReading(0, buttons, leftStick, default);
        for (var waited = TimeSpan.Zero; waited <= duration; waited += PadPoll)
        {
            _pad.Update([held], (long)Time.GetElapsedTime(0).TotalMilliseconds);
            _gamepad.Tick();
            Time.Advance(PadPoll);
            Dispatcher.UIThread.RunJobs();
        }

        _pad.Update([new GamepadReading(0, PadButtons.None, default, default)], (long)Time.GetElapsedTime(0).TotalMilliseconds);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>What the App's timer does every few seconds: asks the launchers which games run.</summary>
    public async Task FollowRunningGamesAsync()
    {
        await Main.PollRunningGamesAsync();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Time passes on the fake clock.</summary>
    public ArcadiaDriver Wait(TimeSpan duration)
    {
        // In small steps, as real time passes: a timer may start the next one from its callback on the UI thread.
        for (var waited = TimeSpan.Zero; waited < duration; waited += ClockStep)
        {
            Time.Advance(ClockStep);
            _gamepad.Tick();
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

    /// <summary>
    /// The timer of <c>App.axaml.cs</c> that follows installations ticks (every 2 s): the launchers' install monitors are
    /// read, the covers updated and a finished installation reported.
    /// </summary>
    public async Task<ArcadiaDriver> FollowInstallsAsync()
    {
        Wait(InstallProgressInterval);
        await Main.PollInstallsAsync();
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>The user comes back to Arcadia's window, e.g. from a launcher: <c>MainWindow</c> rescans the library.</summary>
    public async Task<ArcadiaDriver> ActivateWindowAsync()
    {
        // The rescan skips if the last one was only seconds ago; coming back from a launcher takes longer.
        Wait(TimeSpan.FromSeconds(10));
        await Main.RefreshLibraryAsync();
        Dispatcher.UIThread.RunJobs();
        return this;
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

    private static readonly TimeSpan InstallProgressInterval = TimeSpan.FromSeconds(2);

    /// <summary>Records what Arcadia hands to a launcher; a demo game's <see cref="LaunchTarget.None"/> opens nothing, as on the PC.</summary>
    private bool Open(LaunchTarget target)
    {
        if (target is LaunchTarget.None)
        {
            return false;
        }

        lock (_opened)
        {
            _opened.Add(target);
        }

        return true;
    }

    /// <summary>As the App starts: the library, and which games already run.</summary>
    private async Task LoadAsync()
    {
        await Main.LoadAsync();
        await Main.PollRunningGamesAsync();
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// A launcher of the test: <paramref name="Games"/> games of the demo library, or exactly <paramref name="Library"/> where
/// a test needs to know every title and date. <paramref name="IsInstalled"/> false: the launcher isn't found.
/// <paramref name="IsDemo"/>: Arcadia's own demo data (<c>--demo</c>), whose games can't be started and of which one runs.
/// </summary>
internal sealed record LauncherSpec(LauncherInfo Launcher, int Games, bool IsInstalled = true, IReadOnlyList<Game>? Library = null, bool IsDemo = false)
{
    public static LauncherSpec With(LauncherInfo launcher, params Game[] library) => new(launcher, library.Length, Library: library);
}
