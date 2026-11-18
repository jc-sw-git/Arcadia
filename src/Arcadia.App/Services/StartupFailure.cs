using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;

namespace Arcadia.App.Services;

/// <summary>
/// An unexpected error while Arcadia builds its services and window (#641) must not make it vanish: it goes to the log
/// with its stack trace, and the player gets a small window that names the log. Mouse and keyboard only: no gamepad
/// is running yet at this point.
/// </summary>
public static class StartupFailure
{
    private const double Width = 560;

    public static void Show(IClassicDesktopStyleApplicationLifetime desktop, Exception exception)
    {
        AppLog.Write($"Unerwarteter Fehler beim Start: {exception}");
        try
        {
            var window = Build(Path.Combine(AppLog.Directory, "arcadia.log"));
            window.Closed += (_, _) => desktop.Shutdown();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // A window of the half-built Arcadia would stay behind this one.
            desktop.MainWindow?.Close();
            desktop.MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            // Nothing is left to show it with; the log has both errors.
            AppLog.Write($"Die Startfehler-Meldung konnte nicht angezeigt werden: {ex}");
            desktop.Shutdown(1);
        }
    }

    /// <summary>The window: the message with the log's path, and "Schließen" (also Enter and Esc).</summary>
    internal static Window Build(string logPath)
    {
        var close = new Button { Content = AppTexts.Close, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, IsCancel = true };
        var window = new Window
        {
            Title = "Arcadia",
            Width = Width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = AppTexts.StartupFailed, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new SelectableTextBlock { Text = logPath, TextWrapping = Avalonia.Media.TextWrapping.WrapWithOverflow },
                    close,
                },
            },
        };
        close.Click += (_, _) => window.Close();
        return window;
    }
}
