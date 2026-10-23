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

        var shown = current();
        if (!shown.Any(l => l.Launcher.Id == launcherId))
        {
            return await ScanAsync(current);
        }

        var scanned = shown.Select(l => l.Launcher.Id == launcherId ? rescanned : l).ToList();
        return HasChanged(shown, scanned) ? scanned : null;
    }

    /// <summary>
    /// True if launchers or games differ in anything a tab or cover shows, or in what the library sorts by: after a game
    /// ended, its playtime and last start come with the next rescan (#95). Launchers count in their tab order (#136).
    /// </summary>
    public static bool HasChanged(IReadOnlyList<LauncherLibrary> current, IReadOnlyList<LauncherLibrary> scanned)
    {
        static string Tabs(IEnumerable<LauncherLibrary> libraries) => string.Join('|', libraries.Select(l => $"{l.Launcher.Id}:{l.IsFound}"));

        static string Signature(IEnumerable<LauncherLibrary> libraries) => string.Join('|', libraries
            .SelectMany(l => l.Games.Select(g => $"{l.Launcher.Id}/{g.Key}:{g.IsInstalled}:{g.UpdateAvailable}:{g.InstallDirectory}:{g.LastPlayedAt?.UtcTicks}:{g.Playtime?.Ticks}"))
            .Order(StringComparer.Ordinal));

        return Tabs(current) != Tabs(scanned) || Signature(current) != Signature(scanned);
    }
}
