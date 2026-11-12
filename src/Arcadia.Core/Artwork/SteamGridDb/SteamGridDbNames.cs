using System.Globalization;
using System.Text;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>
/// What a search by name decides for a game (#491): one game it takes without asking, several the player chooses
/// among ("Bei mehreren Treffern"), or none.
/// </summary>
public abstract record NameSearchResult
{
    public sealed record One(SteamGridDbHit Hit) : NameSearchResult;

    /// <param name="Hits">The hits in SteamGridDB's order, at most <see cref="SteamGridDbClient.MaxHits"/>.</param>
    public sealed record Several(IReadOnlyList<SteamGridDbHit> Hits) : NameSearchResult;

    public sealed record None : NameSearchResult;
}

/// <summary>
/// How a game's name is searched and matched at SteamGridDB (#491, D1): the name Arcadia shows without ™ ® ©; a hit
/// matches when its name is the same ignoring case, punctuation and those signs.
/// </summary>
public static class SteamGridDbNames
{
    private static readonly char[] Signs = ['™', '®', '©'];

    /// <summary>The name to search for: the title without ™ ® ©, trimmed, spaces collapsed; empty if nothing is left.</summary>
    public static string SearchTermOf(string title)
    {
        var term = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (Array.IndexOf(Signs, c) >= 0 || char.IsControl(c))
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (term.Length > 0 && term[^1] != ' ')
                {
                    term.Append(' ');
                }

                continue;
            }

            term.Append(c);
        }

        return term.ToString().Trim();
    }

    /// <summary>
    /// The name as it is compared: letters and digits only, lower case, accents kept (they tell names apart in other
    /// languages), every run of anything else one space.
    /// </summary>
    public static string Comparable(string name)
    {
        var comparable = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c))
            {
                comparable.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }
            else if (comparable.Length > 0 && comparable[^1] != ' ')
            {
                comparable.Append(' ');
            }
        }

        return comparable.ToString().TrimEnd();
    }

    /// <summary>
    /// D1: exactly one hit of the same name, or exactly one hit at all, is the game; several of the same name, or none of
    /// the same name but several others, are several hits (all of them offered); no hit is none.
    /// </summary>
    public static NameSearchResult Decide(string title, IReadOnlyList<SteamGridDbHit> hits)
    {
        if (hits.Count == 0)
        {
            return new NameSearchResult.None();
        }

        if (hits.Count == 1)
        {
            return new NameSearchResult.One(hits[0]);
        }

        var name = Comparable(SearchTermOf(title));
        var equal = hits.Where(h => Comparable(h.Name) == name).Take(2).ToList();

        // Several of the same name: all hits are offered, so a similar one the player means is there too.
        return equal.Count == 1 ? new NameSearchResult.One(equal[0]) : new NameSearchResult.Several(hits);
    }
}
