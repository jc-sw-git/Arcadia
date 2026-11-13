using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>The filter's categories with a long list of names (#466): genre, tags, developer, publisher.</summary>
public enum FilterList
{
    Genre,
    Tag,
    Developer,
    Publisher,
}

/// <summary>
/// What the filter knows of one entry beyond its launcher's data (#466): the merged metadata as the detail page shows it
/// and the player's own tags. Names are trimmed and held once, ignoring case; a genre is held by its key, never by a
/// shown name, so a choice survives a change of the UI language (#557).
/// </summary>
public sealed class GameFacts
{
    private static readonly IReadOnlyDictionary<AgeRatingBoard, string> NoRatings = new Dictionary<AgeRatingBoard, string>();

    private GameFacts(
        IReadOnlyList<string> genres,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> ownTags,
        IReadOnlyList<string> developers,
        IReadOnlyList<string> publishers,
        int? year,
        IReadOnlyDictionary<AgeRatingBoard, string> ratings)
    {
        Genres = genres;
        Tags = tags;
        OwnTags = ownTags;
        Developers = developers;
        Publishers = publishers;
        Year = year;
        Ratings = ratings;
    }

    public static GameFacts None { get; } = new([], [], [], [], [], null, NoRatings);

    /// <summary>The genres' keys (<see cref="Genre"/> names).</summary>
    public IReadOnlyList<string> Genres { get; }

    /// <summary>The providers' tags, exactly as the detail page shows them (#466 case 6).</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>The player's own tags of the entry (#495).</summary>
    public IReadOnlyList<string> OwnTags { get; }

    public IReadOnlyList<string> Developers { get; }

    public IReadOnlyList<string> Publishers { get; }

    /// <summary>The release year; a pre-order's future year included.</summary>
    public int? Year { get; }

    /// <summary>The rating per board as the detail page shows it; a board without one is missing.</summary>
    public IReadOnlyDictionary<AgeRatingBoard, string> Ratings { get; }

    /// <summary>The names of <paramref name="list"/>; for tags only the providers', the own ones are <see cref="OwnTags"/>.</summary>
    public IReadOnlyList<string> NamesOf(FilterList list) => list switch
    {
        FilterList.Genre => Genres,
        FilterList.Tag => Tags,
        FilterList.Developer => Developers,
        _ => Publishers,
    };

    /// <summary>The entry has no value in <paramref name="list"/>: "Ohne Angabe" (#44). Own tags count as tags.</summary>
    public bool Lacks(FilterList list) => NamesOf(list).Count == 0 && (list != FilterList.Tag || OwnTags.Count == 0);

    public static GameFacts Of(MergedMetadata metadata, IReadOnlyList<string>? ownTags = null)
    {
        ownTags ??= [];
        if (metadata.IsEmpty && ownTags.Count == 0)
        {
            return None;
        }

        var own = Names(ownTags);
        return new GameFacts(
            [.. (metadata.Genres?.Value ?? []).Distinct().Select(genre => genre.ToString())],
            // As the detail page: a provider's tag the player gave the game as an own one shows only as the own one.
            [.. Names(metadata.Tags?.Value).Where(tag => !own.Contains(tag, FilterNames.Comparer))],
            own,
            Names(metadata.Developers?.Value),
            Names(metadata.Publishers?.Value),
            metadata.ReleaseDate?.Value.Year,
            metadata.AgeRatings.Count == 0 ? NoRatings : metadata.AgeRatings.ToDictionary(rating => rating.Value.Board, rating => rating.Value.Rating));
    }

    private static List<string> Names(IReadOnlyList<string>? names) => names is null || names.Count == 0 ? [] : [.. names
        .Select(name => name.Trim())
        .Where(name => name.Length > 0)
        .Distinct(FilterNames.Comparer)];
}

/// <summary>The <see cref="GameFacts"/> of a tab's entries, by game key; an entry without any has <see cref="GameFacts.None"/>.</summary>
public sealed class FilterFacts(IReadOnlyDictionary<string, GameFacts> byKey)
{
    public static FilterFacts None { get; } = new(new Dictionary<string, GameFacts>());

    public GameFacts Of(Game game) => byKey.GetValueOrDefault(game.Key) ?? GameFacts.None;
}

/// <summary>
/// How the filter compares names (#466): one value per name, ignoring case and surrounding whitespace ("CD PROJEKT RED"
/// = "CD Projekt Red "), no fuzzy merging ("Ubisoft Montreal" ≠ "Ubisoft").
/// </summary>
public static class FilterNames
{
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public static bool Same(string a, string b) => Comparer.Equals(a.Trim(), b.Trim());
}
