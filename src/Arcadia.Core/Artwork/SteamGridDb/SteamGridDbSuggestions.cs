using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>
/// SteamGridDB as the source of more images in "Artwork ändern" (#489, #490, #491): its images for the game's store id or
/// the game it matched by name, best rated first. Without a usable key nothing is sent; a game with neither gets none.
/// </summary>
/// <param name="imagesOf">The game's images at SteamGridDB (ArtworkService.ByName.cs).</param>
public sealed class SteamGridDbSuggestions(SteamGridDbKey key, Func<Game, ArtworkKind, CancellationToken, Task<SteamGridDbAnswer>> imagesOf) : IArtworkSuggestions
{
    public SuggestionKeyState KeyState => key.State switch
    {
        SteamGridDbKeyState.None => SuggestionKeyState.Missing,
        SteamGridDbKeyState.Rejected => SuggestionKeyState.Invalid,
        _ => SuggestionKeyState.Valid,
    };

    public async Task<IReadOnlyList<SuggestedImage>> FindAsync(Game game, ArtworkKind kind, CancellationToken cancel)
    {
        if (!key.IsUsable)
        {
            return [];
        }

        var answer = await imagesOf(game, kind, cancel);
        return answer.Outcome == SteamGridDbOutcome.Found
            ? [.. answer.Images.Select(image => new SuggestedImage(ArtworkService.SteamGridDbOrigin, image.Url))]
            : [];
    }
}
