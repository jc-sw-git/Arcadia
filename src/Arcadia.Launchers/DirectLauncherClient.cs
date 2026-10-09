using System.Diagnostics;
using Arcadia.Core.Launchers;
using Arcadia.Core.Models;

namespace Arcadia.Launchers;

/// <summary>
/// Starts a launch target as-is. Used for launchers that need no preparation and as the
/// fallback for every launcher without its own client.
/// </summary>
public sealed class DirectLauncherClient : ILauncherClient
{
    public string? LauncherId => null;

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(Start(game.Launch) ? LaunchStage.Starting : LaunchStage.NotStartable);
        return Task.CompletedTask;
    }

    /// <returns>False if there is nothing to start (demo data).</returns>
    public static bool Start(LaunchTarget target)
    {
        switch (target)
        {
            case LaunchTarget.ProtocolUri uri:
                Process.Start(new ProcessStartInfo(uri.Uri) { UseShellExecute = true });
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

                Process.Start(info);
                return true;

            case LaunchTarget.ShellAppsFolder app:
                Process.Start(new ProcessStartInfo("explorer.exe", $"shell:appsFolder\\{app.AppUserModelId}") { UseShellExecute = false });
                return true;

            case LaunchTarget.None:
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown launch target.");
        }
    }
}
