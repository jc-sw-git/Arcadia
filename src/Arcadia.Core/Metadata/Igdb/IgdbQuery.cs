using System.Globalization;
using System.Text;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>
/// One query to IGDB (#494) in its query language. Built only here, from a Steam AppID of digits, an IGDB id or a search
/// term reduced to letters, digits, spaces and a few harmless marks, so nothing from a launcher's files can change the query
/// beyond the name.
/// </summary>
/// <param name="Nested">The games are under each result's <c>game</c> (<c>external_games</c>).</param>
public sealed record IgdbQuery(string Endpoint, string Body, bool Nested = false)
{
    /// <summary>A name longer than this is cut: no real title is, and the query stays short.</summary>
    public const int MaxTerm = 100;

    /// <summary>The most results of a search read; DLC and mods are dropped from them before hits are counted.</summary>
    public const int MaxResults = 50;

    // Steam, as IGDB's external game sources number it.
    private const int SteamSource = 1;

    private static readonly string[] Fields =
    [
        "name", "summary", "first_release_date", "game_type", "category", "genres.name", "themes.name", "keywords.name",
        "age_ratings.organization", "age_ratings.rating_category.rating", "age_ratings.category", "age_ratings.rating", "involved_companies.company.name",
        "involved_companies.developer", "involved_companies.publisher", "cover.image_id",
    ];

    private static readonly string GameFields = string.Join(',', Fields);

    private static readonly string ExternalFields = string.Join(',', Fields.Select(field => "game." + field));

    /// <summary>The game of a Steam AppID (<c>external_games</c>, source 1); null for an id that isn't one.</summary>
    public static IgdbQuery? BySteamAppId(string appId) =>
        appId is { Length: > 0 and <= 10 } && appId.All(char.IsAsciiDigit)
            ? new IgdbQuery("external_games", $"fields {ExternalFields}; where external_game_source = {SteamSource} & uid = \"{appId}\"; limit 5;", Nested: true)
            : null;

    /// <summary>The game of IGDB's own id (a hit chosen or matched before); null for an id that can't be one.</summary>
    public static IgdbQuery? ById(long id) =>
        id > 0 ? new IgdbQuery("games", $"fields {GameFields}; where id = {id.ToString(CultureInfo.InvariantCulture)};") : null;

    /// <summary>IGDB's search for <paramref name="name"/>; null when nothing searchable is left of it.</summary>
    public static IgdbQuery? ByName(string name) =>
        SearchTerm(name) is { } term ? new IgdbQuery("games", $"search \"{term}\"; fields {GameFields}; limit {MaxResults};") : null;

    /// <summary>
    /// The term searched for a name: without ™ ® ©, each <c>?</c> of a doubtful title and every character that isn't a
    /// letter, a digit or one of <c>- ' . , : ! &amp; ( )</c> as a space, spaces collapsed, at most <see cref="MaxTerm"/>.
    /// Null when nothing is left.
    /// </summary>
    public static string? SearchTerm(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var term = new StringBuilder(Math.Min(name.Length, MaxTerm));
        foreach (var c in name)
        {
            if (term.Length == MaxTerm)
            {
                break;
            }

            if (c is '™' or '®' or '©')
            {
                continue;
            }

            var kept = char.IsLetterOrDigit(c) || c is '-' or '\'' or '.' or ',' or ':' or '!' or '&' or '(' or ')' ? c : ' ';
            if (kept != ' ' || (term.Length > 0 && term[^1] != ' '))
            {
                term.Append(kept);
            }
        }

        var result = term.ToString().Trim();
        return result.Length > 0 && result.Any(char.IsLetterOrDigit) ? result : null;
    }
}
