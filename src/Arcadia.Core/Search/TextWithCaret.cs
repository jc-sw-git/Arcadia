using System.Globalization;

namespace Arcadia.Core.Search;

/// <summary>
/// A line of text and where typing goes in it, for the search field the on-screen keyboard's arrows move
/// through (#110). The caret steps over what the user sees as one character (a surrogate pair, a letter
/// with its accent), never into the middle of one.
/// </summary>
public readonly record struct TextWithCaret
{
    public TextWithCaret(string text, int caret)
    {
        Text = text;
        Caret = Math.Clamp(caret, 0, text.Length);
    }

    public string Text { get; }

    /// <summary>Characters before the caret, in UTF-16 units.</summary>
    public int Caret { get; }

    public string BeforeCaret => Text[..Caret];

    public string AfterCaret => Text[Caret..];

    /// <summary>Text with the caret at its end, where typing goes on.</summary>
    public static TextWithCaret AtEnd(string text) => new(text, text.Length);

    public TextWithCaret Insert(string typed) => new(BeforeCaret + typed + AfterCaret, Caret + typed.Length);

    /// <summary>Backspace: the character before the caret goes.</summary>
    public TextWithCaret EraseBefore()
    {
        var start = PreviousBoundary();
        return new(Text[..start] + AfterCaret, start);
    }

    public TextWithCaret Left() => new(Text, PreviousBoundary());

    public TextWithCaret Right() => new(Text, NextBoundary());

    public TextWithCaret Home() => new(Text, 0);

    public TextWithCaret End() => AtEnd(Text);

    private int PreviousBoundary()
    {
        var previous = 0;
        foreach (var boundary in Boundaries())
        {
            if (boundary >= Caret)
            {
                break;
            }

            previous = boundary;
        }

        return previous;
    }

    private int NextBoundary()
    {
        var caret = Caret;
        return Boundaries().FirstOrDefault(b => b > caret, Text.Length);
    }

    /// <summary>Where each character the user sees starts, and the end.</summary>
    private IEnumerable<int> Boundaries()
    {
        var position = 0;
        while (position < Text.Length)
        {
            yield return position;
            position += StringInfo.GetNextTextElementLength(Text, position);
        }

        yield return Text.Length;
    }
}
