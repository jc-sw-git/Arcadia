using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Demo;
using Arcadia.Launchers.Epic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Arcadia.App;

public partial class App : Application
{
    /// <summary>How often SISR and its dependencies are checked.</summary>
    private static readonly TimeSpan ControllerSupportInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan InstallCheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InstallProgressInterval = TimeSpan.FromSeconds(2);

    /// <summary>How often Arcadia asks its update service; the service itself checks GitHub only every few hours.</summary>
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(30);

    private IGamepadSource? _gamepad;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var time = TimeProvider.System;
            var http = new HttpClient();

            var scanners = new List<ILauncherScanner> { new EpicScanner() };
            if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
            {
                scanners.AddRange(DemoScanner.DefaultSet(time.GetLocalNow()));
            }

            var controllerSupport = new ControllerSupportService(SettingsStore.CreateDefault(), SisrHost.CreateDefault(http), http, time)
            {
                Log = AppLog.Write,
            };
            var updates = new ArcadiaUpdateService(time) { Log = AppLog.Write };
            var viewModel = new MainViewModel(
                new LibraryService(scanners),
                GameLaunchService.CreateDefault(time),
                controllerSupport,
                updates,
                [new EpicInstallMonitor()],
                time);
            var window = new MainWindow { DataContext = viewModel };
            viewModel.ForegroundRequested += (_, _) => window.BringToFront();
            if (args.Contains("--fullscreen", StringComparer.OrdinalIgnoreCase))
            {
                window.WindowState = Avalonia.Controls.WindowState.FullScreen;
            }

            desktop.MainWindow = window;

            // The downloaded update installs once Arcadia has exited; closing normally also stops SISR.
            updates.RestartRequested += (_, _) => Dispatcher.UIThread.Post(() => desktop.Shutdown());

            var foreground = new ForegroundGuard(window);
            controllerSupport.SisrStarting += (_, _) => Dispatcher.UIThread.Invoke(foreground.Begin);
            controllerSupport.SisrStarted += (_, _) => Dispatcher.UIThread.Post(foreground.End);
            // Tested: Steam keeps SISR's layout after enforce=false + reset until the focus changes.
            controllerSupport.ControllerReleased += (_, _) => Dispatcher.UIThread.Post(() => _ = foreground.BounceFocusAsync());

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

            var clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.UpdateClock());
            clock.Start();

            var monitoring = new DispatcherTimer(ControllerSupportInterval, DispatcherPriority.Background, async (_, _) => await controllerSupport.TickAsync());
            monitoring.Start();

            // Installs run in the launcher: follow their progress, and for launchers Arcadia can't
            // watch, rescan until the games it handed over are installed.
            var installProgress = new DispatcherTimer(InstallProgressInterval, DispatcherPriority.Background, async (_, _) => await viewModel.PollInstallsAsync());
            installProgress.Start();
            var installs = new DispatcherTimer(InstallCheckInterval, DispatcherPriority.Background, async (_, _) => await viewModel.CheckPendingInstallsAsync());
            installs.Start();
            var updateCheck = new DispatcherTimer(UpdateCheckInterval, DispatcherPriority.Background, async (_, _) => await updates.CheckIfDueAsync());
            updateCheck.Start();
            _ = Task.Run(SisrHost.RemoveLegacyCopy).ContinueWith(
                _ => controllerSupport.TickAsync(),
                TaskScheduler.FromCurrentSynchronizationContext());

            desktop.ShutdownRequested += (_, _) =>
            {
                clock.Stop();
                monitoring.Stop();
                installs.Stop();
                installProgress.Stop();
                updateCheck.Stop();
                _gamepad.Dispose();

                // SISR belongs to Arcadia: it goes when Arcadia goes. Run off the UI thread to avoid a deadlock.
                Task.Run(() => controllerSupport.ShutdownAsync()).Wait(TimeSpan.FromSeconds(6));
                AppLog.Write("Arcadia beendet.");
            };

            AppLog.Write($"Arcadia gestartet: {string.Join(' ', args)}");
            _ = viewModel.LoadAsync();
            _ = updates.CheckIfDueAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
