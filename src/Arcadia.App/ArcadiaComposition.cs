using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App;

/// <summary>
/// What Arcadia's object graph is built from: the parts that differ between the App (Windows, the real disk, the
/// network) and the acceptance tests (fakes).
/// </summary>
/// <param name="Integrations">The launchers Arcadia knows; the registry keeps those the user activated.</param>
/// <param name="ControllerPlatform">The PC's drivers, Pan and Steam; null: the real one.</param>
/// <param name="Viiper">Arcadia's own <c>viiper.exe</c> (#242 part B); null in a build without one.</param>
/// <param name="Sisr">What the old Arcadia left of SISR (#214); null on other platforms.</param>
/// <param name="LogosFolder">Where the launcher logos the user chose are kept.</param>
internal sealed record ArcadiaParts(
    SettingsService Settings,
    HttpClient Http,
    TimeProvider Time,
    FirstRunSetup FirstRun,
    IReadOnlyList<ILauncherIntegration> Integrations,
    IControllerSupportPlatform? ControllerPlatform,
    IViiperInstallation? Viiper,
    SisrInstallation? Sisr,
    IProcessTable Processes,
    ArcadiaUpdateService Updates,
    string LogosFolder,
    InstallFolderCheck FolderCheck,
    WebPages WebPages)
{
    /// <summary>Opens a launcher's URI or program; null: the system's shell.</summary>
    public Func<LaunchTarget, bool>? Open { get; init; }
}

/// <summary>What the controllers are wired to: the parts that need SDL, VIIPER, Pan's pipe or a window in the App.</summary>
/// <param name="PanSupported">Pan can run here (Windows); off, the emulation never starts.</param>
/// <param name="HidingCheck">Pan's answer whether it could hide the Steam Controller now (#243 part B2); in the App the same client as <paramref name="Guardian"/>.</param>
/// <param name="LetGoOfPan">Closes Arcadia's connection to Pan once it must leave Pan alone (#366); in the App the same client as <paramref name="Guardian"/>.</param>
/// <param name="Copy">Makes the copy of the Steam Controller for Steam (#242) from the wiring the App hands it.</param>
/// <param name="RunningSteamAppId">Steam's <c>RunningAppID</c>: a Steam game started directly in Steam (#119).</param>
/// <param name="IsArcadiaActive">Arcadia's window is in front.</param>
/// <param name="IsSteamInFront">A window of Steam itself is in front (#242).</param>
/// <param name="GameInFront">The library game in front, or null.</param>
internal sealed record ControllerWiring(
    IGamepadSource Gamepad,
    bool PanSupported,
    IVirtualXboxPadServer Pads,
    IEmulationGuardian Guardian,
    IHidingCheck HidingCheck,
    Func<CancellationToken, Task> LetGoOfPan,
    Func<SteamControllerCopyWiring, ISteamControllerHold> Copy,
    Func<int?> RunningSteamAppId,
    Func<bool> IsArcadiaActive,
    Func<bool> IsMouseModeOn,
    Func<bool> IsSteamInFront,
    Func<string?> GameInFront,
    PanNotices Notices,
    InputLatency Latency)
{
    /// <summary>The pad models Arcadia emulates itself, so its own pads aren't read as the user's (#212).</summary>
    public OwnPadModels? OwnPads { get; init; }
}

/// <summary>
/// Arcadia's object graph, built in one place for <c>App.axaml.cs</c> and the acceptance tests' <c>ArcadiaDriver</c>,
/// so the tests drive what the App runs (#374). What only a window or Windows has (timers, dialogs, the mouse mode, the
/// Pan pipe, VIIPER) the caller makes and passes in, as <see cref="ArcadiaParts"/> and <see cref="ControllerWiring"/>.
/// </summary>
internal sealed class ArcadiaComposition
{
    private ArcadiaComposition(LauncherRegistry launchers, ControllerSupportService controllerSupport, LaunchedGames launchedGames,
        RunningGamesTracker runningGames, MainViewModel main, TimeProvider time, SettingsService settings)
    {
        Launchers = launchers;
        ControllerSupport = controllerSupport;
        LaunchedGames = launchedGames;
        RunningGames = runningGames;
        Main = main;
        _time = time;
        _settings = settings;
    }

    private readonly TimeProvider _time;
    private readonly SettingsService _settings;

    public LauncherRegistry Launchers { get; }

    public ControllerSupportService ControllerSupport { get; }

    public LaunchedGames LaunchedGames { get; }

    public RunningGamesTracker RunningGames { get; }

    public MainViewModel Main { get; }

    /// <summary>
    /// The launchers, the controller emulation's setup, the game trackers and the view model. Ends as the App does before
    /// its window shows: an installed Pan that isn't this Arcadia's shows its update page instead of the library (#297).
    /// </summary>
    public static ArcadiaComposition Build(ArcadiaParts parts)
    {
        var settings = parts.Settings;
        var time = parts.Time;

        // The first start asks which launchers are active (#139), the settings change them and their order (#136);
        // while the question shows, every launcher found is read, so it can count their games. Names, keywords and
        // folders the user gave launchers apply from their next read (#137, #138).
        var launchers = new LauncherRegistry(parts.Integrations, parts.FirstRun.ChosenLaunchers, id => settings.Current.PreferencesOf(id));
        foreach (var problem in launchers.Rejected)
        {
            AppLog.Write(problem);
        }

        // SISR is replaced by Arcadia's own Xbox pad (#213); Arcadia never starts it and removes it only on request (#214).
        var controllerSupport = new ControllerSupportService(settings, parts.Http, time, parts.ControllerPlatform, parts.Sisr)
        {
            Log = AppLog.Write,
            GamesUseSteamInput = launchers.GamesUseSteamInput,
            Viiper = parts.Viiper,
        };

        var launchedGames = new LaunchedGames();
        var installTracker = new InstallTracker(launchers, time) { Log = AppLog.Write };
        var runningGames = new RunningGamesTracker(launchers, new ProcessGames(parts.Processes, launchedGames), time)
        {
            Log = AppLog.Write,
            IsInstalling = key => installTracker.StateOf(key) is not null,
        };

        var main = new MainViewModel(
            new LibraryService(launchers, time) { Log = AppLog.Write },
            new GameLaunchService(launchers.ClientOf, parts.Open) { Log = AppLog.Write },
            controllerSupport,
            parts.Updates,
            installTracker,
            runningGames,
            launchedGames,
            settings,
            new LauncherLogos(settings, parts.LogosFolder),
            parts.FirstRun,
            time,
            parts.FolderCheck);
        main.ShowPanUpdateIfDue();
        parts.WebPages.Attach(main);
        return new ArcadiaComposition(launchers, controllerSupport, launchedGames, runningGames, main, time, settings);
    }

    /// <summary>
    /// The controllers: Arcadia's own navigation (<see cref="GamepadRouting"/>) and the Steam Controller as an Xbox pad
    /// for games, in the mode of the running games (#213); Steam games started directly in Steam count too (#119).
    /// Subscribes both to <see cref="ControllerWiring.Gamepad"/>; the caller starts it.
    /// </summary>
    public (GamepadRouting Routing, SteamControllerForGames ForGames) ConnectControllers(ControllerWiring wiring)
    {
        var gamepad = wiring.Gamepad;
        var routing = new GamepadRouting(Main, wiring.IsArcadiaActive, wiring.IsMouseModeOn, _time, wiring.Latency);
        gamepad.Polled += routing.Tick;

        ControllerSupport.HidingCheck = wiring.HidingCheck;
        ControllerSupport.LetGoOfPan = wiring.LetGoOfPan;
        var forGames = SteamControllerForGames.Create(
            wiring.PanSupported, wiring.Pads, wiring.Guardian, wiring.Copy, ControllerSupport, _settings, () => RunningGames.ActiveGames,
            wiring.RunningSteamAppId, wiring.IsArcadiaActive, wiring.IsMouseModeOn, wiring.IsSteamInFront, wiring.GameInFront, gamepad,
            wiring.Notices, _time, wiring.OwnPads);
        Main.BeforeGameStarts = forGames.BeforeGameStartsAsync;
        Main.ConnectPan(forGames);
        ControllerSupport.PanHoldsPuck = () => forGames.Emulation.HidesPuck;

        gamepad.Sampled += forGames.Pad.Feed;
        gamepad.Lost += forGames.Pad.Drop;
        gamepad.Sampled += forGames.Others.Feed;
        gamepad.Lost += forGames.Others.Drop;
        gamepad.ButtonPressed += routing.Press;
        gamepad.ButtonReleased += routing.Release;
        gamepad.Lost += routing.Drop;
        if (gamepad is NullGamepadSource)
        {
            // No controllers on this system: the hints show the keyboard from the start (#215).
            routing.Drop();
        }

        return (routing, forGames);
    }
}
