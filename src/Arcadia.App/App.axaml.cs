using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.ControllerSupport;
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

    /// <summary>Set on the restart after an update while Arcadia wasn't in front (e.g. a game was).</summary>
    private const string BackgroundArgument = "--background";

    /// <summary>Hands a SISR pause for a running game over to the Arcadia started after an update.</summary>
    private const string SisrPausedArgument = "--sisr-paused-for";

    /// <summary>Hands a game started from Arcadia over to the Arcadia started after an update, so its end brings Arcadia back (#94).</summary>
    private const string StartedGameArgument = "--started-game";

    /// <summary>View and Menu held alone this long act without waiting for the release, so ≡ held on a search hit still opens its details.</summary>
    private static readonly TimeSpan SoloButtonDelay = TimeSpan.FromMilliseconds(200);

    private IGamepadSource? _gamepad;

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

            // Every launcher is active until #15 lets the user choose.
            var launchers = new LauncherRegistry(
                BuiltInLaunchers.Create(time, demo: args.Contains("--demo", StringComparer.OrdinalIgnoreCase)),
                isEnabled: _ => true);
            foreach (var problem in launchers.Rejected)
            {
                AppLog.Write(problem);
            }

            var settings = new SettingsService(SettingsStore.CreateDefault());
            var controllerSupport = new ControllerSupportService(settings, SisrHost.CreateDefault(http), http, time)
            {
                Log = AppLog.Write,
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
            var runningGames = new RunningGamesTracker(launchers, new ProcessGames(new SystemProcessTable(), launchedGames), time) { Log = AppLog.Write };
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == StartedGameArgument)
                {
                    runningGames.ResumeStartedFromArcadia(args[i + 1]);
                }
            }

            var updates = new ArcadiaUpdateService(http, time) { Log = AppLog.Write };
            var viewModel = new MainViewModel(
                new LibraryService(launchers),
                new GameLaunchService(launchers) { Log = AppLog.Write },
                controllerSupport,
                updates,
                new InstallTracker(launchers, time),
                runningGames,
                launchedGames,
                settings,
                time);
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

            _gamepad = GamepadSource.CreateDefault();
            var mouseMode = new MouseModeHost(_gamepad, settings, window, viewModel, time);

            void Press(GamepadButton button)
            {
                // Checked here, not only on the UI thread: View held in the background brings Arcadia to the
                // front, and the same press must not open the context menu once it got there.
                if (!mouseMode.IsArcadiaActive)
                {
                    return;
                }

                if (UiActionMap.FromGamepad(button) is { } action)
                {
                    AppLog.Write($"Gamepad: {button} → {action}");
                    // Only react while Arcadia is the active window, so playing a game doesn't drive the library.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (mouseMode.IsArcadiaActive)
                        {
                            viewModel.HandleGamepad(action);
                            window.FollowSettingsFocus();
                        }
                    });
                }
            }

            // Releases go through even while another window is in front, so a held A can't get stuck.
            void Release(GamepadButton button)
            {
                if (UiActionMap.FromGamepad(button) is { } action)
                {
                    Dispatcher.UIThread.Post(() => viewModel.Release(action));
                }
            }

            // Back + Start switch the mouse mode, so View and Menu act alone only: on release, or held alone a moment.
            var viewAndMenu = new SoloButtons(GamepadButton.View, GamepadButton.Menu, SoloButtonDelay, time);
            viewAndMenu.Pressed += Press;
            viewAndMenu.Released += Release;
            viewAndMenu.Cancelled += _ => Dispatcher.UIThread.Post(viewModel.CancelHold);
            _gamepad.Polled += _ => viewAndMenu.Tick();
            _gamepad.ButtonPressed += button =>
            {
                // In the mouse mode the controller is a mouse, not Arcadia's navigation.
                if (mouseMode.IsOn)
                {
                    return;
                }

                if (viewAndMenu.Handles(button))
                {
                    viewAndMenu.Press(button);
                }
                else
                {
                    Press(button);
                }
            };
            _gamepad.ButtonReleased += button =>
            {
                if (viewAndMenu.Handles(button))
                {
                    viewAndMenu.Release(button);
                }
                else
                {
                    Release(button);
                }
            };
            _gamepad.Start();

            var clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.Status.UpdateClock());
            clock.Start();

            var monitoring = new DispatcherTimer(ControllerSupportInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => Task.Run(() => controllerSupport.TickAsync()), "SISR-Überwachung"));
            monitoring.Start();

            // Quick check between ticks: Steam closed (stop SISR before it can start Steam again)
            // and the Steam Controller switched on or off (preview). Both checks run on the thread
            // pool: they list processes and read Steam's registry, log and SISR's API; the service's
            // events are posted to the UI thread by their handlers.
            var sisrWatch = new DispatcherTimer(ControllerSupportWatchInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => Task.Run(() => controllerSupport.WatchAsync()), "SISR-Überwachung"));
            sisrWatch.Start();

            // Installs run in the launcher: follow their progress, and for launchers Arcadia can't
            // watch, rescan until the games it handed over are installed.
            var installProgress = new DispatcherTimer(InstallProgressInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.PollInstallsAsync, "Installationen verfolgen"));
            installProgress.Start();
            var installs = new DispatcherTimer(InstallCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.CheckPendingInstallsAsync, "Installationen prüfen"));
            installs.Start();
            var updateCheck = new DispatcherTimer(UpdateCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(updates.CheckIfDueAsync, "Update-Prüfung"));
            updateCheck.Start();

            // Which games run (#95): asked off the UI thread, also in the background (Arcadia may be seen on a second
            // screen); the end of a game Arcadia started brings it back (#94). Coming to the front asks at once.
            var running = new DispatcherTimer(RunningGamesInterval, DispatcherPriority.Background, async (_, _) =>
                await AppErrors.Guard(viewModel.PollRunningGamesAsync, "Laufende Spiele"));
            running.Start();
            window.Activated += (_, _) => viewModel.PollRunningGamesAsync().Forget("Laufende Spiele");
            viewModel.StartedGameEnded += (_, gameKey) => ReturnAfterGameAsync(gameKey).Forget("Nach dem Spiel zurück zu Arcadia");

            async Task ReturnAfterGameAsync(string gameKey)
            {
                await Task.Delay(ReturnAfterGameDelay, time);
                if (mouseMode.IsArcadiaActive)
                {
                    return;
                }

                // The user switched to another program meanwhile: it keeps the front (owner, 2026-10-20).
                var launcherPrograms = launchers.Find(Game.LauncherIdOf(gameKey))?.ProgramNames ?? [];
                if (ForegroundGame.InFrontAfter(gameKey, viewModel.GameFolders, launchedGames, launcherPrograms) is var front and not ForegroundAfterGame.Other)
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
                await controllerSupport.TickAsync();
            }).Forget("SISR-Überwachung");

            desktop.ShutdownRequested += (_, _) =>
            {
                clock.Stop();
                monitoring.Stop();
                sisrWatch.Stop();
                installs.Stop();
                installProgress.Stop();
                updateCheck.Stop();
                running.Stop();
                _gamepad.Dispose();

                // After the polling stopped: lets go of a mouse button or key the mouse mode still holds.
                mouseMode.Dispose();

                // SISR belongs to Arcadia: it goes when Arcadia goes. Run off the UI thread to avoid a deadlock.
                // Except for an update: Arcadia is back in seconds, and a game keeps its controller meanwhile.
                if (updates.IsRestartingForUpdate)
                {
                    AppLog.Write("Neustart für Update: SISR läuft weiter.");
                }
                else
                {
                    Task.Run(() => controllerSupport.ShutdownAsync()).Wait(TimeSpan.FromSeconds(6));
                }
                AppLog.Write("Arcadia beendet.");
            };

            AppLog.Write($"Arcadia gestartet: {string.Join(' ', args)}");
            viewModel.LoadAsync().Forget("Bibliothek laden");
            viewModel.PollRunningGamesAsync().Forget("Laufende Spiele");
            updates.CheckIfDueAsync().Forget("Update-Prüfung");
        }

        base.OnFrameworkInitializationCompleted();
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
            else if (args[i] != BackgroundArgument)
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
