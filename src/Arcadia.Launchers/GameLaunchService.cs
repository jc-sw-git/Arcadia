using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers;

/// <summary>Routes a start request to the client of the game's launcher.</summary>
public sealed class GameLaunchService(LauncherRegistry launchers)
{
    private readonly DirectLauncherClient _fallback = new();

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = launchers.Find(game.LauncherId)?.Client ?? _fallback;
        return client.StartAsync(game, progress, cancellationToken);
    }

    /// <summary>Hands an uninstalled game to its launcher's install dialog.</summary>
    /// <returns>False if Arcadia has no way to install the game (demo data, unsupported launcher).</returns>
    public static bool Install(Game game) => game.Install is { } target && DirectLauncherClient.Start(target);
}
