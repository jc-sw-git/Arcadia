using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Arcadia.App;

public partial class App : Application
{
    /// <summary>How often SISR and its dependencies are checked.</summary>
    private static readonly TimeSpan ControllerSupportInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ControllerSupportWatchInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan InstallCheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InstallProgressInterval = TimeSpan.FromSeconds(2);

    /// <summary>How often Arcadia asks which games run (#95), in front and in the background.</summary>
    private static readonly TimeSpan RunningGamesInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// After a game started from Arcadia ended, Arcadia waits this long before it comes to the front, so the launcher's
    /// windows (Epic's rating) open first and Arcadia lands above them (owner, 2026-10-20).
    /// </summary>
    private static readonly TimeSpan ReturnAfterGameDelay = TimeSpan.FromSeconds(1);

    /// <summary>How often Arcadia asks its update service; the service itself checks GitHub only every few hours.</summary>
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Demo libraries for every launcher without an integration; skips the first start's question and saves no launchers.</summary>
    private const string DemoArgument = "--demo";

    /// <summary>With <see cref="DemoArgument"/>: this many demo games in all, e.g. 5000 to measure a large library.</summary>
    private const string DemoGamesArgument = "--demo-games";

    /// <summary>Shows the first start's question again, even after it was answered (#139), e.g. to try it out.</summary>
    private const string FirstRunArgument = "--first-run";

    /// <summary>Set on the restart after an update while Arcadia wasn't in front (e.g. a game was).</summary>
    private const string BackgroundArgument = "--background";

    /// <summary>Hands a SISR pause for a running game over to the Arcadia started after an update.</summary>
    private const string SisrPausedArgument = "--sisr-paused-for";

    /// <summary>Hands a game started from Arcadia over to the Arcadia started after an update, so its end brings Arcadia back (#94).</summary>
    private const string StartedGameArgument = "--started-game";

    private IGamepadSource? _gamepad;

    /// <summary>Pan fell back to its previous journal or started empty (#243); its log says what was wrong.</summary>

    private const string PanJournalText = "Pan konnte sein Journal nicht lesen – Details stehen in pan.log unter %ProgramData%\\Arcadia\\Pan";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // An error in a UI event handler would otherwise end Arcadia; log it, show it, keep running.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            AppErrors.Report(e.Exception, "Oberfläche");
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var time = TimeProvider.System;
            var http = new HttpClient();

            var settings = new SettingsService(SettingsStore.CreateDefault());
            var demo = args.Contains(DemoArgument, StringComparer.OrdinalIgnoreCase);
            var firstRun = new FirstRunSetup(settings, demo, forced: args.Contains(FirstRunArgument, StringComparer.OrdinalIgnoreCase));

            // The first start asks which launchers are active (#139), the settings change them and their order (#136);
            // while the question shows, every launcher found is read, so it can count their games. Names, keywords and
            // folders the user gave launchers apply from their next read (#137, #138).
            var launchers = new LauncherRegistry(
                BuiltInLaunchers.Create(time, demo, DemoGames(args)),
                firstRun.ChosenLaunchers,
                id => settings.Current.PreferencesOf(id));
            foreach (var problem in launchers.Rejected)
            {
                AppLog.Write(problem);
            }

            var sisrHost = SisrHost.CreateDefault(http);
            // SISR is replaced by Arcadia's own Xbox pad (#213); it stays only for what its install brings until #214.
            var controllerSupport = new ControllerSupportService(settings, sisrHost, http, time)
            {
                Log = AppLog.Write,
                StartsSisr = false,
                GamesUseSteamInput = launchers.GamesUseSteamInput,
            };
            // After an update restart: SISR kept running, and pauses for running games carry over.
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == SisrPausedArgument)
                {
                    controllerSupport.ResumePause(args[i + 1]);
                }
            }

            var launchedGames = new LaunchedGames();
            var installTracker = new InstallTracker(launchers, time);
            var runningGames = new RunningGamesTracker(launchers, new ProcessGames(new SystemProcessTable(), launchedGames), time)
            {
                Log = AppLog.Write,
                IsInstalling = key => installTracker.StateOf(key) is not null,
            };
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == StartedGameArgument)
                {
                    runningGames.ResumeStartedFromArcadia(args[i + 1]);
                }
            }

            var updates = new ArcadiaUpdateService(http, time) { Log = AppLog.Write };
            var viewModel = new MainViewModel(
                new LibraryService(launchers, time),
                new GameLaunchService(launchers) { Log = AppLog.Write },
                controllerSupport,
                updates,
                installTracker,
                runningGames,
                launchedGames,
                settings,
                new LauncherLogos(settings, LauncherLogos.DefaultFolder),
                firstRun,
                time,
                new InstallFolderCheck(Directory.Exists, action => Dispatcher.UIThread.Post(action)));
            var window = new MainWindow { DataContext = viewModel };
            viewModel.ForegroundRequested += (_, _) => window.BringToFront();
            if (args.Contains("--fullscreen", StringComparer.OrdinalIgnoreCase))
            {
                window.WindowState = Avalonia.Controls.WindowState.FullScreen;
            }

            if (args.Contains(BackgroundArgument))
            {
                // Come back behind whatever is in front (a game), as Arcadia was before the update.
                window.ShowActivated = false;
                window.Opened += (_, _) => ForegroundGuard.SendToBack(window);
            }

            desktop.MainWindow = window;

            // The mouse mode's overlay is a window too; Arcadia ends with the main window, not the last one.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // The downloaded update installs once Arcadia has exited; closing normally also stops SISR.
            updates.RestartArguments = () => RestartArguments(args, window.IsActive, controllerSupport.PausedFor, runningGames.StartedFromArcadia);
            updates.RestartRequested += (_, _) => Dispatcher.UIThread.Post(() => desktop.Shutdown());

            var foreground = new ForegroundGuard(window);
            controllerSupport.SisrStarting += (_, _) => Dispatcher.UIThread.Invoke(foreground.Begin);
            controllerSupport.SisrStarted += (_, _) => Dispatcher.UIThread.Post(foreground.End);
            // Tested: Steam keeps SISR's layout after enforce=false + reset until the focus changes.
            controllerSupport.ControllerReleased += (_, _) => Dispatcher.UIThread.Post(() => foreground.BounceFocusAsync().Forget("Controller freigeben"));

            _gamepad = ControllerInput.Create(viewModel);
            var mouseMode = new MouseModeHost(_gamepad, settings, window, viewModel, time);
            viewModel.Dialogs = new SystemDialogs(window, () => viewModel.UsesController, () => mouseMode);

            var routing = new GamepadRouting(viewModel, () => mouseMode.IsArcadiaActive, () => mouseMode.IsOn, time, window.Latency);
            _gamepad.Polled += routing.Tick;

            // The Steam Controller as an Xbox pad for games, in the mode of the running games (#213); VIIPER from SISR's
            // folder until #214. Steam games started directly in Steam count too (#119).
            var viiper = new ViiperXboxPads(Path.Combine(sisrHost.InstallDirectory, "viiper.exe"), Core.Diagnostics.LogLocation.Directory, sisrHost.CleanUpViiperAsync, AppLog.Write);
            // Pan, the guardian service (#243): without it no pad; it keeps the connection itself.
            var pan = new PanClient(OperatingSystem.IsWindows() ? new PanPipeTransport(AppLog.Write) : new NoPanTransport(), time, AppLog.Write);
            var panNotices = new PanNotices(viewModel.Toast, () => mouseMode.IsArcadiaActive, mouseMode.ShowNotice);
            pan.JournalProblem += () => panNotices.Show(PanJournalText);
            controllerSupport.HidingCheck = pan;
            var steamPlatform = new ControllerSupportPlatform();
            // The copy of the Steam Controller for Steam needs the VIIPER fork (#242 parts B and C); until then Pan hides nothing.
            var steamInFront = new SteamInFront(time);
            var forGames = SteamControllerForGames.Create(
                OperatingSystem.IsWindows(), viiper, pan, new NoSteamControllerCopy(), controllerSupport, settings, () => runningGames.ActiveGames, steamPlatform.RunningSteamAppId,
                () => mouseMode.IsArcadiaActive, () => mouseMode.IsOn, steamInFront.Now,
                () => ForegroundGame.InFront(viewModel.Session.GameFolders, launchedGames), panNotices, time);
            pan.Start();
            var xboxPad = forGames.Pad;
            viewModel.BeforeGameStarts = forGames.BeforeGameStartsAsync;
            viewModel.ConnectPan(forGames);
            viewModel.QuitRequested += (_, _) => Dispatcher.UIThread.Post(() => desktop.Shutdown());
            // Pan's question before taking the Steam Controller over, or a firmware update Steam tries through the copy (#242):
            // Arcadia may be behind a game, and the player must answer.
            viewModel.PanDialogShown += (_, _) => ForegroundGuard.BringBack(window);
            _gamepad.Polled += xboxPad.Feed;
            _gamepad.Lost += xboxPad.Drop;
            _gamepad.ButtonPressed += routing.Press;
            _gamepad.ButtonReleased += routing.Release;
            _gamepad.Lost += routing.Drop;
            _gamepad.Start();

            var monitoring = new DispatcherTimer(ControllerSupportInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => Task.Run(() => controllerSupport.TickAsync()), "SISR-Überwachung"));
            monitoring.Start();

            // Quick check between ticks: Steam closed (stop SISR before it can start Steam again)
            // and the Steam Controller switched on or off (preview). Both checks run on the thread
            // pool: they list processes and read Steam's registry, log and SISR's API; the service's
            // events are posted to the UI thread by their handlers.
            var sisrWatch = new DispatcherTimer(ControllerSupportWatchInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => Task.Run(() => controllerSupport.WatchAsync()), "SISR-Überwachung"));
            sisrWatch.Start();
            var xboxPadTick = new DispatcherTimer(ControllerSupportWatchInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => Task.Run(forGames.TickAsync), "Xbox-Pad für Spiele"));

            // Installs run in the launcher: follow their progress, and for launchers Arcadia can't
            // watch, rescan until the games it handed over are installed.
            var installProgress = new DispatcherTimer(InstallProgressInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.Session.PollInstallsAsync, "Installationen verfolgen"));
            installProgress.Start();
            var installs = new DispatcherTimer(InstallCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.Session.CheckPendingInstallsAsync, "Installationen prüfen"));
            installs.Start();
            var updateCheck = new DispatcherTimer(UpdateCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(updates.CheckIfDueAsync, "Update-Prüfung"));
            updateCheck.Start();

            // Which games run (#95): asked off the UI thread, also in the background (Arcadia may be seen on a second
            // screen); the end of a game Arcadia started brings it back (#94). Coming to the front asks at once.
            var running = new DispatcherTimer(RunningGamesInterval, DispatcherPriority.Background, async (_, _) =>
                await AppErrors.Guard(viewModel.Session.PollRunningGamesAsync, "Laufende Spiele"));
            running.Start();
            window.Activated += (_, _) => viewModel.Session.PollRunningGamesAsync().Forget("Laufende Spiele");
            viewModel.Session.StartedGameEnded += (_, gameKey) => ReturnAfterGameAsync(gameKey).Forget("Nach dem Spiel zurück zu Arcadia");

            async Task ReturnAfterGameAsync(string gameKey)
            {
                await Task.Delay(ReturnAfterGameDelay, time);
                if (mouseMode.IsArcadiaActive)
                {
                    return;
                }

                // The user switched to another program meanwhile: it keeps the front (owner, 2026-10-20).
                var launcherPrograms = launchers.Find(Game.LauncherIdOf(gameKey))?.ProgramNames ?? [];
                if (ForegroundGame.InFrontAfter(gameKey, viewModel.Session.GameFolders, launchedGames, launcherPrograms) is var front and not ForegroundAfterGame.Other)
                {
                    AppLog.Write($"{gameKey} beendet, vorne: {front} – Arcadia kommt nach vorne");
                    ForegroundGuard.BringBack(window);
                }
                else
                {
                    AppLog.Write($"{gameKey} beendet, ein anderes Programm ist vorne – Arcadia bleibt hinten");
                }
            }

            Task.Run(async () =>
            {
                SisrHost.RemoveLegacyCopy();

                // Before Arcadia's pad starts VIIPER: stopping SISR cleans up the VIIPER in SISR's folder.
                await controllerSupport.StopReplacedSisrAsync();
                await controllerSupport.TickAsync();
                await forGames.Emulation.TickAsync();
                var route = forGames.Routing.Update();
                var panUpdate = new PanUpdateOffer(controllerSupport, viewModel);
                var panUpdateDue = panUpdate.IsDue();
                Dispatcher.UIThread.Post(() =>
                {
                    xboxPadTick.Start();
                    if (panUpdateDue)
                    {
                        panUpdate.Ask();
                    }
                });
                await xboxPad.TickAsync();
                AppLog.Write($"Steam Controller in Spielen bereit ({route.Why})");
            }).Forget("SISR-Überwachung");

            desktop.ShutdownRequested += (_, _) =>
            {
                monitoring.Stop();
                sisrWatch.Stop();
                xboxPadTick.Stop();
                installs.Stop();
                installProgress.Stop();
                updateCheck.Stop();
                running.Stop();
                _gamepad.Dispose();

                // After the polling stopped: closes the open pages and stops the library's delayed rescans.
                viewModel.Dispose();

                // After the polling stopped: lets go of a mouse button or key the mouse mode still holds.
                mouseMode.Dispose();

                // The Xbox pad goes also for an update: nothing feeds it while Arcadia restarts, and it comes back then.
                // Pan hears the planned exit with it, so it has nothing to undo.
                Task.Run(xboxPad.ShutdownAsync).Wait(TimeSpan.FromSeconds(5));
                Task.Run(() => pan.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(2));

                // SISR belongs to Arcadia: it goes when Arcadia goes. Run off the UI thread to avoid a deadlock.
                // Except for an update: Arcadia is back in seconds, and a game keeps its controller meanwhile.
                if (updates.IsRestartingForUpdate)
                {
                    AppLog.Write("Neustart für Update: SISR läuft weiter.");
                }
                else
                {
                    // Its own limit is 3 s before it kills SISR; the rest is for the kill and the clean-up.
                    Task.Run(() => controllerSupport.ShutdownAsync()).Wait(TimeSpan.FromSeconds(8));
                }
                AppLog.Write("Arcadia beendet.");
            };

            // Version and Windows build first: a bug report with only the log must say what it is about.
            AppLog.Write($"Arcadia {updates.CurrentVersion} gestartet ({System.Runtime.InteropServices.RuntimeInformation.OSDescription}, "
                + $"{(updates.IsInstalled ? "installiert" : "nicht installiert")}): {string.Join(' ', args)}");
            viewModel.Session.LoadAsync().Forget("Bibliothek laden");
            viewModel.Session.PollRunningGamesAsync().Forget("Laufende Spiele");
            updates.CheckIfDueAsync().Forget("Update-Prüfung");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static int? DemoGames(string[] args)
    {
        var at = Array.FindIndex(args, a => a.Equals(DemoGamesArgument, StringComparison.OrdinalIgnoreCase));
        return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
            && count is > 0 and <= 100_000
            ? count
            : null;
    }

    /// <summary>Arcadia's own arguments for the start after an update, plus the state to carry over.</summary>
    private static string[] RestartArguments(string[] args, bool inFront, IEnumerable<string> pausedFor, IEnumerable<string> startedGames)
    {
        var kept = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is SisrPausedArgument or StartedGameArgument)
            {
                i++;
            }
            // --first-run asked once; the start after an update must not ask again.
            else if (args[i] != BackgroundArgument && !args[i].Equals(FirstRunArgument, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(args[i]);
            }
        }

        if (!inFront)
        {
            kept.Add(BackgroundArgument);
        }

        foreach (var directory in pausedFor)
        {
            kept.Add(SisrPausedArgument);
            kept.Add(directory);
        }

        foreach (var gameKey in startedGames)
        {
            kept.Add(StartedGameArgument);
            kept.Add(gameKey);
        }

        return [.. kept];
    }
}
