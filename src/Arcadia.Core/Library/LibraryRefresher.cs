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
    private bool _refreshing;

    /// <summary>
    /// Rescans unless a rescan is still running or, without <paramref name="force"/>, the last one
    /// was less than <see cref="Throttle"/> ago.
    /// </summary>
    /// <param name="current">The libraries shown now; read after the scan, so it sees what changed meanwhile.</param>
    /// <returns>The rescanned libraries if they differ from <paramref name="current"/>, otherwise null.</returns>
    public async Task<IReadOnlyList<LauncherLibrary>?> RefreshAsync(Func<IReadOnlyList<LauncherLibrary>> current, bool force = false)
    {
        var now = time.GetUtcNow();
        if (_refreshing || (!force && now - _lastRefresh < Throttle))
        {
            return null;
        }

        _refreshing = true;
        _lastRefresh = now;
        try
        {
            var scanned = await library.LoadAsync();
            return HasChanged(current(), scanned) ? scanned : null;
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// True if launchers or games differ in anything a tab or cover shows, or in what the library sorts by: after a game
    /// ended, its playtime and last start come with the next rescan (#95).
    /// </summary>
    public static bool HasChanged(IReadOnlyList<LauncherLibrary> current, IReadOnlyList<LauncherLibrary> scanned)
    {
        static string Signature(IEnumerable<LauncherLibrary> libraries) => string.Join('|', libraries
            .SelectMany(l => l.Games.Select(g => $"{l.Launcher.Id}/{g.Key}:{g.IsInstalled}:{g.UpdateAvailable}:{g.InstallDirectory}:{g.LastPlayedAt?.UtcTicks}:{g.Playtime?.Ticks}"))
            .Order(StringComparer.Ordinal));

        return current.Count != scanned.Count || Signature(current) != Signature(scanned);
    }
}
