using Arcadia.Core.Artwork;

namespace Arcadia.App.ViewModels;

/// <summary>One game's background as "Bild ansehen" shows it, with the title shown beside it.</summary>
public sealed record ShownImage(string GameKey, string Title, ArtworkFile File);

/// <summary>
/// The random order of "Bild ansehen" (#565): no game's image comes again before every other one of the pool was shown
/// once. The pool is handed in anew each time, so a game that left the library is never picked and a new one joins.
/// </summary>
public sealed class ImageShowOrder(Random random)
{
    private readonly HashSet<string> _shown = [];

    /// <summary>A random image of <paramref name="pool"/> not shown since the last round, never <paramref name="current"/>; null when the pool has no other image.</summary>
    public ShownImage? Next(IReadOnlyList<ShownImage> pool, string? current)
    {
        var others = pool.Where(image => image.GameKey != current).ToList();
        if (others.Count == 0)
        {
            return null;
        }

        var fresh = others.Where(image => !_shown.Contains(image.GameKey)).ToList();
        if (fresh.Count == 0)
        {
            // Every one was shown: the next round starts.
            _shown.Clear();
            fresh = others;
        }

        var next = fresh[random.Next(fresh.Count)];
        _shown.Add(next.GameKey);
        return next;
    }
}
