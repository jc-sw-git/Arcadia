using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>The category "Installiert" of the library's filter (#44): one of three.</summary>
public enum InstalledChoice
{
    All,
    Installed,
    NotInstalled,
}

/// <summary>Every category of the library's filter (#44), in the modal's order.</summary>
public enum FilterCategory
{
    Installed,
    Type,
    Genre,
    Tag,
    Developer,
    Publisher,
    Year,
    AgeRating,
}

/// <summary>
/// A tab's filter (#44, #466): AND between the categories, OR within one. A category with nothing chosen lets everything
/// through, so a filter on nothing is <see cref="None"/>. Pure, and equal by value, so a tab's filtered list can be kept
/// while it stays the same. The metadata categories read an entry's <see cref="GameFacts"/>.
/// </summary>
public sealed class GameFilter : IEquatable<GameFilter>
{
    private readonly NameChoice[] _lists;

    // Whether any long list is chosen: most filters choose none, and Failing runs per entry.
    private readonly bool _anyList;

    private GameFilter(InstalledChoice installed, IReadOnlyList<GameType> types, NameChoice[] lists, YearChoice? years, RatingChoice? rating)
    {
        Installed = installed;
        Types = types;
        _lists = lists;
        _anyList = lists.Any(list => list.IsActive);
        Years = years;
        Rating = rating;
    }

    public static GameFilter None { get; } = new(InstalledChoice.All, [], NoLists(), null, null);

    public InstalledChoice Installed { get; }

    /// <summary>The chosen types in <see cref="LibraryTypes.Order"/>, each once; empty for "Alle Typen".</summary>
    public IReadOnlyList<GameType> Types { get; }

    /// <summary>"Erscheinungsjahr"; null for "Alle Jahre".</summary>
    public YearChoice? Years { get; }

    /// <summary>"Altersfreigabe": the board looked at and its chosen ratings; null before a board was picked.</summary>
    public RatingChoice? Rating { get; }

    public bool IsActive => Installed != InstalledChoice.All || Types.Count > 0 || _lists.Any(list => list.IsActive) || Years is not null
        || Rating is { IsActive: true };

    /// <summary>The choice in a category with a long list of names.</summary>
    public NameChoice NamesOf(FilterList list) => _lists[(int)list];

    public static GameFilter Of(InstalledChoice installed, IEnumerable<GameType> types) =>
        new(Enum.IsDefined(installed) ? installed : InstalledChoice.All, InOrder(types), NoLists(), null, null);

    public GameFilter WithInstalled(InstalledChoice installed) =>
        new(Enum.IsDefined(installed) ? installed : InstalledChoice.All, Types, _lists, Years, Rating);

    /// <summary>The type chosen or not; the others keep their state.</summary>
    public GameFilter WithType(GameType type, bool chosen) => WithTypes(chosen ? Types.Append(type) : Types.Where(t => t != type));

    /// <summary>"Alle Typen": the category lets every type through again.</summary>
    public GameFilter WithAllTypes() => WithTypes([]);

    public GameFilter WithTypes(IEnumerable<GameType> types) => new(Installed, InOrder(types), _lists, Years, Rating);

    public GameFilter WithNames(FilterList list, NameChoice choice)
    {
        var lists = (NameChoice[])_lists.Clone();
        lists[(int)list] = choice;
        return new(Installed, Types, lists, Years, Rating);
    }

    public GameFilter WithYears(YearChoice? years) => new(Installed, Types, _lists, years, Rating);

    public GameFilter WithRating(RatingChoice? rating) => new(Installed, Types, _lists, Years, rating);

    /// <summary>An own tag renamed or merged (<paramref name="to"/> the kept name) or deleted (null), #466 case 17.</summary>
    public GameFilter WithOwnTagRenamed(string name, string? to) =>
        WithNames(FilterList.Tag, NamesOf(FilterList.Tag).WithOwnRenamed(name, to));

    public bool Matches(Game game, GameFacts facts) => Failing(game, facts) == 0;

    /// <summary>The games the filter lets through, in their order; the same list without a filter.</summary>
    public IReadOnlyList<Game> Apply(IReadOnlyList<Game> games, FilterFacts? facts = null)
    {
        if (!IsActive)
        {
            return games;
        }

        facts ??= FilterFacts.None;
        return games.Where(game => Matches(game, facts.Of(game))).ToList();
    }

    /// <summary>
    /// What is left of the filter in a tab that offers <paramref name="offer"/> (#44 case 17): a category the tab doesn't
    /// show filters nothing, and a value it doesn't hold drops out. A tab whose games change gets its saved choice back.
    /// </summary>
    public GameFilter Within(FilterOffer offer) => new(
        offer.HasInstalled ? Installed : InstalledChoice.All,
        offer.HasTypes ? Types.Where(offer.Types.Contains).ToList() : [],
        [.. Enum.GetValues<FilterList>().Select(list => Held(list, offer))],
        offer.HasYears ? Years : null,
        Rating is { } rating && offer.Boards.Contains(rating.Board) ? rating : null);

    /// <summary>
    /// This choice, made in a tab that offers <paramref name="offer"/>, laid over <paramref name="saved"/>: what the tab
    /// didn't show (a category it hides, a value it doesn't hold) stays as saved, so it comes back with its entries even
    /// after the modal was opened and closed meanwhile (#44 case 17).
    /// </summary>
    public GameFilter Over(GameFilter saved, FilterOffer offer) => new(
        offer.HasInstalled ? Installed : saved.Installed,
        offer.HasTypes ? InOrder(Types.Concat(saved.Types.Where(type => !offer.Types.Contains(type)))) : saved.Types,
        [.. Enum.GetValues<FilterList>().Select(list => Over(list, saved.NamesOf(list), offer))],
        offer.HasYears ? Years : saved.Years,
        // A board the tab has no ratings of wasn't shown: its saved choice stays (case 18 of #466).
        Rating is { IsActive: true } || saved.Rating is not { } kept || offer.Boards.Contains(kept.Board) ? Rating : kept);

    /// <summary>
    /// The hits and the counts beside every value, each counted with the other categories' choice (#44): one pass, each
    /// entry counted where at most one category stops it.
    /// </summary>
    public FilterCounts Count(IReadOnlyList<Game> games, FilterFacts? facts = null)
    {
        facts ??= FilterFacts.None;
        var counts = new FilterCounts(games.Count);
        foreach (var game in games)
        {
            var gameFacts = facts.Of(game);
            var failing = Failing(game, gameFacts);
            if (failing == 0)
            {
                counts.AddHit(game, gameFacts);
            }
            else if ((failing & (failing - 1)) == 0)
            {
                // Stopped by exactly one category: it counts beside that category's values only.
                counts.Add((FilterCategory)System.Numerics.BitOperations.TrailingZeroCount(failing), game, gameFacts);
            }
        }

        return counts;
    }

    public bool Equals(GameFilter? other) => other is not null && Installed == other.Installed && Types.SequenceEqual(other.Types)
        && _lists.SequenceEqual(other._lists) && Equals(Years, other.Years) && Equals(Rating, other.Rating);

    public override bool Equals(object? obj) => Equals(obj as GameFilter);

    public override int GetHashCode() => HashCode.Combine(Installed, Types.Count, Types.Count > 0 ? Types[0] : default, Years);

    /// <summary>The categories that stop the entry, one bit per <see cref="FilterCategory"/>; 0 lets it through.</summary>
    private int Failing(Game game, GameFacts facts)
    {
        var failing = 0;
        if (!MatchesInstalled(game))
        {
            failing |= Bit(FilterCategory.Installed);
        }

        if (Types.Count > 0 && !Types.Contains(game.Type))
        {
            failing |= Bit(FilterCategory.Type);
        }

        foreach (var list in _anyList ? Lists : [])
        {
            if (!_lists[(int)list].Matches(facts, list))
            {
                failing |= Bit(CategoryOf(list));
            }
        }

        if (Years is { } years && !years.Matches(facts))
        {
            failing |= Bit(FilterCategory.Year);
        }

        if (Rating is { } rating && !rating.Matches(facts))
        {
            failing |= Bit(FilterCategory.AgeRating);
        }

        return failing;
    }

    public static FilterCategory CategoryOf(FilterList list) => list switch
    {
        FilterList.Genre => FilterCategory.Genre,
        FilterList.Tag => FilterCategory.Tag,
        FilterList.Developer => FilterCategory.Developer,
        _ => FilterCategory.Publisher,
    };

    private static int Bit(FilterCategory category) => 1 << (int)category;

    private bool MatchesInstalled(Game game) => Installed switch
    {
        InstalledChoice.Installed => game.IsInstalled,
        InstalledChoice.NotInstalled => !game.IsInstalled,
        _ => true,
    };

    private NameChoice Held(FilterList list, FilterOffer offer)
    {
        var choice = NamesOf(list);
        return choice.IsActive
            ? NameChoice.Of(choice.Names.Where(name => offer.Holds(list, name)), choice.Own.Where(offer.HoldsOwnTag), choice.WithUnknown)
            : choice;
    }

    private NameChoice Over(FilterList list, NameChoice saved, FilterOffer offer)
    {
        var choice = NamesOf(list);
        return NameChoice.Of(
            choice.Names.Concat(saved.Names.Where(name => !offer.Holds(list, name))),
            choice.Own.Concat(saved.Own.Where(own => !offer.HoldsOwnTag(own))),
            // The switch belongs to what is chosen: the shown values' if any is left, else the saved ones'.
            choice.IsActive ? choice.WithUnknown : saved.WithUnknown);
    }

    // Once, not per entry: Failing runs for every entry of the tab at each count.
    private static readonly FilterList[] Lists = Enum.GetValues<FilterList>();

    private static NameChoice[] NoLists() => [.. Enum.GetValues<FilterList>().Select(_ => NameChoice.None)];

    private static List<GameType> InOrder(IEnumerable<GameType> types) => types
        .Distinct()
        .OrderBy(LibraryTypes.Position)
        .ToList();
}
