using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Search;

/// <summary>Finds games by title, the best matches first.</summary>
public static class GameSearch
{
    // "pokemon" finds "Pokémon", "HOLLOW" finds "Hollow".
    private const CompareOptions Matching = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// The games whose title contains <paramref name="term"/>: titles that start with it first,
    /// then titles with a word that starts with it, then the rest; within each group by title.
    /// Games that tie keep the order they came in, so launchers stay in tab order. An empty term finds nothing.
    /// </summary>
    public static IReadOnlyList<Game> Find(IEnumerable<Game> games, string term, CultureInfo culture)
    {
        term = term.Trim();
        if (term.Length == 0)
        {
            return [];
        }

        var titles = StringComparer.Create(culture, ignoreCase: true);
        return games
            .Select(game => (Game: game, Rank: Rank(game.Title, term, culture.CompareInfo)))
            .Where(hit => hit.Rank is not null)
            .OrderBy(hit => hit.Rank)
            .ThenBy(hit => hit.Game.Title, titles)
            .Select(hit => hit.Game)
            .ToList();
    }

    /// <summary>0: the title starts with the term, 1: a word does, 2: it's inside a word, null: no match.</summary>
    private static int? Rank(string title, string term, CompareInfo compare)
    {
        var index = compare.IndexOf(title, term, Matching);
        if (index < 0)
        {
            return null;
        }

        for (; index >= 0; index = index + 1 < title.Length ? compare.IndexOf(title, term, index + 1, Matching) : -1)
        {
            if (index == 0)
            {
                return 0;
            }

            if (!char.IsLetterOrDigit(title[index - 1]))
            {
                return 1;
            }
        }

        return 2;
    }
}
