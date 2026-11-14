using System.Runtime.InteropServices;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// A value of a long list as the modal shows it (#466): <paramref name="Key"/> is what a choice holds (a genre's key,
/// else the name in its most frequent spelling), <paramref name="Label"/> what the player reads, <paramref name="Entries"/>
/// how many of the tab's entries have it ("Häufigste zuerst").
/// </summary>
public sealed record FilterName(string Key, string Label, int Entries);

/// <summary>
/// The categories a tab offers (#44, #466): a category appears only where it tells entries apart, so "Installiert" needs
/// both installed and uninstalled entries, "Typ" more than one type, a metadata category one value at least. A tab
/// without any has no filter at all.
/// </summary>
public sealed class FilterOffer
{
    private static readonly IReadOnlyList<FilterName> NoNames = [];

    private readonly IReadOnlyList<FilterName>[] _names;
    private readonly HashSet<string>[] _keys;
    private readonly HashSet<string> _ownKeys;

    public FilterOffer(
        bool hasInstalled,
        IReadOnlyList<GameType> types,
        IReadOnlyList<FilterName>[]? names = null,
        IReadOnlyList<FilterName>? ownTags = null,
        IReadOnlyDictionary<int, int>? years = null,
        IReadOnlyList<AgeRatingBoard>? boards = null)
    {
        HasInstalled = hasInstalled;
        Types = types;
        _names = names ?? [.. Enum.GetValues<FilterList>().Select(_ => NoNames)];
        _keys = [.. _names.Select(list => list.Select(name => name.Key).ToHashSet(FilterNames.Comparer))];
        OwnTags = ownTags ?? NoNames;
        _ownKeys = OwnTags.Select(tag => tag.Key).ToHashSet(FilterNames.Comparer);
        Years = years ?? new Dictionary<int, int>();
        Boards = boards ?? [];
    }

    public bool HasInstalled { get; }

    public IReadOnlyList<GameType> Types { get; }

    public bool HasTypes => Types.Count > 1;

    /// <summary>The own tags some entry of the tab has, alphabetically, the first group of "Tags" (#466 case 4).</summary>
    public IReadOnlyList<FilterName> OwnTags { get; }

    /// <summary>The entries per release year; empty without any year.</summary>
    public IReadOnlyDictionary<int, int> Years { get; }

    public bool HasYears => Years.Count > 0;

    public int EarliestYear => HasYears ? Years.Keys.Min() : 0;

    public int LatestYear => HasYears ? Years.Keys.Max() : 0;

    /// <summary>The boards the tab has ratings of, in <see cref="AgeRatingBoard"/>'s order.</summary>
    public IReadOnlyList<AgeRatingBoard> Boards { get; }

    public bool HasRatings => Boards.Count > 0;

    public bool HasAny => HasInstalled || HasTypes || Enum.GetValues<FilterList>().Any(Has) || HasYears || HasRatings;

    /// <summary>The list's values, by <see cref="FilterName.Entries"/>, then by name; own tags are <see cref="OwnTags"/>.</summary>
    public IReadOnlyList<FilterName> NamesOf(FilterList list) => _names[(int)list];

    public bool Has(FilterList list) => _names[(int)list].Count > 0 || (list == FilterList.Tag && OwnTags.Count > 0);

    public bool Has(FilterCategory category) => category switch
    {
        FilterCategory.Installed => HasInstalled,
        FilterCategory.Type => HasTypes,
        FilterCategory.Year => HasYears,
        FilterCategory.AgeRating => HasRatings,
        FilterCategory.Genre => Has(FilterList.Genre),
        FilterCategory.Tag => Has(FilterList.Tag),
        FilterCategory.Developer => Has(FilterList.Developer),
        _ => Has(FilterList.Publisher),
    };

    public bool Holds(FilterList list, string name) => _keys[(int)list].Contains(name.Trim());

    public bool HoldsOwnTag(string name) => _ownKeys.Contains(name.Trim());

    /// <summary>
    /// The board "Altersfreigabe" starts with (#466): <paramref name="preferred"/> (the region's, #557) if the tab has it,
    /// else the first the tab has.
    /// </summary>
    public AgeRatingBoard DefaultBoard(AgeRatingBoard preferred = AgeRatingBoard.Usk) =>
        Boards.Contains(preferred) || Boards.Count == 0 ? preferred : Boards[0];

    public static FilterOffer Of(IReadOnlyList<Game> games, FilterFacts? facts = null)
    {
        facts ??= FilterFacts.None;
        bool installed = false, uninstalled = false;
        var types = new HashSet<GameType>();
        var spellings = Lists.Select(_ => new Dictionary<string, Spellings>(FilterNames.Comparer)).ToArray();
        var own = new Dictionary<string, Spellings>(FilterNames.Comparer);
        var years = new Dictionary<int, int>();
        var boards = new HashSet<AgeRatingBoard>();
        foreach (var game in games)
        {
            installed |= game.IsInstalled;
            uninstalled |= !game.IsInstalled;
            types.Add(game.Type);
            var gameFacts = facts.Of(game);
            if (ReferenceEquals(gameFacts, GameFacts.None))
            {
                continue;
            }

            foreach (var list in Lists)
            {
                Count(spellings[(int)list], gameFacts.NamesOf(list));
            }

            Count(own, gameFacts.OwnTags);
            if (gameFacts.Year is { } year)
            {
                years[year] = years.GetValueOrDefault(year) + 1;
            }

            boards.UnionWith(gameFacts.Ratings.Keys);
        }

        return new FilterOffer(
            installed && uninstalled,
            types.OrderBy(LibraryTypes.Position).ToList(),
            [.. Lists.Select(list => ByEntries(spellings[(int)list], list))],
            [.. ByEntries(own, FilterList.Tag).OrderBy(tag => tag.Label, StringComparer.CurrentCultureIgnoreCase)],
            years,
            [.. boards.Order()]);
    }

    private static readonly FilterList[] Lists = Enum.GetValues<FilterList>();

    private static void Count(Dictionary<string, Spellings> spellings, IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            ref var ofName = ref CollectionsMarshal.GetValueRefOrAddDefault(spellings, names[i], out _);
            (ofName ??= new Spellings()).Add(names[i]);
        }
    }

    /// <summary>One value per name, in its most frequent spelling (#466 case 5), the most frequent first.</summary>
    private static List<FilterName> ByEntries(Dictionary<string, Spellings> spellings, FilterList list) => [.. spellings.Values
        .Select(ofName => new FilterName(ofName.MostFrequent, LabelOf(list, ofName.MostFrequent), ofName.Entries))
        .OrderByDescending(name => name.Entries)
        .ThenBy(name => name.Label, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// The spellings of one name and how many entries use each. Nearly every name has one spelling, so the others get a
    /// dictionary only when they occur: one per name made the offer of thousands of tags too slow (#466 case 24).
    /// </summary>
    private sealed class Spellings
    {
        private string? _first;
        private int _firstCount;
        private Dictionary<string, int>? _others;

        public int Entries { get; private set; }

        public string MostFrequent => _others is null
            ? _first!
            : _others.Append(new KeyValuePair<string, int>(_first!, _firstCount))
                .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First().Key;

        public void Add(string spelling)
        {
            Entries++;
            if (_first is null || string.Equals(_first, spelling, StringComparison.Ordinal))
            {
                _first ??= spelling;
                _firstCount++;
                return;
            }

            _others ??= new Dictionary<string, int>(StringComparer.Ordinal);
            _others[spelling] = _others.GetValueOrDefault(spelling) + 1;
        }
    }

    private static string LabelOf(FilterList list, string key) =>
        list == FilterList.Genre && Enum.TryParse<Genre>(key, out var genre) ? GenreNames.Of(genre) : key;
}
