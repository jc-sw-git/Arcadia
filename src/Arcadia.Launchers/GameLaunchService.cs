using Arcadia.Core.Launchers;
using Arcadia.Core.Models;

namespace Arcadia.Launchers;

/// <summary>Routes a start request to the client of the game's launcher.</summary>
public sealed class GameLaunchService(IEnumerable<ILauncherClient> clients)
{
    private readonly IReadOnlyList<ILauncherClient> _clients = clients.ToList();
    private readonly DirectLauncherClient _fallback = new();

    public static GameLaunchService CreateDefault(TimeProvider time) => new([new Epic.EpicLauncherClient(time)]);

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = _clients.FirstOrDefault(c => c.LauncherId == game.LauncherId) ?? _fallback;
        return client.StartAsync(game, progress, cancellationToken);
    }
}
