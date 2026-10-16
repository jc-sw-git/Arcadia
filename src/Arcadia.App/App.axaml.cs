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
using Avalonia;
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

    /// <summary>How often Arcadia asks its update service; the service itself checks GitHub only every few hours.</summary>
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Set on the restart after an update while Arcadia wasn't in front (e.g. a game was).</summary>
    private const string BackgroundArgument = "--background";

    /// <summary>Hands a SISR pause for a running game over to the Arcadia started after an update.</summary>
    private const string SisrPausedArgument = "--sisr-paused-for";

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

            var controllerSupport = new ControllerSupportService(SettingsStore.CreateDefault(), SisrHost.CreateDefault(http), http, time)
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

            var updates = new ArcadiaUpdateService(http, time) { Log = AppLog.Write };
            var viewModel = new MainViewModel(
                new LibraryService(launchers),
                new GameLaunchService(launchers) { Log = AppLog.Write },
                controllerSupport,
                updates,
                new InstallTracker(launchers, time),
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

            // The downloaded update installs once Arcadia has exited; closing normally also stops SISR.
            updates.RestartArguments = () => RestartArguments(args, window.IsActive, controllerSupport.PausedFor);
            updates.RestartRequested += (_, _) => Dispatcher.UIThread.Post(() => desktop.Shutdown());

            var foreground = new ForegroundGuard(window);
            controllerSupport.SisrStarting += (_, _) => Dispatcher.UIThread.Invoke(foreground.Begin);
            controllerSupport.SisrStarted += (_, _) => Dispatcher.UIThread.Post(foreground.End);
            // Tested: Steam keeps SISR's layout after enforce=false + reset until the focus changes.
            controllerSupport.ControllerReleased += (_, _) => Dispatcher.UIThread.Post(() => foreground.BounceFocusAsync().Forget("Controller freigeben"));

            _gamepad = GamepadSource.CreateDefault();
            _gamepad.ButtonPressed += button =>
            {
                if (UiActionMap.FromGamepad(button) is { } action)
                {
                    AppLog.Write($"Gamepad: {button} → {action}");
                    // Only react while Arcadia is the active window, so playing a game doesn't drive the library.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (window.IsActive)
                        {
                            viewModel.Handle(action);
                        }
                    });
                }
            };
            _gamepad.Start();

            var clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.Status.UpdateClock());
            clock.Start();

            var monitoring = new DispatcherTimer(ControllerSupportInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => controllerSupport.TickAsync(), "SISR-Überwachung"));
            monitoring.Start();

            // Quick check between ticks: Steam closed (stop SISR before it can start Steam again)
            // and the Steam Controller switched on or off (preview).
            var sisrWatch = new DispatcherTimer(ControllerSupportWatchInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(() => controllerSupport.WatchAsync(), "SISR-Überwachung"));
            sisrWatch.Start();

            // Installs run in the launcher: follow their progress, and for launchers Arcadia can't
            // watch, rescan until the games it handed over are installed.
            var installProgress = new DispatcherTimer(InstallProgressInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.PollInstallsAsync, "Installationen verfolgen"));
            installProgress.Start();
            var installs = new DispatcherTimer(InstallCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(viewModel.CheckPendingInstallsAsync, "Installationen prüfen"));
            installs.Start();
            var updateCheck = new DispatcherTimer(UpdateCheckInterval, DispatcherPriority.Background, async (_, _) => await AppErrors.Guard(updates.CheckIfDueAsync, "Update-Prüfung"));
            updateCheck.Start();
            Task.Run(SisrHost.RemoveLegacyCopy).ContinueWith(
                _ => controllerSupport.TickAsync(),
                TaskScheduler.FromCurrentSynchronizationContext()).Unwrap().Forget("SISR-Überwachung");

            desktop.ShutdownRequested += (_, _) =>
            {
                clock.Stop();
                monitoring.Stop();
                installs.Stop();
                installProgress.Stop();
                updateCheck.Stop();
                _gamepad.Dispose();

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
            updates.CheckIfDueAsync().Forget("Update-Prüfung");
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Arcadia's own arguments for the start after an update, plus the state to carry over.</summary>
    private static string[] RestartArguments(string[] args, bool inFront, IEnumerable<string> pausedFor)
    {
        var kept = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == SisrPausedArgument)
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

        return [.. kept];
    }
}
