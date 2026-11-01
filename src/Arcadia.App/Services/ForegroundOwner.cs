using Arcadia.Core.Library;
using Arcadia.Platform;

namespace Arcadia.App.Services;

/// <summary>
/// Who owns the foreground window: a window of Steam itself (the client, Big Picture, its overlay windows; #242) or a
/// library game (<see cref="ForegroundGame"/>). Asked on every gamepad poll, from the polling thread and the copy's
/// reader thread, so the process's path is looked up only when the foreground process changed (#357): an answer never
/// outlives a switch, and with no foreground window (Windows reports none during some switches) neither holds.
/// </summary>
/// <param name="steamPrograms">
/// The file names of the programs of the launcher that owns Steam Input (<c>LauncherRegistry.SteamInputOwner</c>): their
/// windows are Steam's; none if no such launcher is registered.
/// </param>
/// <param name="foregroundProcessId">The process owning the foreground window, or null; the system's unless a test gives one.</param>
/// <param name="pathOf">A process's executable path, or null; the system's unless a test gives one.</param>
public sealed class ForegroundOwner(
    Func<GameFolders> gameFolders,
    LaunchedGames launched,
    IReadOnlyCollection<string> steamPrograms,
    Func<int?>? foregroundProcessId = null,
    Func<int, string?>? pathOf = null)
{
    private readonly Func<int?> _foregroundProcessId = foregroundProcessId ?? ProcessWatch.ForegroundProcessId;
    private readonly Func<int, string?> _pathOf = pathOf ?? ProcessWatch.PathOf;

    private static readonly Owner Nobody = new(null, null, IsSteam: false, GameKey: null);

    // Swapped as a whole, so both threads always read one consistent answer.
    private volatile Owner _last = Nobody;

    public bool IsSteam() => Current().IsSteam;

    /// <summary>The <c>Game.Key</c> of the library game in front, or null.</summary>
    public string? GameKey() => Current().GameKey;

    private Owner Current()
    {
        if (_foregroundProcessId() is not { } id)
        {
            return Nobody;
        }

        var folders = gameFolders();
        var last = _last;
        if (last.ProcessId == id && ReferenceEquals(last.Folders, folders))
        {
            return last;
        }

        var path = _pathOf(id);
        var isSteam = path is not null && steamPrograms.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        var gameKey = isSteam ? null : ForegroundGame.GameOf(id, path, folders, launched);
        var now = new Owner(id, folders, isSteam, gameKey);
        _last = now;
        return now;
    }

    private sealed record Owner(int? ProcessId, GameFolders? Folders, bool IsSteam, string? GameKey);
}
