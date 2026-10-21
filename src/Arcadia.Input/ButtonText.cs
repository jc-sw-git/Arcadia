using System.Text.RegularExpressions;

namespace Arcadia.Input;

/// <summary>A piece of a text: plain text, or a controller button to draw as a badge, as the legend draws it.</summary>
public sealed record ButtonTextPart(string Text, bool IsButton);

/// <summary>
/// Finds the controller buttons in a text, so they can be drawn with a frame like on the controller
/// (owner, 2026-10-20). In prose only the symbols ⧉ and ≡ count: a lone "A" there may be a word.
/// In a list of buttons (the mouse mode's help) the names A, LB, L3, … count too.
/// </summary>
public static partial class ButtonText
{
    public static IReadOnlyList<ButtonTextPart> Split(string text, bool namesToo)
    {
        var parts = new List<ButtonTextPart>();
        var start = 0;
        foreach (Match match in (namesToo ? SymbolsOrNames() : Symbols()).Matches(text))
        {
            if (match.Index > start)
            {
                parts.Add(new ButtonTextPart(text[start..match.Index], IsButton: false));
            }

            parts.Add(new ButtonTextPart(match.Value, IsButton: true));
            start = match.Index + match.Length;
        }

        if (start < text.Length)
        {
            parts.Add(new ButtonTextPart(text[start..], IsButton: false));
        }

        return parts;
    }

    [GeneratedRegex("[⧉≡]")]
    private static partial Regex Symbols();

    [GeneratedRegex(@"[⧉≡]|\b(?:LB|RB|LT|RT|L3|R3|A|B|X|Y)\b")]
    private static partial Regex SymbolsOrNames();
}
