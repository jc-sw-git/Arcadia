using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// The choice in a category with a long list of names (#466): the chosen names (OR), for tags also own tags, and
/// "Ohne Angabe einbeziehen". Names are kept once ignoring case; nothing chosen filters nothing, and then the switch,
/// which shows only once something is chosen, is off.
/// </summary>
public sealed class NameChoice : IEquatable<NameChoice>
{
    private NameChoice(IReadOnlyList<string> names, IReadOnlyList<string> own, bool withUnknown)
    {
        Names = names;
        Own = own;
        WithUnknown = withUnknown && (names.Count > 0 || own.Count > 0);
    }

    public static NameChoice None { get; } = new([], [], false);

    /// <summary>The chosen names: a provider's tags, developers, publishers, or genres by key.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The chosen own tags (only in "Tags"); they hit only the entries the player gave them (#466 case 4).</summary>
    public IReadOnlyList<string> Own { get; }

    /// <summary>"Ohne Angabe einbeziehen": entries without any value pass as well.</summary>
    public bool WithUnknown { get; }

    public bool IsActive => Names.Count > 0 || Own.Count > 0;

    public static NameChoice Of(IEnumerable<string> names, IEnumerable<string>? own = null, bool withUnknown = false) =>
        new(Distinct(names), Distinct(own ?? []), withUnknown);

    public bool Has(string name, bool isOwn = false) => (isOwn ? Own : Names).Contains(name, FilterNames.Comparer);

    /// <summary>The name chosen or not; the others keep their state.</summary>
    public NameChoice With(string name, bool isOwn, bool chosen)
    {
        var list = isOwn ? Own : Names;
        var changed = chosen ? list.Append(name) : list.Where(other => !FilterNames.Same(other, name));
        return isOwn ? Of(Names, changed, WithUnknown) : Of(changed, Own, WithUnknown);
    }

    public NameChoice WithUnknownEntries(bool on) => new(Names, Own, on);

    /// <summary>An own tag renamed or merged into another one (#466 case 17); a deleted one passes null.</summary>
    public NameChoice WithOwnRenamed(string name, string? to) =>
        !Has(name, isOwn: true) ? this : Of(Names, Own.Where(own => !FilterNames.Same(own, name)).Concat(to is null ? [] : [to]), WithUnknown);

    public bool Matches(GameFacts facts, FilterList list)
    {
        if (!IsActive)
        {
            return true;
        }

        if (facts.Lacks(list))
        {
            return WithUnknown;
        }

        return Names.Count > 0 && facts.NamesOf(list).Any(name => Has(name))
            || Own.Count > 0 && list == FilterList.Tag && facts.OwnTags.Any(name => Has(name, isOwn: true));
    }

    public bool Equals(NameChoice? other) => other is not null && WithUnknown == other.WithUnknown
        && Names.Order(FilterNames.Comparer).SequenceEqual(other.Names.Order(FilterNames.Comparer), FilterNames.Comparer)
        && Own.Order(FilterNames.Comparer).SequenceEqual(other.Own.Order(FilterNames.Comparer), FilterNames.Comparer);

    public override bool Equals(object? obj) => Equals(obj as NameChoice);

    public override int GetHashCode() => HashCode.Combine(Names.Count, Own.Count, WithUnknown);

    private static List<string> Distinct(IEnumerable<string> names) =>
        [.. names.Select(name => name.Trim()).Where(name => name.Length > 0).Distinct(FilterNames.Comparer)];
}

/// <summary>"Erscheinungsjahr" (#466): from and to, both included, and "Ohne Angabe einbeziehen".</summary>
public sealed record YearChoice
{
    public YearChoice(int from, int to, bool withUnknown = false)
    {
        From = Math.Min(from, to);
        To = Math.Max(from, to);
        WithUnknown = withUnknown;
    }

    public int From { get; }

    public int To { get; }

    public bool WithUnknown { get; init; }

    public bool Matches(GameFacts facts) => facts.Year is { } year ? year >= From && year <= To : WithUnknown;

    /// <summary>Same range, whether "Ohne Angabe" is on or not: a quick pick is chosen by its range.</summary>
    public bool IsRange(int from, int to) => From == from && To == to;
}

/// <summary>
/// "Altersfreigabe" (#466, provisional): one board and one or more of its ratings (OR). Kept with no rating while the
/// player looks at a board, filtering nothing then. No conversion: an entry without this board's rating is "ohne Angabe".
/// </summary>
public sealed class RatingChoice : IEquatable<RatingChoice>
{
    private RatingChoice(AgeRatingBoard board, IReadOnlyList<string> ratings, bool withUnknown)
    {
        Board = board;
        Ratings = ratings;
        WithUnknown = withUnknown && ratings.Count > 0;
    }

    public AgeRatingBoard Board { get; }

    /// <summary>The chosen ratings in the board's order (<see cref="AgeRatingScale.Of"/>).</summary>
    public IReadOnlyList<string> Ratings { get; }

    public bool WithUnknown { get; }

    public bool IsActive => Ratings.Count > 0;

    /// <summary>A rating the board doesn't have drops out.</summary>
    public static RatingChoice Of(AgeRatingBoard board, IEnumerable<string> ratings, bool withUnknown = false)
    {
        var scale = AgeRatingScale.Of(board);
        var chosen = ratings.ToHashSet(StringComparer.Ordinal);
        return new RatingChoice(board, [.. scale.Where(chosen.Contains)], withUnknown);
    }

    public RatingChoice With(string rating, bool chosen) =>
        Of(Board, chosen ? Ratings.Append(rating) : Ratings.Where(other => other != rating), WithUnknown);

    public RatingChoice WithUnknownEntries(bool on) => new(Board, Ratings, on);

    public bool Matches(GameFacts facts) =>
        !IsActive || (facts.Ratings.TryGetValue(Board, out var rating) ? Ratings.Contains(rating) : WithUnknown);

    public bool Equals(RatingChoice? other) =>
        other is not null && Board == other.Board && WithUnknown == other.WithUnknown && Ratings.SequenceEqual(other.Ratings);

    public override bool Equals(object? obj) => Equals(obj as RatingChoice);

    public override int GetHashCode() => HashCode.Combine(Board, Ratings.Count, WithUnknown);
}

/// <summary>The ratings of each board in their order (#43): USK 0 … 18, PEGI 3 … 18, ESRB E … AO.</summary>
public static class AgeRatingScale
{
    public static IReadOnlyList<string> Of(AgeRatingBoard board) => board switch
    {
        AgeRatingBoard.Usk => ["0", "6", "12", "16", "18"],
        AgeRatingBoard.Pegi => ["3", "7", "12", "16", "18"],
        AgeRatingBoard.Esrb => ["E", "E10+", "T", "M", "AO"],
        _ => [],
    };
}

/// <summary>The quick picks of "Erscheinungsjahr" (#466).</summary>
public enum YearPick
{
    All,
    LastThreeYears,
    Twenties,
    Tens,
    Noughties,
    Before2000,
}

/// <summary>
/// What each quick pick means: "Letzte 3 Jahre" is this year and the two before, a decade its ten years, "Vor 2000" every
/// year up to 1999. "Alle Jahre" is no choice.
/// </summary>
public static class YearPicks
{
    /// <summary>The earliest year a release date may have (<c>MetadataCheck</c>): "Vor 2000" starts here.</summary>
    public const int Earliest = 1950;

    public static IReadOnlyList<YearPick> All { get; } = Enum.GetValues<YearPick>();

    /// <summary>The pick's range; null for "Alle Jahre".</summary>
    public static (int From, int To)? RangeOf(YearPick pick, int thisYear) => pick switch
    {
        YearPick.LastThreeYears => (thisYear - 2, thisYear),
        YearPick.Twenties => (2020, 2029),
        YearPick.Tens => (2010, 2019),
        YearPick.Noughties => (2000, 2009),
        YearPick.Before2000 => (Earliest, 1999),
        _ => null,
    };

    /// <summary>The pick <paramref name="choice"/> is, if any; null for a range set by hand.</summary>
    public static YearPick? Of(YearChoice? choice, int thisYear) => choice is null
        ? YearPick.All
        : All.Cast<YearPick?>().FirstOrDefault(pick => RangeOf(pick!.Value, thisYear) is { } range && choice.IsRange(range.From, range.To));
}
