using System.Text;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>What a search by name came to (#494).</summary>
/// <param name="Taken">The one game taken without a question; null for none or several.</param>
/// <param name="Hits">Several hits for "Bei mehreren Treffern", in IGDB's order (at most <see cref="IgdbMatching.MaxHits"/>); empty otherwise.</param>
public sealed record IgdbMatch(IgdbGame? Taken, IReadOnlyList<IgdbGame> Hits)
{
    public static IgdbMatch None { get; } = new(null, []);

    public bool IsSeveral => Hits.Count > 1;
}

/// <summary>
/// Which of IGDB's search results is the launcher's game (#494): DLC, mods, bundles and the other types that are no main
/// game are dropped first (D4); a hit whose name is the same, ignoring case, punctuation and ™ ® ©, with each <c>?</c> of a
/// doubtful title standing for one character (D5), is taken when it is the only one; one main game left of any name is
/// taken too. Two or more equal, or none equal but several left, are several hits.
/// </summary>
public static class IgdbMatching
{
    /// <summary>The most hits offered (#491): two rows of the waiting list.</summary>
    public const int MaxHits = 8;

    public static IgdbMatch Of(string launcherTitle, IReadOnlyList<IgdbGame> results)
    {
        var main = results.Where(game => IgdbGameType.IsMainGame(game.Type)).ToList();
        var title = Fold(launcherTitle);
        var equal = main.Where(game => Matches(title, FoldHit(game.Name))).ToList();
        return (equal.Count, main.Count) switch
        {
            (1, _) => new IgdbMatch(equal[0], []),
            ( > 1, _) => new IgdbMatch(null, [.. equal.Take(MaxHits)]),
            (0, 1) => new IgdbMatch(main[0], []),
            (0, > 1) => new IgdbMatch(null, [.. main.Take(MaxHits)]),
            _ => IgdbMatch.None,
        };
    }

    /// <summary>A launcher title with a <c>?</c> is doubtful: Epic's catalog writes every character beyond ASCII so (#43).</summary>
    public static bool IsDoubtful(string? title) => title?.Contains('?') == true;

    // Stands in a hit's folded name for a character beyond ASCII that is no letter or digit (dash, ellipsis, curly quote,
    // ™): Epic's title has a "?" there, or nothing when the character was left out.
    private const char Sign = '\uE000';

    /// <summary>The launcher's title in lower case of its letters, digits and <c>?</c> only.</summary>
    internal static string Fold(string name)
    {
        var folded = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c) || c == '?')
            {
                folded.Append(char.ToLowerInvariant(c));
            }
        }

        return folded.ToString();
    }

    /// <summary>A hit's name in lower case of its letters and digits, each other character beyond ASCII as <see cref="Sign"/>.</summary>
    internal static string FoldHit(string name)
    {
        var folded = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c))
            {
                folded.Append(char.ToLowerInvariant(c));
            }
            else if (!char.IsAscii(c) && !char.IsWhiteSpace(c))
            {
                folded.Append(Sign);
            }
        }

        return folded.ToString();
    }

    /// <summary>
    /// The launcher's folded title against a hit's (<see cref="FoldHit"/>): each <c>?</c> stands for exactly one character
    /// beyond ASCII (D5), since Epic writes only those so; a sign of the hit no <c>?</c> stands for is left out.
    /// </summary>
    internal static bool Matches(string title, string hit)
    {
        if (title.Length == 0)
        {
            return false;
        }

        // matched[j]: the title's first i characters match the hit's first j; one row per character of the title.
        var matched = new bool[hit.Length + 1];
        matched[0] = true;
        for (var j = 1; j <= hit.Length && hit[j - 1] == Sign; j++)
        {
            matched[j] = true;
        }

        foreach (var t in title)
        {
            var next = new bool[hit.Length + 1];
            for (var j = 1; j <= hit.Length; j++)
            {
                var h = hit[j - 1];
                next[j] = (matched[j - 1] && (t == '?' ? !char.IsAscii(h) : t == h))
                    || (h == Sign && next[j - 1]);
            }

            matched = next;
        }

        return matched[hit.Length];
    }
}
