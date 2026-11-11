using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>Whether the player's key for the source of more images (SteamGridDB, #489) can be used.</summary>
public enum SuggestionKeyState
{
    /// <summary>No key: Arcadia asks the source nothing, "Artwork ändern" says how to get one.</summary>
    Missing,

    /// <summary>The source refused the key: no request until a new one is entered, "Artwork ändern" says so.</summary>
    Invalid,

    Valid,
}

/// <summary>One image the source offers for a game, with its rating there (higher is better), if it has one.</summary>
/// <param name="Origin">Short and stable, e.g. <c>steamgriddb</c>; kept in the cache's metadata.</param>
/// <param name="Download">An <c>https</c> address of the image.</param>
public sealed record SuggestedImage(string Origin, Uri Download, int? Score = null);

/// <summary>
/// More images for "Artwork ändern" than the launcher has (#13: SteamGridDB, by rating, up to 20). The source is wired by
/// #489's SteamGridDB client; without one the modal shows the launcher's image, the own file and the notice.
/// </summary>
public interface IArtworkSuggestions
{
    SuggestionKeyState KeyState { get; }

    /// <summary>The images for the game, best first; none if the source has none or can't be reached. Never throws for those.</summary>
    Task<IReadOnlyList<SuggestedImage>> FindAsync(Game game, ArtworkKind kind, CancellationToken cancel);
}
