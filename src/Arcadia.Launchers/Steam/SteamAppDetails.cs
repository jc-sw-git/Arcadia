using System.Globalization;
using System.Text.Json;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam's store page of a game (#493): <c>store.steampowered.com/api/appdetails?l=&lt;language&gt;&amp;appids=&lt;AppID&gt;</c>,
/// no key, no cookie, asked in the UI language (#557). Gives the description (in that language where the game has one, else
/// English), the short description, genres,
/// developers, publishers and the age ratings <c>usk</c>, <c>pegi</c>, <c>esrb</c> and Steam's own <c>steam_germany</c>.
/// Steam takes about 200 requests per 5 minutes; Arcadia stays below.
/// </summary>
internal sealed class SteamAppDetails : IMetadataStore
{
    private const string Address = "https://store.steampowered.com/api/appdetails";

    private const int MaxNames = 10;

    // Steam's own limit is about 200 per 5 minutes; a little below it leaves room for the store pages the player opens.
    public RequestLimit Limit { get; } = new(180, TimeSpan.FromMinutes(5));

    public Uri? AddressOf(Game game, CultureInfo language) => AppIdOf(game) is { } appId
        ? new Uri($"{Address}?l={SteamLanguage.NameOf(language)}&appids={appId.ToString(CultureInfo.InvariantCulture)}")
        : null;

    /// <summary>A Steam game's AppID: its id when that is a plain number as Steam writes it, else null (never a request).</summary>
    internal static uint? AppIdOf(Game game) =>
        game.LauncherId == SteamIntegration.Info.Id
        && game.Id.Length is > 0 and <= 10
        && game.Id.All(char.IsAsciiDigit)
        && uint.TryParse(game.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
        && appId > 0
        && appId.ToString(CultureInfo.InvariantCulture) == game.Id
            ? appId
            : null;

    public GameMetadata? Parse(Game game, ReadOnlySpan<byte> answer)
    {
        if (AppIdOf(game) is not { } appId)
        {
            return null;
        }

        try
        {
            var reader = new Utf8JsonReader(answer, new JsonReaderOptions { MaxDepth = 16 });
            using var document = JsonDocument.ParseValue(ref reader);
            return Parse(appId, document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GameMetadata? Parse(uint appId, JsonElement root)
    {
        var id = appId.ToString(CultureInfo.InvariantCulture);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(id, out var app) || app.ValueKind != JsonValueKind.Object
            || !app.TryGetProperty("success", out var success) || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        if (success.ValueKind == JsonValueKind.False)
        {
            return GameMetadata.Empty;
        }

        // Steam answers an AppID it moved with the data of the new one: that is another game's page.
        if (!app.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("steam_appid", out var answeredId) || answeredId.ValueKind != JsonValueKind.Number
            || !answeredId.TryGetUInt32(out var answered) || answered != appId)
        {
            return null;
        }

        return new GameMetadata
        {
            Description = String(data, "about_the_game") ?? String(data, "detailed_description"),
            ShortDescription = String(data, "short_description"),
            Genres = SteamGenres.Of(GenreIds(data)),
            Developers = Strings(data, "developers"),
            Publishers = Strings(data, "publishers"),
            AgeRatings = Ratings(data),
        };
    }

    private static IEnumerable<string> GenreIds(JsonElement data)
    {
        if (!data.TryGetProperty("genres", out var genres) || genres.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var genre in genres.EnumerateArray())
        {
            if (genre.ValueKind == JsonValueKind.Object && String(genre, "id") is { } id)
            {
                yield return id;
            }
        }
    }

    private static List<AgeRating> Ratings(JsonElement data)
    {
        var ratings = new List<AgeRating>();
        if (!data.TryGetProperty("ratings", out var all) || all.ValueKind != JsonValueKind.Object)
        {
            return ratings;
        }

        void Add(string name, AgeRatingBoard board, bool byStore = false)
        {
            if (all.TryGetProperty(name, out var rating) && rating.ValueKind == JsonValueKind.Object && String(rating, "rating") is { } value)
            {
                ratings.Add(new AgeRating(board, value, byStore));
            }
        }

        Add("usk", AgeRatingBoard.Usk);
        Add("steam_germany", AgeRatingBoard.Usk, byStore: true);
        Add("pegi", AgeRatingBoard.Pegi);
        Add("esrb", AgeRatingBoard.Esrb);
        return ratings;
    }

    private static List<string> Strings(JsonElement data, string name) =>
        data.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array
            ?
            [
                .. values.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!.Trim())
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxNames),
            ]
            : [];

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
