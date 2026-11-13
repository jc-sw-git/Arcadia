using Arcadia.App.Services;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>A tag as the detail page shows it: the player's own (#495) drawn apart from a provider's.</summary>
public sealed record DetailTag(string Name, bool IsOwn);

/// <summary>
/// One line of "Infos" (#493): its caption, the value or an age rating badge in its board's colour (#541), and the
/// provider it came from.
/// </summary>
public sealed record DetailFact(string Caption, string Value, string Source, bool IsBadge = false, AgeRatingTone Tone = AgeRatingTone.White);

/// <summary>
/// What the detail page shows of a game's metadata (#493): the info line under the title, the short description and the
/// tags in "Übersicht", and in "Infos" the full description and every field with its source. A field nobody delivered is
/// left out; without any, the info line, the description and the tags are missing and "Infos" says there is nothing yet.
/// The player's own tags (#495) come before the providers'; a provider's tag of the same name shows once, as the own one.
/// </summary>
public sealed class DetailMetadata
{
    /// <summary>How many tags "Übersicht" names before "+n"; one line of the artboard holds about three.</summary>
    public const int OverviewTagCount = 3;

    /// <param name="ownTags">The game's own tags, alphabetically (#495).</param>
    public DetailMetadata(MergedMetadata data, IReadOnlyList<string>? ownTags = null)
    {
        OwnTags = ownTags ?? [];
        IsEmpty = data.IsEmpty && OwnTags.Count == 0;
        var genres = data.Genres?.Value.Select(GenreNames.Of).ToList();
        var year = data.ReleaseDate?.Value.Year.ToString(Formatting.Culture);
        Badge = data.AgeRatings.Count > 0 ? BadgeOf(data.AgeRatings[0].Value) : null;
        BadgeTone = data.AgeRatings.Count > 0 ? AgeRatingTones.Of(data.AgeRatings[0].Value) : AgeRatingTone.White;
        InfoLine = Joined(AppTexts.DetailInfoSeparator, genres is null ? null : List(genres), data.Developers is { } developers ? List(developers.Value) : null, year);
        RailLine = data.IsEmpty ? AppTexts.DetailInfosNone : Joined(AppTexts.DetailInfoSeparator, genres?[0], year, Badge) ?? Joined(AppTexts.DetailInfoSeparator, data.Developers?.Value[0]) ?? string.Empty;

        // Without a short description "Übersicht" shows the start of the full one, cut after its lines.
        ShortDescription = data.ShortDescription?.Value ?? data.Description?.Value;
        Description = data.Description?.Value ?? data.ShortDescription?.Value;
        DescriptionSource = (data.Description ?? data.ShortDescription)?.Source;

        var own = OwnTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Tags = [.. (data.Tags?.Value ?? []).Where(tag => !own.Contains(tag))];
        TagsSource = data.Tags?.Source;
        var all = OwnTags.Select(tag => new DetailTag(tag, IsOwn: true)).Concat(Tags.Select(tag => new DetailTag(tag, IsOwn: false))).ToList();
        OverviewTags = [.. all.Take(OverviewTagCount)];
        MoreTags = all.Count > OverviewTagCount ? AppTexts.DetailMoreTags(count: Formatting.Count(all.Count - OverviewTagCount)) : null;

        var facts = new List<DetailFact>();
        if (data.Title is { } title)
        {
            // A corrected title names its source, and the broken launcher title stands only here (#494).
            facts.Add(new(AppTexts.DetailTitleCaption, title.Value, title.Source));
            if (data.LauncherTitle is { } launcherTitle)
            {
                facts.Add(new(string.Empty, launcherTitle.Value, launcherTitle.Source));
            }
        }

        Add(facts, AppTexts.DetailGenresCaption, data.Genres, _ => List(genres!));
        Add(facts, AppTexts.DetailDevelopersCaption, data.Developers, List);
        Add(facts, AppTexts.DetailPublishersCaption, data.Publishers, List);
        Add(facts, AppTexts.DetailReleasedCaption, data.ReleaseDate, date => date.ToString("dd.MM.yyyy", Formatting.Culture));
        for (var r = 0; r < data.AgeRatings.Count; r++)
        {
            // The caption stands once, beside the first rating; each rating names its own source, the same way whether
            // the store's own rating for Germany stands in for a USK or not (#541).
            var rating = data.AgeRatings[r];
            facts.Add(new(r == 0 ? AppTexts.DetailAgeRatingCaption : string.Empty, BadgeOf(rating.Value), rating.Source, IsBadge: true, AgeRatingTones.Of(rating.Value)));
        }

        Facts = facts;
    }

    public static DetailMetadata None { get; } = new(MergedMetadata.None);

    /// <summary>No provider delivered anything (yet): a demo or plugin game, or a game the store hasn't answered for.</summary>
    public bool IsEmpty { get; }

    /// <summary>"Action, Abenteuer · Nordkai Studio · 2021"; null without genres, developer and year.</summary>
    public string? InfoLine { get; }

    /// <summary>The first age rating, e.g. "USK 16": USK before PEGI before ESRB.</summary>
    public string? Badge { get; }

    /// <summary>The colour of <see cref="Badge"/>'s border (#541).</summary>
    public AgeRatingTone BadgeTone { get; }

    public bool HasBadge => Badge is not null;

    public bool HasInfoText => InfoLine is not null;

    public bool HasInfoLine => InfoLine is not null || Badge is not null;

    /// <summary>The line below "Infos" in the rail: first genre · year · age rating, as in the artboard.</summary>
    public string RailLine { get; }

    public string? ShortDescription { get; }

    public bool HasShortDescription => ShortDescription is not null;

    public string? Description { get; }

    public bool HasDescription => Description is not null;

    /// <summary>
    /// Said in "Übersicht" and "Infos" where the description would be, when no provider has one (#531); null with one.
    /// The short description falls back to the full one and back, so both are missing together.
    /// </summary>
    public string? MissingDescription => HasDescription ? null : AppTexts.DetailNoDescription;

    /// <summary>The provider of the description, e.g. "Steam"; shown only in "Infos".</summary>
    public string? DescriptionSource { get; }

    /// <summary>The player's own tags (#495), alphabetically; in "Infos" under their own caption.</summary>
    public IReadOnlyList<string> OwnTags { get; }

    public bool HasOwnTags => OwnTags.Count > 0;

    /// <summary>Every provider's tag in the provider's order, without those the player has as own tags.</summary>
    public IReadOnlyList<string> Tags { get; }

    public bool HasTags => Tags.Count > 0;

    /// <summary>"Übersicht" shows tags: own or a provider's.</summary>
    public bool HasOverviewTags => OverviewTags.Count > 0;

    public string? TagsSource { get; }

    /// <summary>The first tags, own ones first, shown in "Übersicht"; <see cref="MoreTags"/> counts the rest.</summary>
    public IReadOnlyList<DetailTag> OverviewTags { get; }

    /// <summary>"+6"; null when all tags show.</summary>
    public string? MoreTags { get; }

    public bool HasMoreTags => MoreTags is not null;

    /// <summary>A corrected title beside the launcher's, genres, developer, publisher, release date and every age rating, each with its source.</summary>
    public IReadOnlyList<DetailFact> Facts { get; }

    public bool HasFacts => Facts.Count > 0;

    public static string BadgeOf(AgeRating rating) =>
        AppTexts.DetailAgeRating(board: rating.Board.ToString().ToUpperInvariant(), rating: rating.Rating);

    private static void Add<T>(List<DetailFact> facts, string caption, Sourced<T>? field, Func<T, string> text)
    {
        if (field is not null)
        {
            facts.Add(new(caption, text(field.Value), field.Source));
        }
    }

    private static string List(IEnumerable<string> names) => string.Join(AppTexts.DetailListSeparator, names);

    /// <summary>The parts that are there, joined; null when none is, so no stray "·" is left.</summary>
    private static string? Joined(string separator, params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return present.Count == 0 ? null : string.Join(separator, present);
    }
}
