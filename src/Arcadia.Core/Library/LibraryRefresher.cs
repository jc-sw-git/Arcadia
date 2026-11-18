using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// Rescans the library and tells whether anything changed — games installed, uninstalled or
/// updated outside Arcadia, or a launcher that came or went.
/// </summary>
public sealed class LibraryRefresher(LibraryService library, TimeProvider time)
{
    /// <summary>Window activations come in bursts; one rescan per burst is enough.</summary>
    public static readonly TimeSpan Throttle = TimeSpan.FromSeconds(5);

    private DateTimeOffset _lastRefresh;

    /// <summary>Counts the scans that read launchers, so a caller can tell a skipped refresh from an unchanged one.</summary>
    public long Scans { get; private set; }
    private Task<IReadOnlyList<LauncherLibrary>?>? _running;

    /// <summary>
    /// Rescans unless, without <paramref name="force"/>, a rescan is still running or the last one was less than
    /// <see cref="Throttle"/> ago. A forced rescan waits for a running one and then scans again, since it may be asked
    /// for a change the running one doesn't see yet (e.g. a launcher switched on, #136).
    /// </summary>
    /// <param name="current">The libraries shown now; read after the scan, so it sees what changed meanwhile.</param>
    /// <returns>The rescanned libraries if they differ from <paramref name="current"/>, otherwise null.</returns>
    public async Task<IReadOnlyList<LauncherLibrary>?> RefreshAsync(Func<IReadOnlyList<LauncherLibrary>> current, bool force = false)
    {
        while (_running is { IsCompleted: false } running)
        {
            if (!force)
            {
                return null;
            }

            // Its caller takes over its result.
            await running;
        }

        var now = time.GetUtcNow();
        if (!force && now - _lastRefresh < Throttle)
        {
            return null;
        }

        _lastRefresh = now;
        var scan = ScanAsync(current);
        _running = scan;
        return await scan;
    }

    private async Task<IReadOnlyList<LauncherLibrary>?> ScanAsync(Func<IReadOnlyList<LauncherLibrary>> current)
    {
        var scanned = await library.LoadAsync();
        Scans++;
        return HasChanged(current(), scanned) ? scanned : null;
    }

    /// <summary>
    /// Rescans only <paramref name="launcherId"/> ("Neu einlesen" on its page, #137) after a running rescan; the other
    /// launchers keep what <paramref name="current"/> shows. A launcher that is active but has no tab yet is read with all
    /// the others, so the tabs come out in order. Doesn't count for <see cref="Throttle"/>: it reads one launcher only.
    /// </summary>
    /// <returns>The libraries with the rescanned launcher if anything changed, otherwise null.</returns>
    public async Task<IReadOnlyList<LauncherLibrary>?> RefreshAsync(Func<IReadOnlyList<LauncherLibrary>> current, string launcherId)
    {
        while (_running is { IsCompleted: false } running)
        {
            await running;
        }

        var scan = ScanAsync(current, launcherId);
        _running = scan;
        return await scan;
    }

    private async Task<IReadOnlyList<LauncherLibrary>?> ScanAsync(Func<IReadOnlyList<LauncherLibrary>> current, string launcherId)
    {
        if (await library.LoadAsync(launcherId) is not { } rescanned)
        {
            return null;
        }

        Scans++;
        var shown = current();
        if (!shown.Any(l => l.Launcher.Id == launcherId))
        {
            return await ScanAsync(current);
        }

        var scanned = shown.Select(l => l.Launcher.Id == launcherId ? rescanned : l).ToList();
        return HasChanged(shown, scanned) ? scanned : null;
    }

    /// <summary>
    /// True if launchers or games differ in anything a game carries: what a tab or cover shows, what the library sorts
    /// and filters by (after a game ended, its playtime and last start come with the next rescan, #95; a reinstall into
    /// the same folder, its install date), how it starts, and a launcher's hint after an account switch (#83).
    /// Launchers count in their tab order (#136), games per launcher in any order. Compared field by field (records),
    /// without building strings: a 5,000-game library is compared on the UI thread after every rescan.
    /// </summary>
    public static bool HasChanged(IReadOnlyList<LauncherLibrary> current, IReadOnlyList<LauncherLibrary> scanned)
    {
        if (current.Count != scanned.Count)
        {
            return true;
        }

        for (var i = 0; i < current.Count; i++)
        {
            var (before, after) = (current[i], scanned[i]);
            if (before.Launcher.Id != after.Launcher.Id || before.IsFound != after.IsFound || before.ScanFailed != after.ScanFailed
                || GamesDiffer(before.Games, after.Games))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GamesDiffer(IReadOnlyList<Game> before, IReadOnlyList<Game> after)
    {
        if (before.Count != after.Count)
        {
            return true;
        }

        if (before.SequenceEqual(after))
        {
            return false; // the usual case: a scanner lists its games in the same order each time
        }

        var byKey = new Dictionary<string, Game>(before.Count, StringComparer.Ordinal);
        foreach (var game in before)
        {
            if (!byKey.TryAdd(game.Key, game))
            {
                return true; // a key twice is a scanner's mistake: showing the scan is the safe side
            }
        }

        return after.Any(game => !byKey.TryGetValue(game.Key, out var known) || known != game);
    }
}
