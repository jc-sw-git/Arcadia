using System.Diagnostics;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Win32;

namespace Arcadia.Launchers.Epic;

/// <summary>What <see cref="EpicLauncherClient"/> needs from the system; replaced in tests.</summary>
public interface IEpicLauncherProcesses
{
    bool IsLauncherRunning();

    /// <summary>The launcher and its web helper run; only then does it accept launch requests.</summary>
    bool IsLauncherReady();

    string? FindLauncherExecutable();

    void StartLauncherSilently(string executable);

    void Request(LaunchTarget target);

    bool AnyRunningFrom(string directory);
}

/// <summary>
/// Starts Epic games through the Epic Games Launcher. Games flagged <c>bRequiresAuth</c> get their
/// login token from the launcher, so it has to run. If it doesn't, it is started hidden in the tray
/// (<c>-silent</c>, the same switch Epic's own autostart uses) before the game is requested.
/// </summary>
public sealed class EpicLauncherClient(TimeProvider time, IEpicLauncherProcesses? processes = null) : ILauncherClient
{
    private const string LauncherProcess = "EpicGamesLauncher";
    private const string WebHelperProcess = "EpicWebHelper";

    public static readonly TimeSpan LauncherReadyTimeout = TimeSpan.FromSeconds(60);

    // The web helper appears before the launcher accepts launch requests; give it a moment more.
    private static readonly TimeSpan LauncherSettleTime = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan GameStartTimeout = TimeSpan.FromSeconds(30);

    private readonly IEpicLauncherProcesses _processes = processes ?? new SystemProcesses();

    public string? LauncherId => KnownLaunchers.Epic.Id;

    public async Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        if (game.Launch is not LaunchTarget.ProtocolUri uri)
        {
            await new DirectLauncherClient().StartAsync(game, progress, cancellationToken);
            return;
        }

        var startedLauncher = false;
        var launcherReady = true;
        if (!_processes.IsLauncherRunning() && _processes.FindLauncherExecutable() is { } launcher)
        {
            progress?.Report(LaunchStage.StartingLauncher);
            _processes.StartLauncherSilently(launcher);
            startedLauncher = true;

            launcherReady = await ProcessWatch.WaitUntilAsync(_processes.IsLauncherReady, LauncherReadyTimeout, time, cancellationToken);
            if (launcherReady)
            {
                await Task.Delay(LauncherSettleTime, time, cancellationToken);
            }

            // Not ready is no success: say so, then still ask once, since that often works anyway.
            progress?.Report(launcherReady ? LaunchStage.LauncherReady : LaunchStage.LauncherNotReady);
        }

        if (launcherReady)
        {
            progress?.Report(LaunchStage.Starting);
        }

        _processes.Request(uri);

        if (game.InstallDirectory is not { } directory || !Directory.Exists(directory))
        {
            return;
        }

        bool IsGameRunning() => _processes.AnyRunningFrom(directory);
        var running = await ProcessWatch.WaitUntilAsync(IsGameRunning, GameStartTimeout, time, cancellationToken);

        // A launcher that was just started sometimes drops the first request; ask once more.
        if (!running && startedLauncher)
        {
            _processes.Request(uri);
            running = await ProcessWatch.WaitUntilAsync(IsGameRunning, GameStartTimeout, time, cancellationToken);
        }

        progress?.Report(running ? LaunchStage.Running : LaunchStage.NotConfirmed);
    }

    private sealed class SystemProcesses : IEpicLauncherProcesses
    {
        public bool IsLauncherRunning() => ProcessWatch.IsRunning(LauncherProcess);

        public bool IsLauncherReady() => ProcessWatch.IsRunning(LauncherProcess) && ProcessWatch.IsRunning(WebHelperProcess);

        public string? FindLauncherExecutable() => EpicLauncherClient.FindLauncherExecutable();

        public void StartLauncherSilently(string executable) =>
            Process.Start(new ProcessStartInfo(executable, "-silent") { UseShellExecute = false })?.Dispose();

        public void Request(LaunchTarget target) => DirectLauncherClient.Start(target);

        public bool AnyRunningFrom(string directory) => ProcessWatch.AnyRunningFrom(directory);
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
