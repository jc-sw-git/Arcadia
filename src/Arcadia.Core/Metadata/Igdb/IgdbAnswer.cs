using System.Text.Json;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>
/// Reads IGDB's answer (#494): genres onto Arcadia's list, age ratings by organization and rating category, themes and
/// keywords as provider tags, developers and publishers from the involved companies, the first release date. Anything of
/// another shape is left out; the values are checked again (<see cref="MetadataCheck"/>) before they are shown.
/// </summary>
public static class IgdbAnswer
{
    /// <summary>The longest name read; a longer one is no game's.</summary>
    public const int MaxName = 200;

    // IGDB's genre names onto Arcadia's list; MOBA, Quiz/Trivia and every other one are dropped.
    private static readonly Dictionary<string, Genre> Genres = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Point-and-click"] = Genre.PointAndClick,
        ["Fighting"] = Genre.Fighting,
        ["Shooter"] = Genre.Shooter,
        ["Music"] = Genre.Music,
        ["Platform"] = Genre.Platformer,
        ["Puzzle"] = Genre.Puzzle,
        ["Racing"] = Genre.Racing,
        ["Real Time Strategy (RTS)"] = Genre.Strategy,
        ["Role-playing (RPG)"] = Genre.RolePlaying,
        ["Simulator"] = Genre.Simulation,
        ["Sport"] = Genre.Sports,
        ["Strategy"] = Genre.Strategy,
        ["Turn-based strategy (TBS)"] = Genre.Strategy,
        ["Tactical"] = Genre.Tactics,
        ["Hack and slash/Beat 'em up"] = Genre.Action,
        ["Adventure"] = Genre.Adventure,
        ["Indie"] = Genre.Indie,
        ["Arcade"] = Genre.Arcade,
        ["Pinball"] = Genre.Arcade,
        ["Visual Novel"] = Genre.VisualNovel,
        ["Card & Board Game"] = Genre.CardAndBoard,
    };

    // IGDB has "Action" only as a theme; it is the genre of Arcadia's list.
    private const string ActionTheme = "Action";

    // Organizations: ESRB 1, PEGI 2, USK 4. Rating categories: PEGI 1–5, ESRB 6–12 (RP and EC have no place), USK 18–22.
    private static readonly Dictionary<(long Organization, long Category), AgeRating> Ratings = new()
    {
        [(2, 1)] = new(AgeRatingBoard.Pegi, "3"),
        [(2, 2)] = new(AgeRatingBoard.Pegi, "7"),
        [(2, 3)] = new(AgeRatingBoard.Pegi, "12"),
        [(2, 4)] = new(AgeRatingBoard.Pegi, "16"),
        [(2, 5)] = new(AgeRatingBoard.Pegi, "18"),
        [(1, 8)] = new(AgeRatingBoard.Esrb, "E"),
        [(1, 9)] = new(AgeRatingBoard.Esrb, "E10+"),
        [(1, 10)] = new(AgeRatingBoard.Esrb, "T"),
        [(1, 11)] = new(AgeRatingBoard.Esrb, "M"),
        [(1, 12)] = new(AgeRatingBoard.Esrb, "AO"),
        [(4, 18)] = new(AgeRatingBoard.Usk, "0"),
        [(4, 19)] = new(AgeRatingBoard.Usk, "6"),
        [(4, 20)] = new(AgeRatingBoard.Usk, "12"),
        [(4, 21)] = new(AgeRatingBoard.Usk, "16"),
        [(4, 22)] = new(AgeRatingBoard.Usk, "18"),
    };

    /// <summary>The games of an answer, in its order; null if it isn't IGDB's JSON (an array of objects).</summary>
    /// <param name="nested">Each result holds its game under <c>game</c> (<c>external_games</c>).</param>
    public static IReadOnlyList<IgdbGame>? Read(byte[] body, bool nested = false)
    {
        try
        {
            using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var games = new List<IgdbGame>();
            foreach (var item in json.RootElement.EnumerateArray())
            {
                var game = nested && item.ValueKind == JsonValueKind.Object && item.TryGetProperty("game", out var inner) ? inner : item;
                if (GameOf(game) is { } found && games.All(g => g.Id != found.Id))
                {
                    games.Add(found);
                }
            }

            return games;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IgdbGame? GameOf(JsonElement game)
    {
        if (game.ValueKind != JsonValueKind.Object || Long(game, "id") is not { } id || id <= 0
            || Text(game, "name")?.Trim() is not { Length: > 0 and <= MaxName } name)
        {
            return null;
        }

        var released = Long(game, "first_release_date") is { } seconds and > -2208988800 and < 4133980800
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime)
            : (DateOnly?)null;
        var themes = Names(game, "themes");
        var genres = Names(game, "genres").Select(g => Genres.TryGetValue(g, out var genre) ? genre : (Genre?)null).OfType<Genre>().ToList();
        if (themes.Contains(ActionTheme, StringComparer.OrdinalIgnoreCase))
        {
            genres.Insert(0, Genre.Action);
        }

        var summary = Text(game, "summary");
        var data = new GameMetadata
        {
            Description = summary,
            ShortDescription = summary,
            Genres = [.. genres.Distinct()],
            Developers = Companies(game, "developer"),
            Publishers = Companies(game, "publisher"),
            ReleaseDate = released,
            Tags = [.. themes, .. Names(game, "keywords")],
            AgeRatings = AgeRatings(game),
        };
        var cover = game.TryGetProperty("cover", out var image) && image.ValueKind == JsonValueKind.Object ? Text(image, "image_id") : null;
        var type = (int?)(Long(game, "game_type") ?? Long(game, "category"));
        return new IgdbGame(id, name, type, data, IgdbImages.IsImageId(cover) ? cover : null, released?.Year);
    }

    private static List<AgeRating> AgeRatings(JsonElement game) =>
    [
        .. Objects(game, "age_ratings")
            .Select(r => (Long(r, "organization"), Long(r, "rating_category")) is ({ } organization, { } category)
                && Ratings.TryGetValue((organization, category), out var rating) ? rating : null)
            .OfType<AgeRating>()
            .Distinct(),
    ];

    private static List<string> Companies(JsonElement game, string role) =>
    [
        .. Objects(game, "involved_companies")
            .Where(c => c.TryGetProperty(role, out var flag) && flag.ValueKind == JsonValueKind.True)
            .Select(c => c.TryGetProperty("company", out var company) && company.ValueKind == JsonValueKind.Object ? Text(company, "name") : null)
            .OfType<string>(),
    ];

    private static List<string> Names(JsonElement game, string property) => [.. Objects(game, property).Select(o => Text(o, "name")).OfType<string>()];

    private static IEnumerable<JsonElement> Objects(JsonElement game, string property) =>
        game.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object).ToList()
            : [];

    private static string? Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Long(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;
}

/// <summary>IGDB's cover images (#494): only by an id of letters and digits, only over HTTPS from IGDB's image host.</summary>
public static class IgdbImages
{
    public static readonly Uri Host = new("https://images.igdb.com/igdb/image/upload/");

    public static bool IsImageId(string? id) => id is { Length: > 0 and <= 40 } && id.All(char.IsAsciiLetterOrDigit);

    /// <summary>The cover in IGDB's 264 × 374 size; null for an id that isn't one.</summary>
    public static Uri? CoverOf(string? imageId) => IsImageId(imageId) ? new Uri(Host, $"t_cover_big/{imageId!.ToLowerInvariant()}.jpg") : null;
}
