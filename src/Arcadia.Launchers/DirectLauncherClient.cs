using System.Diagnostics;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers;

/// <summary>
/// Starts a launch target as-is. Used for launchers that need no preparation and as the
/// fallback for every launcher without its own client.
/// </summary>
/// <param name="start">Starts a target; defaults to <see cref="Start"/>.</param>
public sealed class DirectLauncherClient(Func<LaunchTarget, bool>? start = null) : ILauncherClient
{
    private readonly Func<LaunchTarget, bool> _start = start ?? Start;

    public string? LauncherId => null;

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(_start(game.Launch) ? LaunchStage.Starting : LaunchStage.NotStartable);
        return Task.CompletedTask;
    }

    /// <returns>False if there is nothing to start (demo data).</returns>
    public static bool Start(LaunchTarget target)
    {
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
}
