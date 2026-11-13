using System.Diagnostics;
using System.Text.RegularExpressions;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers;

/// <summary>
/// Starts a launch target as-is. Used for launchers that need no preparation and as the
/// fallback for every launcher without its own client.
/// </summary>
/// <param name="start">Starts a target; defaults to <see cref="Start"/>.</param>
public sealed partial class DirectLauncherClient(Func<LaunchTarget, bool>? start = null) : ILauncherClient
{
    private readonly Func<LaunchTarget, bool> _start = start ?? Start;

    public string? LauncherId => null;

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(game.Launch is not LaunchTarget.None && !IsStartable(game.Launch) ? LaunchStage.Refused
            : _start(game.Launch) ? LaunchStage.Starting
            : LaunchStage.NotStartable);
        return Task.CompletedTask;
    }

    /// <returns>False if there is nothing to start (demo data) or the target isn't one Arcadia starts (<see cref="IsStartable"/>).</returns>
    public static bool Start(LaunchTarget target)
    {
        if (!IsStartable(target))
        {
            return false;
        }

        switch (target)
        {
            case LaunchTarget.ProtocolUri uri:
                Process.Start(new ProcessStartInfo(uri.Uri) { UseShellExecute = true })?.Dispose();
                return true;

            case LaunchTarget.Executable exe:
                var info = new ProcessStartInfo(exe.Path)
                {
                    UseShellExecute = false,
                    WorkingDirectory = exe.WorkingDirectory ?? Path.GetDirectoryName(exe.Path) ?? string.Empty,
                };
                foreach (var argument in exe.Arguments)
                {
                    info.ArgumentList.Add(argument);
                }

                Process.Start(info)?.Dispose();
                return true;

            case LaunchTarget.ShellAppsFolder app:
                Process.Start(new ProcessStartInfo("explorer.exe", $"shell:appsFolder\\{app.AppUserModelId}") { UseShellExecute = false })?.Dispose();
                return true;

            case LaunchTarget.None:
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown launch target.");
        }
    }

    /// <summary>
    /// Checked here, where the shell gets it, whoever built the target (a plugin, a manifest's value): a protocol URI
    /// must be absolute and not name a file or share (the shell would open or run it), an AUMID only has the characters
    /// Windows allows in one, so nothing can ride along into explorer's argument. Executables are started without the
    /// shell, with their arguments as a list.
    /// </summary>
    public static bool IsStartable(LaunchTarget target) => target switch
    {
        LaunchTarget.ProtocolUri uri => Uri.TryCreate(uri.Uri, UriKind.Absolute, out var parsed) && !parsed.IsFile && !parsed.IsUnc,
        LaunchTarget.ShellAppsFolder app => AppUserModelId().IsMatch(app.AppUserModelId),
        LaunchTarget.None => false,
        _ => true,
    };

    [GeneratedRegex(@"^[A-Za-z0-9._!-]+$")]
    private static partial Regex AppUserModelId();
}
