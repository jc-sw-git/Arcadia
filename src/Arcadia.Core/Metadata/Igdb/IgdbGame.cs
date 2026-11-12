using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>What IGDB answered (#494).</summary>
public enum IgdbOutcome
{
    /// <summary>An answer; <see cref="IgdbLookup.Games"/> may be empty (IGDB doesn't know the game).</summary>
    Found,

    /// <summary>Twitch or IGDB turned the access down: a warning, and IGDB is skipped until the access changes.</summary>
    Rejected,

    /// <summary>No network, no answer within 10 s, a server error or an answer that can't be read.</summary>
    NoConnection,
}

/// <summary>
/// One game as IGDB knows it (#494): its own id, its name, its type (main game, DLC, …; <see cref="IgdbGameType"/>), its
/// metadata as IGDB delivers it (not yet checked), the id of its cover image and the year it came out.
/// </summary>
public sealed record IgdbGame(long Id, string Name, int? Type, GameMetadata Data, string? CoverId, int? Year);

/// <summary>IGDB's answer to one request, the games in its order.</summary>
public sealed record IgdbLookup(IgdbOutcome Outcome, IReadOnlyList<IgdbGame> Games)
{
    public static IgdbLookup Of(IgdbOutcome outcome) => new(outcome, []);
}

/// <summary>IGDB's game types (<c>game_type</c>, formerly <c>category</c>) Arcadia counts as a main game (#494, D4).</summary>
public static class IgdbGameType
{
    public const int MainGame = 0;

    // Main game, standalone expansion, remake, remaster, expanded game, port. Dropped: DLC (1), expansion (2), bundle (3),
    // mod (5), episode (6), season (7), fork (12), pack (13), update (14).
    private static readonly HashSet<int> Main = [0, 4, 8, 9, 10, 11];

    /// <summary>A game without a type is an old entry of a main game, as IGDB's site shows it.</summary>
    public static bool IsMainGame(int? type) => type is null || Main.Contains(type.Value);
}
