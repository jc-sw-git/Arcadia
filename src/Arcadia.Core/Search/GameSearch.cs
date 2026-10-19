using System.Globalization;
using System.Text;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Search;

/// <summary>
/// Finds games by title, the best matches first. The titles are prepared once (folded and sorted
/// by name), so a search per key press is a single ordinal pass even over thousands of games;
/// a culture-aware comparison per title took 25 ms and more for 5,000 games.
/// </summary>
public sealed class GameSearch
{
    // Left out on both sides: some launchers keep them in titles, Epic's catalog loses them, and
    // folding would turn "™" into "TM" and find "Batman" or "Hitman".
    private static readonly char[] LegalSigns = ['™', '®', '©', '℠'];

    // Letters that a culture-aware comparison ignoring accents takes for others, but that don't
    // decompose into a base letter and a mark: "strasse" finds "Straße", "soren" "Søren".
    private static readonly Dictionary<char, string> LetterVariants = new()
    {
        ['ß'] = "SS", ['ẞ'] = "SS", ['ø'] = "O", ['Ø'] = "O", ['ł'] = "L", ['Ł'] = "L", ['đ'] = "D", ['Đ'] = "D",
        ['æ'] = "AE", ['Æ'] = "AE", ['œ'] = "OE", ['Œ'] = "OE", ['’'] = "'", ['‘'] = "'",
    };

    private readonly CultureInfo _culture;

    // By name, as the library sorts; games with the same name keep the order they came in.
    private readonly IReadOnlyList<(Game Game, string Folded)> _byName;

    /// <param name="games">The games to search, launchers in tab order.</param>
    /// <param name="culture">Sorts the titles.</param>
    public GameSearch(IEnumerable<Game> games, CultureInfo culture)
    {
        _culture = culture;
        _byName = games
            .Select(game => (Game: game, Folded: Fold(game.Title)))
            .OrderBy(entry => entry.Game.Title, GameSorter.TitleComparer(culture))
            .ToList();
    }

    /// <summary>
    /// The games whose title contains <paramref name="term"/>, in <paramref name="order"/>, the library's
    /// sort orders. Without one, by relevance: titles that start with it first, then titles with a word
    /// that starts with it, then the rest; within each group by title. Games that tie keep the order
    /// they came in, so launchers stay in tab order. An empty term finds nothing. Case, accents and
    /// trademark and copyright signs count for nothing.
    /// </summary>
    /// <param name="launcherIds">Only games of these launchers; null for all.</param>
    public IReadOnlyList<Game> Find(string term, IReadOnlySet<string>? launcherIds = null, SortOrder? order = null)
    {
        var folded = Fold(term).Trim();
        if (folded.Length == 0)
        {
            return [];
        }

        // By name the hits are already in order; otherwise they go into their rank, each in name order.
        var byName = order is { Field: SortField.Name };
        var ranks = new[] { new List<Game>(), new List<Game>(), new List<Game>() };
        foreach (var (game, title) in _byName)
        {
            if ((launcherIds is null || launcherIds.Contains(game.LauncherId)) && Rank(title, folded) is { } rank)
            {
                ranks[byName ? 0 : rank].Add(game);
            }
        }

        if (byName)
        {
            if (order!.Value.Direction == SortDirection.Descending)
            {
                ranks[0].Reverse();
            }

            return ranks[0];
        }

        IReadOnlyList<Game> hits = [.. ranks[0], .. ranks[1], .. ranks[2]];
        return order is { } sort ? GameSorter.Sort(hits, sort, _culture) : hits;
    }

    /// <summary>A one-off search; to search the same games again, keep a <see cref="GameSearch"/>.</summary>
    public static IReadOnlyList<Game> Find(IEnumerable<Game> games, string term, CultureInfo culture, SortOrder? order = null) =>
        new GameSearch(games, culture).Find(term, order: order);

    /// <summary>
    /// <paramref name="text"/> as the search compares it: without legal signs, accents and case, and
    /// with compatibility forms (ligatures, superscripts, full-width letters) as plain letters.
    /// </summary>
    internal static string Fold(string text)
    {
        if (text.IndexOfAny(LegalSigns) >= 0)
        {
            text = string.Concat(text.Split(LegalSigns));
        }

        var folded = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormKD))
        {
            if (LetterVariants.TryGetValue(c, out var variant))
            {
                folded.Append(variant);
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(char.ToUpperInvariant(c));
            }
        }

        return folded.ToString();
    }

    /// <summary>0: the title starts with the term, 1: a word does, 2: it's inside a word, null: no match.</summary>
    private static int? Rank(string title, string term)
    {
        var index = title.IndexOf(term, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        for (; index >= 0; index = index + 1 < title.Length ? title.IndexOf(term, index + 1, StringComparison.Ordinal) : -1)
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
