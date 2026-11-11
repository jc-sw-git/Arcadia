using Arcadia.Core.Artwork.SteamGridDb;

namespace Arcadia.Core.Artwork;

/// <summary>
/// What the row "Fehlende Cover und Hintergründe" shows (#489): how many games of the last libraries lack a cover or a
/// background, the run going on and the result of the last run the player started with "Jetzt suchen".
/// </summary>
/// <param name="Running">The run going on; null while none runs.</param>
/// <param name="Result">The last run started by the player; null before one.</param>
public sealed record ArtworkSearch(int MissingCovers, int MissingHeroes, ArtworkRun? Running, ArtworkSearchResult? Result)
{
    /// <summary>Before the first scan's run has ended.</summary>
    public static ArtworkSearch NotYet { get; } = new(0, 0, null, null);
}

/// <summary>A run going on: <paramref name="Done"/> of the <paramref name="Total"/> games that missed an image when it began.</summary>
/// <param name="ByPlayer">Started with "Jetzt suchen".</param>
public sealed record ArtworkRun(int Done, int Total, bool ByPlayer);

/// <summary>What a run found: images taken into the cache, and whether SteamGridDB took part.</summary>
/// <param name="WithSteamGridDb">A usable SteamGridDB key at the start: SteamGridDB was asked where it was needed.</param>
/// <param name="SteamGridDbFailure">Why SteamGridDB was left out after a while (key turned down, no connection); null if it wasn't.</param>
public sealed record ArtworkSearchResult(int Covers, int Heroes, bool WithSteamGridDb, SteamGridDbOutcome? SteamGridDbFailure);
