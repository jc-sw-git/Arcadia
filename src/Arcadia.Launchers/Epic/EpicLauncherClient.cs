using System.Diagnostics;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Win32;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Starts Epic games through the Epic Games Launcher. Games flagged <c>bRequiresAuth</c> get their
/// login token from the launcher, so it has to run. If it doesn't, it is started hidden in the tray
/// (<c>-silent</c>, the same switch Epic's own autostart uses) before the game is requested.
/// </summary>
public sealed class EpicLauncherClient(TimeProvider time) : ILauncherClient
{
    private const string LauncherProcess = "EpicGamesLauncher";
    private const string WebHelperProcess = "EpicWebHelper";

    private static readonly TimeSpan LauncherReadyTimeout = TimeSpan.FromSeconds(60);

    // The web helper appears before the launcher accepts launch requests; give it a moment more.
    private static readonly TimeSpan LauncherSettleTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan GameStartTimeout = TimeSpan.FromSeconds(30);

    public string? LauncherId => KnownLaunchers.Epic.Id;

    public async Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        if (game.Launch is not LaunchTarget.ProtocolUri uri)
        {
            await new DirectLauncherClient().StartAsync(game, progress, cancellationToken);
            return;
        }

        var startedLauncher = false;
        if (!ProcessWatch.IsRunning(LauncherProcess) && FindLauncherExecutable() is { } launcher)
        {
            progress?.Report(LaunchStage.StartingLauncher);
            Process.Start(new ProcessStartInfo(launcher, "-silent") { UseShellExecute = false })?.Dispose();
            startedLauncher = true;

            await ProcessWatch.WaitUntilAsync(
                () => ProcessWatch.IsRunning(LauncherProcess) && ProcessWatch.IsRunning(WebHelperProcess),
                LauncherReadyTimeout,
                time,
                cancellationToken);
            await Task.Delay(LauncherSettleTime, time, cancellationToken);
            progress?.Report(LaunchStage.LauncherReady);
        }

        progress?.Report(LaunchStage.Starting);
        DirectLauncherClient.Start(uri);

        if (game.InstallDirectory is not { } directory || !Directory.Exists(directory))
        {
            return;
        }

        bool IsGameRunning() => ProcessWatch.AnyRunningFrom(directory);
        var running = await ProcessWatch.WaitUntilAsync(IsGameRunning, GameStartTimeout, time, cancellationToken);

        // A launcher that was just started sometimes drops the first request; ask once more.
        if (!running && startedLauncher)
        {
            DirectLauncherClient.Start(uri);
            running = await ProcessWatch.WaitUntilAsync(IsGameRunning, GameStartTimeout, time, cancellationToken);
        }

        progress?.Report(running ? LaunchStage.Running : LaunchStage.NotConfirmed);
    }

    /// <summary>
    /// The launcher executable, read from the <c>com.epicgames.launcher://</c> protocol registration.
    /// </summary>
    public static string? FindLauncherExecutable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var key = Registry.ClassesRoot.OpenSubKey(@"com.epicgames.launcher\shell\open\command");
        var command = key?.GetValue(null) as string;
        var path = ParseExecutable(command);
        if (path is not null && File.Exists(path))
        {
            return path;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe");
        return File.Exists(fallback) ? fallback : null;
    }

    /// <summary>Extracts the executable from a shell command like <c>"C:\…\x.exe" %1</c>.</summary>
    public static string? ParseExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = command.Trim();
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? text[..(exe + 4)] : text.Split(' ')[0];
    }
}
