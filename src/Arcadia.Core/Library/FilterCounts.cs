using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// What the modal shows beside the values (#44, #466): each count leaves its own category out, so it says how many
/// entries a value would bring with the other categories' choice. Filled by <see cref="GameFilter.Count"/>.
/// </summary>
public sealed class FilterCounts
{
    // Arrays once: the counts run per entry, 5,000 a press in a large tab.
    private static readonly FilterCategory[] Categories = Enum.GetValues<FilterCategory>();
    private static readonly AgeRatingBoard[] Boards = Enum.GetValues<AgeRatingBoard>();
    private static readonly int CategoryCount = Categories.Length;

    private readonly int[] _base = new int[CategoryCount];
    private readonly int[] _byType = new int[Enum.GetValues<GameType>().Length];
    private readonly Dictionary<string, int>[] _byName = [.. Enum.GetValues<FilterList>().Select(_ => new Dictionary<string, int>(FilterNames.Comparer))];
    private readonly Dictionary<string, int> _byOwnTag = new(FilterNames.Comparer);
    private readonly int[] _lacking = new int[Enum.GetValues<FilterList>().Length];
    private readonly Dictionary<int, int> _byYear = [];
    private readonly Dictionary<(AgeRatingBoard Board, string Rating), int> _byRating = [];
    private readonly int[] _lackingRating = new int[Boards.Length];
    private int _installed;
    private int _lackingYear;

    internal FilterCounts(int total)
    {
        Total = total;
    }

    /// <summary>"Mit dieser Auswahl N …".</summary>
    public int Hits { get; private set; }

    /// <summary>"… von M Einträgen": every entry of the tab.</summary>
    public int Total { get; }

    /// <summary>"Alle" under "Installiert".</summary>
    public int AnyInstalledState => _base[(int)FilterCategory.Installed];

    public int Installed => _installed;

    public int NotInstalled => AnyInstalledState - _installed;

    /// <summary>"Alle Typen": the entries every other category lets through.</summary>
    public int WithInstalled => _base[(int)FilterCategory.Type];

    /// <summary>"Alle <Kategorie>": the entries the other categories let through, whatever this one says.</summary>
    public int AllOf(FilterCategory category) => _base[(int)category];

    public int Of(InstalledChoice choice) => choice switch
    {
        InstalledChoice.Installed => Installed,
        InstalledChoice.NotInstalled => NotInstalled,
        _ => AnyInstalledState,
    };

    public int Of(GameType type) => _byType[(int)type];

    /// <summary>The entries with <paramref name="name"/> in <paramref name="list"/>; an own tag only counts its own entries.</summary>
    public int Of(FilterList list, string name, bool isOwn = false) =>
        (isOwn ? _byOwnTag : _byName[(int)list]).GetValueOrDefault(name);

    /// <summary>The entries without any value in the list: what "Ohne Angabe einbeziehen" adds.</summary>
    public int Lacking(FilterList list) => _lacking[(int)list];

    public int OfYear(int year) => _byYear.GetValueOrDefault(year);

    /// <summary>The entries from <paramref name="from"/> to <paramref name="to"/>, both included: a quick pick's hits.</summary>
    public int OfYears(int from, int to) => _byYear.Where(year => year.Key >= from && year.Key <= to).Sum(year => year.Value);

    public int LackingYear => _lackingYear;

    public int OfRating(AgeRatingBoard board, string rating) => _byRating.GetValueOrDefault((board, rating));

    /// <summary>The entries without a rating of <paramref name="board"/>, also those with only another board's.</summary>
    public int LackingRating(AgeRatingBoard board) => _lackingRating[Array.IndexOf(Boards, board)];

    internal void AddHit(Game game, GameFacts facts)
    {
        Hits++;
        foreach (var category in Categories)
        {
            Add(category, game, facts);
        }
    }

    internal void Add(FilterCategory category, Game game, GameFacts facts)
    {
        _base[(int)category]++;
        if (category > FilterCategory.Type && ReferenceEquals(facts, GameFacts.None))
        {
            // Most entries of a tab without metadata: only "ohne Angabe" grows, without a lookup.
            AddLacking(category);
            return;
        }

        switch (category)
        {
            case FilterCategory.Installed:
                _installed += game.IsInstalled ? 1 : 0;
                break;
            case FilterCategory.Type:
                _byType[(int)game.Type]++;
                break;
            case FilterCategory.Genre:
                AddNames(FilterList.Genre, facts);
                break;
            case FilterCategory.Tag:
                AddNames(FilterList.Tag, facts);
                foreach (var own in facts.OwnTags)
                {
                    _byOwnTag[own] = _byOwnTag.GetValueOrDefault(own) + 1;
                }

                break;
            case FilterCategory.Developer:
                AddNames(FilterList.Developer, facts);
                break;
            case FilterCategory.Publisher:
                AddNames(FilterList.Publisher, facts);
                break;
            case FilterCategory.Year:
                if (facts.Year is { } year)
                {
                    _byYear[year] = _byYear.GetValueOrDefault(year) + 1;
                }
                else
                {
                    _lackingYear++;
                }

                break;
            case FilterCategory.AgeRating:
                for (var i = 0; i < Boards.Length; i++)
                {
                    var board = Boards[i];
                    if (facts.Ratings.TryGetValue(board, out var rating))
                    {
                        _byRating[(board, rating)] = _byRating.GetValueOrDefault((board, rating)) + 1;
                    }
                    else
                    {
                        _lackingRating[i]++;
                    }
                }

                break;
        }
    }

    private void AddLacking(FilterCategory category)
    {
        switch (category)
        {
            case FilterCategory.Year:
                _lackingYear++;
                break;
            case FilterCategory.AgeRating:
                for (var i = 0; i < _lackingRating.Length; i++)
                {
                    _lackingRating[i]++;
                }

                break;
            default:
                _lacking[(int)ListOf(category)]++;
                break;
        }
    }

    private static FilterList ListOf(FilterCategory category) => category switch
    {
        FilterCategory.Genre => FilterList.Genre,
        FilterCategory.Tag => FilterList.Tag,
        FilterCategory.Developer => FilterList.Developer,
        _ => FilterList.Publisher,
    };

    private void AddNames(FilterList list, GameFacts facts)
    {
        if (facts.Lacks(list))
        {
            _lacking[(int)list]++;
            return;
        }

        var counts = _byName[(int)list];
        foreach (var name in facts.NamesOf(list))
        {
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }
    }
}
