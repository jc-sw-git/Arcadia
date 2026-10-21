using System.Text.RegularExpressions;

namespace Arcadia.Input;

/// <summary>A piece of a text: plain text, or a controller button to draw as a badge, as the legend draws it.</summary>
public sealed record ButtonTextPart(string Text, bool IsButton);

/// <summary>
/// Finds the controller buttons in a text, so they can be drawn with a frame like on the controller
/// (owner, 2026-10-20). In prose the symbols ⧉ and ≡ count, and a name in brackets such as "[L3]":
/// a lone "A" there may be a word. In a list of buttons (the mouse mode's help) the bare names A, LB,
/// L3, LS, D-Pad, … count too.
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

            parts.Add(new ButtonTextPart(match.Groups["name"].Success ? match.Groups["name"].Value : match.Value, IsButton: true));
            start = match.Index + match.Length;
        }

        if (start < text.Length)
        {
            parts.Add(new ButtonTextPart(text[start..], IsButton: false));
        }

        return parts;
    }

    /// <summary>A, B, X and Y are round on the controller; the other buttons are drawn as a pill.</summary>
    public static bool IsRound(string button) => button is "A" or "B" or "X" or "Y";

    [GeneratedRegex(@"[⧉≡]|\[(?<name>LB|RB|LT|RT|L3|R3|LS|RS|A|B|X|Y|D-Pad)\]")]
    private static partial Regex Symbols();

    [GeneratedRegex(@"[⧉≡]|\[(?<name>LB|RB|LT|RT|L3|R3|LS|RS|A|B|X|Y|D-Pad)\]|\bD-Pad\b|\b(?:LB|RB|LT|RT|L3|R3|LS|RS|A|B|X|Y)\b")]
    private static partial Regex SymbolsOrNames();
}
