using System.Text.RegularExpressions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// The language rule (#43): a description counts as German when it reads German and has no broken characters; any other
/// counts only when no provider has a German one. Stores answer in English for a game that exists only in English, and
/// Epic's catalog writes every umlaut as <c>?</c>.
/// </summary>
public static partial class TextLanguage
{
    // Short, frequent words that are spelled this way only in one of the two languages.
    private static readonly HashSet<string> German = new(StringComparer.OrdinalIgnoreCase)
    {
        "der", "die", "das", "und", "ist", "nicht", "mit", "ein", "eine", "einen", "dem", "den", "des", "sich", "auf", // not a UI text (words to recognise German by)
        "für", "auch", "wird", "werden", "sind", "zu", "im", "von", "oder", "deine", "dein", "du", "ihr", "euch", "noch", // not a UI text (words to recognise German by)
    };

    private static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "is", "of", "with", "you", "your", "to", "for", "this", "that", "are", "be", "an", "on", "it",
        "from", "as", "or", "will", "can", "their", "they", "into", "who", "which", "has", "have",
    };

    /// <summary>Reads as German and has no broken character.</summary>
    public static bool IsGoodGerman(string text) => !IsBroken(text) && IsGerman(text);

    /// <summary>A <c>?</c> between two letters ("K?ln"): a character the source couldn't store.</summary>
    public static bool IsBroken(string text) => BrokenCharacter().IsMatch(text) || text.Contains('�');

    /// <summary>More of German's frequent words than of English's (umlauts count as German too).</summary>
    public static bool IsGerman(string text)
    {
        int german = 0, english = 0;
        foreach (Match word in Word().Matches(text))
        {
            if (German.Contains(word.Value))
            {
                german++;
            }
            else if (English.Contains(word.Value))
            {
                english++;
            }
        }

        german += Umlaut().Count(text) / 4;
        return german > english;
    }

    [GeneratedRegex(@"\p{L}\?\p{L}")]
    private static partial Regex BrokenCharacter();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();

    [GeneratedRegex(@"[äöüÄÖÜß]")]
    private static partial Regex Umlaut();
}
