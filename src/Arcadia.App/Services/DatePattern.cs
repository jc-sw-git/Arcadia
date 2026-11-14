using System.Text;

namespace Arcadia.App.Services;

/// <summary>
/// Cuts fields out of a region's custom date pattern (#592): the year of the header date, the weekday of a news entry's
/// date. The pattern is read as .NET reads it, as fields ("dddd", "yyyy") and the literal text between them, quoted
/// ("'de'"), escaped ("\.") or plain; a letter inside a literal is never taken for a field, so the result stays a pattern
/// .NET can read.
/// </summary>
internal static class DatePattern
{
    // The letters .NET reads as fields in a custom date and time pattern.
    private const string FieldLetters = "dfFghHKmMstyz";

    // What ends a literal only to part it from the next field: spaces and the commas of several scripts.
    private const string Separators = ",،፣၊";

    /// <summary>
    /// "dd.MM.yyyy" → "dd.MM.", "dddd, d 'de' MMMM 'de' yyyy" → "dddd, d 'de' MMMM", "yyyy'年'M'月'd'日'" → "M'月'd'日'".
    /// The era goes with the year ("d MMMM g yyyy" → "d MMMM"). A dot stays where it ended a German day or month ("13.11.").
    /// </summary>
    public static string WithoutYear(string pattern) =>
        Without(pattern, field => field[0] is 'y' or 'g', before => before.TrimEnd().EndsWith('.') ? "." : string.Empty);

    /// <summary>
    /// "dddd, d. MMMM yyyy" → "d. MMMM yyyy", "yyyy. MMMM d., dddd" → "yyyy. MMMM d.", "yyyy年M月d日dddd" → "yyyy年M月d日":
    /// what stands before a weekday belongs to the day, only the separator goes.
    /// </summary>
    public static string WithoutWeekday(string pattern) =>
        Without(pattern, field => field[0] == 'd' && field.Length >= 3, before => before.TrimEnd().TrimEnd(Separators.ToCharArray()).TrimEnd());

    /// <summary>The weekday and month by their abbreviated names: "dddd, d. MMMM" → "ddd, d. MMM".</summary>
    public static string Abbreviated(string pattern)
    {
        var text = new StringBuilder(pattern.Length);
        foreach (var token in Tokens(pattern))
        {
            text.Append(token.Field switch
            {
                "dddd" => "ddd",
                "MMMM" => "MMM",
                _ => token.Raw,
            });
        }

        return text.ToString();
    }

    /// <summary>Whether the pattern has a field of <paramref name="letter"/>, e.g. a day ('d') or a month ('M').</summary>
    public static bool Has(string pattern, char letter) => Tokens(pattern).Any(token => token.Field?[0] == letter);

    // Takes out every field that matches and the literal that joins it to the rest: the one after a first field, else
    // the one before, of which keptBefore says what stays; after a last field also the literal that only belongs to it
    // ("yyyy 'г.'").
    private static string Without(string pattern, Func<string, bool> matches, Func<string, string> keptBefore)
    {
        var tokens = Joined(Tokens(pattern));
        for (var i = tokens.FindIndex(t => t.Field is { } field && matches(field)); i >= 0; i = tokens.FindIndex(t => t.Field is { } field && matches(field)))
        {
            var first = !tokens.Take(i).Any(t => t.Field is not null);
            var last = !tokens.Skip(i + 1).Any(t => t.Field is not null);
            var end = i + 1 < tokens.Count && tokens[i + 1].Field is null && (first || last) ? i + 2 : i + 1;
            var start = i;
            if (!first && i > 0 && tokens[i - 1].Field is null)
            {
                start = i - 1;
            }

            var kept = start < i ? keptBefore(tokens[start].Text) : string.Empty;
            tokens.RemoveRange(start, end - start);
            if (kept.Length > 0)
            {
                tokens.Insert(start, Token.Literal(Quoted(kept), kept));
            }
        }

        // A single letter left alone ("d") would be read as a standard format ("d" = short date with the year).
        var cut = string.Concat(tokens.Select(t => t.Raw)).Trim();
        return cut.Length == 1 && FieldLetters.Contains(cut[0]) ? "%" + cut : cut;
    }

    private static string Quoted(string text) =>
        "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    // Neighbouring literals as one, so a field is joined to the rest by a single literal.
    private static List<Token> Joined(IEnumerable<Token> tokens)
    {
        var joined = new List<Token>();
        foreach (var token in tokens)
        {
            if (token.Field is null && joined.Count > 0 && joined[^1].Field is null)
            {
                joined[^1] = Token.Literal(joined[^1].Raw + token.Raw, joined[^1].Text + token.Text);
            }
            else
            {
                joined.Add(token);
            }
        }

        return joined;
    }

    private static IEnumerable<Token> Tokens(string pattern)
    {
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c is '\'' or '"')
            {
                var start = i++;
                var text = new StringBuilder();
                while (i < pattern.Length && pattern[i] != c)
                {
                    if (pattern[i] == '\\' && i + 1 < pattern.Length)
                    {
                        i++;
                    }

                    text.Append(pattern[i++]);
                }

                // An open quote stays as it is: the pattern can't be read, and the caller falls back.
                i = Math.Min(i + 1, pattern.Length);
                yield return Token.Literal(pattern[start..i], text.ToString());
            }
            else if (c == '\\')
            {
                var end = Math.Min(i + 2, pattern.Length);
                yield return Token.Literal(pattern[i..end], pattern[(i + 1)..end]);
                i = end;
            }
            else if (c == '%' && i + 1 < pattern.Length && FieldLetters.Contains(pattern[i + 1]))
            {
                // "%d": a single field on its own; the "%" belongs to it.
                var end = i + 1;
                while (end < pattern.Length && pattern[end] == pattern[i + 1])
                {
                    end++;
                }

                yield return new Token(pattern[i..end], pattern[(i + 1)..end], string.Empty);
                i = end;
            }
            else if (FieldLetters.Contains(c))
            {
                var end = i;
                while (end < pattern.Length && pattern[end] == c)
                {
                    end++;
                }

                yield return new Token(pattern[i..end], pattern[i..end], string.Empty);
                i = end;
            }
            else
            {
                yield return Token.Literal(c.ToString(), c.ToString());
                i++;
            }
        }
    }

    /// <param name="Raw">The token as it stands in the pattern.</param>
    /// <param name="Field">The field's letters ("dddd"), null for a literal.</param>
    /// <param name="Text">A literal's text as shown, without quotes and escapes.</param>
    private sealed record Token(string Raw, string? Field, string Text)
    {
        public static Token Literal(string raw, string text) => new(raw, null, text);
    }
}
