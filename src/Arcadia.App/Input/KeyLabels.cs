using System.Text;
using System.Text.RegularExpressions;
using Arcadia.App.Services;

namespace Arcadia.App.Input;

/// <summary>
/// The letter printed on a key in the user's real input layout (#588), not the on-screen keyboard's. Views and texts name
/// keys by their US QWERTY place ("WASD", "⇧ Q", "(V)"); <see cref="Printed"/> turns that into what the player's keyboard
/// shows there ("ZQSD", "⇧ A" on AZERTY). Without a reader for the layout (Linux for now) the QWERTY letters stay.
/// </summary>
public static partial class KeyLabels
{
    private static Dictionary<char, char> _printed = [];

    /// <summary>
    /// Takes the active layout: <paramref name="printedAt"/> gives the letter printed at a QWERTY letter's place, or null
    /// where the key prints no letter (a dead key, a symbol), which keeps the QWERTY letter. Raises
    /// <see cref="InputHints.Changed"/> when a letter changed, so every hint shows the new layout at once.
    /// </summary>
    public static void Use(Func<char, char?> printedAt)
    {
        var printed = new Dictionary<char, char>();
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            if (printedAt(letter) is { } shown && char.IsLetter(shown) && char.ToUpperInvariant(shown) != letter)
            {
                printed[letter] = char.ToUpperInvariant(shown);
            }
        }

        if (printed.Count == _printed.Count && printed.All(pair => _printed.TryGetValue(pair.Key, out var old) && old == pair.Value))
        {
            return;
        }

        _printed = printed;
        AppLog.Write($"Tastaturlayout: WASD QE RFGCV → {Printed("WASD")} {Printed("QE")} {Printed("RFGCV")}");
        InputHints.NotifyChanged();
    }

    /// <summary>
    /// A key label with its letters as printed on the active layout. Only words of capital letters change ("WASD", "Q" in
    /// "⇧ Q"); a key's name ("Leertaste", "Esc") or a digit ("F2") stays as it is.
    /// </summary>
    public static string Printed(string label)
    {
        if (_printed.Count == 0)
        {
            return label;
        }

        return CapitalWord().Replace(label, word => Map(word.Value));
    }

    /// <summary>
    /// A tooltip whose brackets name keys, e.g. "Vorherige Seite (Q, Bild ↑ oder Umschalt+←)": the cluster's letters in
    /// the brackets follow the layout. Single letters only, so "LT" or "L3" in a bracket stay controller buttons.
    /// </summary>
    public static string PrintedInBrackets(string text)
    {
        if (_printed.Count == 0)
        {
            return text;
        }

        return Bracket().Replace(text, bracket => ClusterLetter().Replace(bracket.Value, letter => Map(letter.Value)));
    }

    private static string Map(string letters)
    {
        var mapped = new StringBuilder(letters.Length);
        foreach (var letter in letters)
        {
            mapped.Append(_printed.TryGetValue(letter, out var shown) ? shown : letter);
        }

        return mapped.ToString();
    }

    // A whole word of capitals, e.g. "WASD", not the "E" in "Esc" or the "F" in "F2".
    [GeneratedRegex(@"(?<![\p{L}\p{N}])\p{Lu}+(?![\p{L}\p{N}])")]
    private static partial Regex CapitalWord();

    [GeneratedRegex(@"\([^()]*\)")]
    private static partial Regex Bracket();

    // The keys of the one-hand cluster (controls.md), standing alone.
    [GeneratedRegex(@"(?<![\p{L}\p{N}])[WASDQERFGCV](?![\p{L}\p{N}])")]
    private static partial Regex ClusterLetter();
}
