using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>How many entries of one type a library holds, and how many of them are installed (#141).</summary>
public sealed record TypeCount(GameType Type, int Total, int Installed)
{
    public bool AreAllInstalled => Installed == Total;
}

/// <summary>
/// Which entries of a launcher's library are shown, by their type and the launcher's switches (#141), and how many of
/// each type there are. Filtered after the scan, so a switch acts at once without reading the launcher again.
/// </summary>
public static class LibraryTypes
{
    /// <summary>The order of the count line (owner, 2026-10-26); the filter (#44) will use it too.</summary>
    public static IReadOnlyList<GameType> Order { get; } = [GameType.Game, GameType.Software, GameType.Tool, GameType.Soundtrack];

    /// <summary>
    /// Games always; another type if one of the launcher's options that names it is switched on, or if none names it
    /// (a launcher that offers no switch for a type always shows it).
    /// </summary>
    public static bool IsShown(GameType type, IReadOnlyList<LibraryTypeOption> options, IReadOnlyCollection<string> switchedOn)
    {
        if (type == GameType.Game)
        {
            return true;
        }

        var named = false;
        foreach (var option in options)
        {
            if (option.Types.Contains(type))
            {
                if (switchedOn.Contains(option.Id))
                {
                    return true;
                }

                named = true;
            }
        }

        return !named;
    }

    /// <summary>
    /// The entries of <paramref name="games"/> that are shown. The same list when all are, so a tab's sorted copy
    /// (kept while its list stays the same) isn't sorted again.
    /// </summary>
    public static IReadOnlyList<Game> Shown(IReadOnlyList<Game> games, IReadOnlyList<LibraryTypeOption> options, IReadOnlyCollection<string> switchedOn)
    {
        if (games.All(game => game.Type == GameType.Game))
        {
            return games;
        }

        var shown = games.Where(game => IsShown(game.Type, options, switchedOn)).ToList();
        return shown.Count == games.Count ? games : shown;
    }

    /// <summary>One count per type that has entries, in <see cref="Order"/>; a type Arcadia doesn't know yet comes last.</summary>
    public static IReadOnlyList<TypeCount> Count(IEnumerable<Game> games)
    {
        var totals = new Dictionary<GameType, (int Total, int Installed)>();
        foreach (var game in games)
        {
            var (total, installed) = totals.GetValueOrDefault(game.Type);
            totals[game.Type] = (total + 1, installed + (game.IsInstalled ? 1 : 0));
        }

        return totals
            .OrderBy(entry => Order.Contains(entry.Key) ? Order.ToList().IndexOf(entry.Key) : Order.Count + (int)entry.Key)
            .Select(entry => new TypeCount(entry.Key, entry.Value.Total, entry.Value.Installed))
            .ToList();
    }
}
