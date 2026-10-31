using Arcadia.Core.Library;
using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>
/// Who owns the foreground window: a window of Steam itself (the client, Big Picture, its overlay windows; #242) or a
/// library game (<see cref="ForegroundGame"/>). Asked on every gamepad poll, from the polling thread and the copy's
/// reader thread, so the process's path is looked up only when the foreground process changed (#357): an answer never
/// outlives a switch, and with no foreground window (Windows reports none during some switches) neither holds.
/// </summary>
public sealed class ForegroundOwner(Func<GameFolders> gameFolders, LaunchedGames launched)
{
    private static readonly string[] SteamPrograms = ["steam.exe", "steamwebhelper.exe"];

    private static readonly Owner Nobody = new(null, null, IsSteam: false, GameKey: null);

    // Swapped as a whole, so both threads always read one consistent answer.
    private volatile Owner _last = Nobody;

    public bool IsSteam() => Current().IsSteam;

    /// <summary>The <c>Game.Key</c> of the library game in front, or null.</summary>
    public string? GameKey() => Current().GameKey;

    private Owner Current()
    {
        if (ProcessWatch.ForegroundProcessId() is not { } id)
        {
            return Nobody;
        }

        var folders = gameFolders();
        var last = _last;
        if (last.ProcessId == id && ReferenceEquals(last.Folders, folders))
        {
            return last;
        }

        var path = ProcessWatch.PathOf(id);
        var isSteam = path is not null && SteamPrograms.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        var gameKey = isSteam ? null : ForegroundGame.GameOf(id, path, folders, launched);
        var now = new Owner(id, folders, isSteam, gameKey);
        _last = now;
        return now;
    }

    private sealed record Owner(int? ProcessId, GameFolders? Folders, bool IsSteam, string? GameKey);
}
