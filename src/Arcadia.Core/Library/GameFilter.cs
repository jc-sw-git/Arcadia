using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>The category "Installiert" of the library's filter (#44): one of three.</summary>
public enum InstalledChoice
{
    All,
    Installed,
    NotInstalled,
}

/// <summary>
/// A tab's filter (#44): AND between the categories, OR within one. An empty <see cref="Types"/> lets every type through,
/// so a filter on nothing is <see cref="None"/>. Pure, and equal by value, so a tab's filtered list can be kept while it
/// stays the same.
/// </summary>
public sealed class GameFilter : IEquatable<GameFilter>
{
    private GameFilter(InstalledChoice installed, IReadOnlyList<GameType> types)
    {
        Installed = installed;
        Types = types;
    }

    public static GameFilter None { get; } = new(InstalledChoice.All, []);

    public InstalledChoice Installed { get; }

    /// <summary>The chosen types in <see cref="LibraryTypes.Order"/>, each once; empty for "Alle Typen".</summary>
    public IReadOnlyList<GameType> Types { get; }

    public bool IsActive => Installed != InstalledChoice.All || Types.Count > 0;

    public static GameFilter Of(InstalledChoice installed, IEnumerable<GameType> types) =>
        new(Enum.IsDefined(installed) ? installed : InstalledChoice.All, InOrder(types));

    public GameFilter WithInstalled(InstalledChoice installed) => Of(installed, Types);

    /// <summary>The type chosen or not; the others keep their state.</summary>
    public GameFilter WithType(GameType type, bool chosen) =>
        Of(Installed, chosen ? Types.Append(type) : Types.Where(t => t != type));

    /// <summary>"Alle Typen": the category lets every type through again.</summary>
    public GameFilter WithAllTypes() => Of(Installed, []);

    public bool Matches(Game game) => MatchesInstalled(game) && MatchesType(game);

    /// <summary>The games the filter lets through, in their order; the same list without a filter.</summary>
    public IReadOnlyList<Game> Apply(IReadOnlyList<Game> games) => IsActive ? games.Where(Matches).ToList() : games;

    /// <summary>
    /// What is left of the filter in a tab that offers <paramref name="offer"/> (#44 case 17): a category the tab doesn't
    /// show filters nothing, and a type it doesn't hold drops out. A tab whose games change gets its saved choice back.
    /// </summary>
    public GameFilter Within(FilterOffer offer) => Of(
        offer.HasInstalled ? Installed : InstalledChoice.All,
        offer.HasTypes ? Types.Where(offer.Types.Contains) : []);

    /// <summary>
    /// This choice, made in a tab that offers <paramref name="offer"/>, laid over <paramref name="saved"/>: what the tab
    /// didn't show (a category it hides, a type it doesn't hold) stays as saved, so it comes back with its entries even
    /// after the modal was opened and closed meanwhile (#44 case 17).
    /// </summary>
    public GameFilter Over(GameFilter saved, FilterOffer offer) => Of(
        offer.HasInstalled ? Installed : saved.Installed,
        offer.HasTypes ? Types.Concat(saved.Types.Where(type => !offer.Types.Contains(type))) : saved.Types);

    /// <summary>
    /// The hits and the counts beside every value, each counted with the other category's choice (#44): one pass, so a
    /// tab of 5,000 entries costs well under a millisecond per press in the modal.
    /// </summary>
    public FilterCounts Count(IReadOnlyList<Game> games)
    {
        int hits = 0, installedAmongTyped = 0, typed = 0;
        var byType = new Dictionary<GameType, int>();
        var withInstalled = 0;
        foreach (var game in games)
        {
            var installedFits = MatchesInstalled(game);
            var typeFits = MatchesType(game);
            if (typeFits)
            {
                typed++;
                installedAmongTyped += game.IsInstalled ? 1 : 0;
            }

            if (installedFits)
            {
                withInstalled++;
                byType[game.Type] = byType.GetValueOrDefault(game.Type) + 1;
                hits += typeFits ? 1 : 0;
            }
        }

        return new FilterCounts(hits, games.Count, typed, installedAmongTyped, typed - installedAmongTyped, withInstalled, byType);
    }

    public bool Equals(GameFilter? other) => other is not null && Installed == other.Installed && Types.SequenceEqual(other.Types);

    public override bool Equals(object? obj) => Equals(obj as GameFilter);

    public override int GetHashCode() => HashCode.Combine(Installed, Types.Count, Types.Count > 0 ? Types[0] : default);

    private bool MatchesInstalled(Game game) => Installed switch
    {
        InstalledChoice.Installed => game.IsInstalled,
        InstalledChoice.NotInstalled => !game.IsInstalled,
        _ => true,
    };

    private bool MatchesType(Game game) => Types.Count == 0 || Types.Contains(game.Type);

    private static List<GameType> InOrder(IEnumerable<GameType> types) => types
        .Distinct()
        .OrderBy(LibraryTypes.Position)
        .ToList();
}

/// <summary>What the modal shows beside the values (#44): each count leaves its own category out.</summary>
/// <param name="Hits">"Mit dieser Auswahl N …".</param>
/// <param name="Total">"… von M Einträgen": every entry of the tab.</param>
/// <param name="AnyInstalledState">"Alle" under "Installiert": the entries the type choice lets through.</param>
/// <param name="WithInstalled">"Alle Typen": the entries the installed choice lets through.</param>
/// <param name="ByType">Each type's entries among <paramref name="WithInstalled"/>; a type without any is missing.</param>
public sealed record FilterCounts(
    int Hits,
    int Total,
    int AnyInstalledState,
    int Installed,
    int NotInstalled,
    int WithInstalled,
    IReadOnlyDictionary<GameType, int> ByType)
{
    public int Of(InstalledChoice choice) => choice switch
    {
        InstalledChoice.Installed => Installed,
        InstalledChoice.NotInstalled => NotInstalled,
        _ => AnyInstalledState,
    };

    public int Of(GameType type) => ByType.GetValueOrDefault(type);
}

/// <summary>
/// The categories a tab offers (#44): a category appears only where it tells entries apart, so "Installiert" needs both
/// installed and uninstalled entries and "Typ" more than one type. A tab without any has no filter at all.
/// </summary>
public sealed record FilterOffer(bool HasInstalled, IReadOnlyList<GameType> Types)
{
    public bool HasTypes => Types.Count > 1;

    public bool HasAny => HasInstalled || HasTypes;

    public static FilterOffer Of(IReadOnlyList<Game> games)
    {
        bool installed = false, uninstalled = false;
        var types = new HashSet<GameType>();
        foreach (var game in games)
        {
            installed |= game.IsInstalled;
            uninstalled |= !game.IsInstalled;
            types.Add(game.Type);
        }

        return new FilterOffer(installed && uninstalled, types.OrderBy(LibraryTypes.Position).ToList());
    }
}
