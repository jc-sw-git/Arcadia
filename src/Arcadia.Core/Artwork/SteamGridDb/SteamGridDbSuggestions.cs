using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>
/// SteamGridDB as the source of more images in "Artwork ändern" (#489, #490): its images for the game's store id, best
/// rated first. Without a usable key nothing is sent (<see cref="SteamGridDbKey.ImagesAsync"/>); a game without a store id
/// gets none (search by name: #491).
/// </summary>
public sealed class SteamGridDbSuggestions(SteamGridDbKey key, Func<Game, ArtworkStoreId?> storeIdOf) : IArtworkSuggestions
{
    public SuggestionKeyState KeyState => key.State switch
    {
        SteamGridDbKeyState.None => SuggestionKeyState.Missing,
        SteamGridDbKeyState.Rejected => SuggestionKeyState.Invalid,
        _ => SuggestionKeyState.Valid,
    };

    public async Task<IReadOnlyList<SuggestedImage>> FindAsync(Game game, ArtworkKind kind, CancellationToken cancel)
    {
        if (storeIdOf(game) is not { } storeId)
        {
            return [];
        }

        var answer = await key.ImagesAsync(storeId, kind, cancel);
        return answer.Outcome == SteamGridDbOutcome.Found
            ? [.. answer.Images.Select(image => new SuggestedImage(ArtworkService.SteamGridDbOrigin, image.Url, image.Score))]
            : [];
    }
}
