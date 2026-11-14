using System.Text.RegularExpressions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// Which of Arcadia's UI languages a text is in (#43, #557), by the short, frequent words only that language spells that
/// way, and whether it has broken characters. Stores answer in English for a game that lacks the asked language, and
/// Epic's catalog writes every umlaut as <c>?</c>.
/// </summary>
public static partial class TextLanguage
{
    // Words to recognise a language by, none of them frequent in one of the others (no "de", "la", "en", "es", "des", "du").
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Words = new Dictionary<string, HashSet<string>>
    {
        ["de"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "der", "die", "das", "und", "ist", "nicht", "mit", "ein", "eine", "einen", "dem", "den", "des", "sich", "auf", // not a UI text (words to recognise German by)
            "für", "auch", "wird", "werden", "sind", "zu", "im", "von", "oder", "deine", "dein", "du", "ihr", "euch", "noch", // not a UI text (words to recognise German by)
        },
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "is", "of", "with", "you", "your", "to", "for", "this", "that", "are", "be", "an", "on", "it",
            "from", "as", "or", "will", "can", "their", "they", "into", "who", "which", "has", "have",
        },
        ["fr"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "le", "les", "une", "est", "et", "pour", "dans", "avec", "sur", "vous", "votre", "vos", "qui", "pas", "au", // not a UI text (words to recognise French by)
            "aux", "ce", "cette", "être", "leur", "leurs", "mais", "où", "très", "sont", "nouveau", "chaque", // not a UI text (words to recognise French by)
        },
        ["es"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "el", "los", "las", "del", "una", "y", "para", "con", "por", "su", "sus", "más", "pero", "este", "esta", // not a UI text (words to recognise Spanish by)
            "como", "también", "juego", "puedes", "cada", "nuevo", "tus", "muy", // not a UI text (words to recognise Spanish by)
        },

        // Not a UI language: recognised only so that an Italian text, close to a Spanish one, is never taken for it.
        ["it"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "gli", "della", "delle", "degli", "sono", "che", "questo", "questa", "nel", "anche", "perché", "più", "di", // not a UI text (words to recognise Italian by)
        },
    };

    /// <summary>
    /// The language <paramref name="text"/> is in by its two-letter code ("de", "en", "fr", "es", or "it", which is no UI
    /// language); null when no language has more of its words in it than every other (a few words, a tie, another language).
    /// </summary>
    public static string? LanguageOf(string text)
    {
        var counts = Words.Keys.ToDictionary(language => language, _ => 0, StringComparer.Ordinal);
        foreach (Match word in Word().Matches(text))
        {
            foreach (var (language, words) in Words)
            {
                if (words.Contains(word.Value))
                {
                    counts[language]++;
                }
            }
        }

        // Letters only one of the languages writes count as a word per four.
        counts["de"] += Umlaut().Count(text) / 4;
        counts["fr"] += FrenchLetter().Count(text) / 4;
        counts["es"] += SpanishLetter().Count(text) / 4;
        var best = counts.MaxBy(count => count.Value);
        return best.Value > 0 && counts.Count(count => count.Value == best.Value) == 1 ? best.Key : null;
    }

    /// <summary>Most of its letters are Latin ones, as in every UI language; a Japanese or Russian text isn't.</summary>
    public static bool IsLatin(string text)
    {
        var letters = Letter().Count(text);
        return letters > 0 && LatinLetter().Count(text) * 2 > letters;
    }

    /// <summary>A <c>?</c> between two letters ("K?ln"): a character the source couldn't store.</summary>
    public static bool IsBroken(string text) => BrokenCharacter().IsMatch(text) || text.Contains('�');

    [GeneratedRegex(@"\p{L}\?\p{L}")]
    private static partial Regex BrokenCharacter();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex Letter();

    [GeneratedRegex(@"[\p{IsBasicLatin}\p{IsLatin-1Supplement}\p{IsLatinExtended-A}-[\P{L}]]")]
    private static partial Regex LatinLetter();

    [GeneratedRegex(@"[äöüÄÖÜß]")]
    private static partial Regex Umlaut();

    [GeneratedRegex(@"[çœèêàùÇŒÈÊÀÙ]")]
    private static partial Regex FrenchLetter();

    [GeneratedRegex(@"[ñ¿¡Ñ]")]
    private static partial Regex SpanishLetter();
}
