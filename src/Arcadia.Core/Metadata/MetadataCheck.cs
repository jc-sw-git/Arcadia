using System.Text;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// Every value a provider or the cache delivers is checked here before it is shown (#493): descriptions as plain text,
/// names and tags without control characters and of a sane length, genres from the list, age ratings only with a value
/// their board really has. Whatever fails a check is dropped, never shown raw.
/// </summary>
public static class MetadataCheck
{
    public const int MaxShortDescription = 1000;
    public const int MaxName = 120;
    public const int MaxNames = 10;
    public const int MaxTags = 40;
    public const int MaxTag = 40;
    public const int MaxTitle = 200;

    private static readonly DateOnly Earliest = new(1950, 1, 1);
    private static readonly DateOnly Latest = new(2100, 12, 31);

    private static readonly Dictionary<AgeRatingBoard, string[]> Ratings = new()
    {
        [AgeRatingBoard.Usk] = ["0", "6", "12", "16", "18"],
        [AgeRatingBoard.Pegi] = ["3", "7", "12", "16", "18"],
        [AgeRatingBoard.Esrb] = ["E", "E10+", "T", "M", "AO"],
    };

    /// <summary><paramref name="metadata"/> with every value checked; never throws.</summary>
    public static GameMetadata Checked(GameMetadata? metadata) => metadata is null ? GameMetadata.Empty : new GameMetadata
    {
        Description = PlainText.Of(metadata.Description),
        ShortDescription = PlainText.Of(metadata.ShortDescription, MaxShortDescription),
        Genres = [.. (metadata.Genres ?? []).Where(Enum.IsDefined).Distinct()],
        Developers = Names(metadata.Developers, MaxNames, MaxName),
        Publishers = Names(metadata.Publishers, MaxNames, MaxName),
        ReleaseDate = metadata.ReleaseDate is { } date && date >= Earliest && date <= Latest ? date : null,
        Tags = Names(metadata.Tags, MaxTags, MaxTag),
        AgeRatings = [.. (metadata.AgeRatings ?? []).Select(CheckedRating).OfType<AgeRating>().Distinct()],
    };

    /// <summary>A provider's title of a game (#494) in one line, without markup; null if it is empty or too long.</summary>
    public static string? CheckedTitle(string? title) => PlainName(title, MaxTitle);

    /// <summary>The rating with its board's spelling (<c>m</c> → <c>M</c>, <c>e10</c> → <c>E10+</c>); null for a value the board doesn't have.</summary>
    public static AgeRating? CheckedRating(AgeRating? rating)
    {
        if (rating is null || !Ratings.TryGetValue(rating.Board, out var values) || rating.Rating is null)
        {
            return null;
        }

        var value = rating.Rating.Trim().ToUpperInvariant();
        if (rating.Board == AgeRatingBoard.Esrb && value == "E10")
        {
            value = "E10+";
        }

        return values.Contains(value, StringComparer.Ordinal) ? rating with { Rating = value } : null;
    }

    private static List<string> Names(IReadOnlyList<string>? names, int maxCount, int maxLength) =>
    [
        .. (names ?? [])
            .Select(name => PlainName(name, maxLength))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(maxCount),
    ];

    /// <summary>A name in one line, without markup; null if it is empty or too long to be a name.</summary>
    private static string? PlainName(string? name, int maxLength)
    {
        if (PlainText.Of(name, maxLength * 2) is not { } text)
        {
            return null;
        }

        var line = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            line.Append(c == '\n' ? ' ' : c);
        }

        var result = line.ToString().Trim();
        return result.Length is > 0 && result.Length <= maxLength ? result : null;
    }
}
