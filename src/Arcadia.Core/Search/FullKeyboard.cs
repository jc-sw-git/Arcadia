namespace Arcadia.Core.Search;

/// <summary>What a key of <see cref="FullKeyboard"/> is.</summary>
public enum BoardKeyKind
{
    Character,
    Backspace,
    Tab,
    Enter,

    /// <summary>Caps lock: an empty, darker key the focus passes over; LB tapped latches shift instead.</summary>
    CapsLock,

    Shift,
    Space,
    AltGr,

    // The right block: keys no real keyboard has, then the arrows.
    Layout,
    Dock,
    Hide,
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// A key of <see cref="FullKeyboard"/>, measured in keys: a character key is 1 wide including the gap to
/// its neighbour, and every row is 1 high.
/// </summary>
/// <param name="Cap">What a character key types; null for every other key.</param>
/// <param name="LowerLeft">
/// The tall ISO Enter only: where its lower part, one row further down, begins. Both parts end at the
/// same right edge.
/// </param>
public sealed record BoardKey(BoardKeyKind Kind, int Row, double Left, double Width, KeyCap? Cap = null, double? LowerLeft = null)
{
    /// <summary>The row the key ends in; the tall ISO Enter reaches into the next one.</summary>
    public int LastRow => LowerLeft is null ? Row : Row + 1;

    /// <summary>The horizontal centre of the key's part in <paramref name="row"/>; the focus moves by these.</summary>
    public double CentreIn(int row) =>
        row != Row && LowerLeft is { } lowerLeft ? (lowerLeft + Left + Width) / 2 : Left + (Width / 2);

    /// <summary>The width of the key's part in <paramref name="row"/>.</summary>
    public double WidthIn(int row) => row != Row && LowerLeft is { } lowerLeft ? Left + Width - lowerLeft : Width;
}

/// <summary>
/// The mouse mode's on-screen keyboard (#110) as a real keyboard of the layout is built (owner, 2026-10-22):
/// DE, UK, FR and ES in ISO form, US in ANSI form, with backspace, tab, enter, caps lock, both shift keys,
/// the space bar and AltGr where they sit on it, but no Ctrl, Alt, Windows or menu key. On the right a
/// block of keys no real keyboard has (layout, dock, hide) above the arrows, laid out like a real
/// keyboard's (↑ over ← ↓ →). Five rows, each 15 keys wide; the right block 3 keys wide beside them.
/// </summary>
public sealed class FullKeyboard
{
    /// <summary>The rows of keys, from the number row to the space bar.</summary>
    public const int RowCount = 5;

    /// <summary>Where the right block begins: a little further than a key's gap past the 15 keys of the rows.</summary>
    public const double RightBlockLeft = 15.25;

    /// <summary>The width of everything, rows and right block.</summary>
    public const double Width = RightBlockLeft + 3;

    private FullKeyboard(KeyboardLayout layout, IReadOnlyList<BoardKey> keys)
    {
        Layout = layout;
        Keys = keys;
        Rows = Enumerable.Range(0, RowCount)
            .Select(row => (IReadOnlyList<BoardKey>)keys.Where(k => k.Row <= row && row <= k.LastRow).OrderBy(k => k.CentreIn(row)).ToList())
            .ToList();
    }

    public KeyboardLayout Layout { get; }

    /// <summary>Every key, each once.</summary>
    public IReadOnlyList<BoardKey> Keys { get; }

    /// <summary>The keys in each row from left to right; the tall ISO Enter is in both of its rows.</summary>
    public IReadOnlyList<IReadOnlyList<BoardKey>> Rows { get; }

    public static FullKeyboard For(KeyboardLayout layout)
    {
        var rows = layout.Rows;
        var keys = new List<BoardKey>();
        if (layout.Form == KeyboardForm.Ansi)
        {
            Expect(layout, 13, 13, 11, 10);
            NumberRow(keys, rows[0]);
            keys.Add(new BoardKey(BoardKeyKind.Tab, 1, 0, 1.5));
            Characters(keys, 1, 1.5, rows[1].Take(12));
            keys.Add(new BoardKey(BoardKeyKind.Character, 1, 13.5, 1.5, rows[1][12]));
            keys.Add(new BoardKey(BoardKeyKind.CapsLock, 2, 0, 1.75));
            Characters(keys, 2, 1.75, rows[2]);
            keys.Add(new BoardKey(BoardKeyKind.Enter, 2, 12.75, 2.25));
            keys.Add(new BoardKey(BoardKeyKind.Shift, 3, 0, 2.25));
            Characters(keys, 3, 2.25, rows[3]);
        }
        else
        {
            Expect(layout, 13, 12, 12, 11);
            NumberRow(keys, rows[0]);
            keys.Add(new BoardKey(BoardKeyKind.Tab, 1, 0, 1.5));
            Characters(keys, 1, 1.5, rows[1]);
            keys.Add(new BoardKey(BoardKeyKind.Enter, 1, 13.5, 1.5, LowerLeft: 13.75));
            keys.Add(new BoardKey(BoardKeyKind.CapsLock, 2, 0, 1.75));
            Characters(keys, 2, 1.75, rows[2]);
            keys.Add(new BoardKey(BoardKeyKind.Shift, 3, 0, 1.25));
            Characters(keys, 3, 1.25, rows[3]);
        }

        keys.Add(new BoardKey(BoardKeyKind.Shift, 3, 12.25, 2.75));

        // Ctrl, Windows and Alt would take the 3.75 keys left of the space bar.
        keys.Add(new BoardKey(BoardKeyKind.Space, 4, 3.75, 6.25));
        keys.Add(new BoardKey(BoardKeyKind.AltGr, 4, 10, 1.25));

        keys.Add(new BoardKey(BoardKeyKind.Layout, 0, RightBlockLeft, 3));
        keys.Add(new BoardKey(BoardKeyKind.Dock, 1, RightBlockLeft, 3));
        keys.Add(new BoardKey(BoardKeyKind.Hide, 2, RightBlockLeft, 3));
        keys.Add(new BoardKey(BoardKeyKind.Up, 3, RightBlockLeft + 1, 1));
        keys.Add(new BoardKey(BoardKeyKind.Left, 4, RightBlockLeft, 1));
        keys.Add(new BoardKey(BoardKeyKind.Down, 4, RightBlockLeft + 1, 1));
        keys.Add(new BoardKey(BoardKeyKind.Right, 4, RightBlockLeft + 2, 1));
        return new FullKeyboard(layout, keys);
    }

    private static void NumberRow(List<BoardKey> keys, IReadOnlyList<KeyCap> caps)
    {
        Characters(keys, 0, 0, caps);
        keys.Add(new BoardKey(BoardKeyKind.Backspace, 0, 13, 2));
    }

    private static void Characters(List<BoardKey> keys, int row, double left, IEnumerable<KeyCap> caps)
    {
        foreach (var cap in caps)
        {
            keys.Add(new BoardKey(BoardKeyKind.Character, row, left, 1, cap));
            left++;
        }
    }

    /// <summary>A layout whose rows don't fit its form would leave gaps or overlap; better to fail at once.</summary>
    private static void Expect(KeyboardLayout layout, params int[] lengths)
    {
        if (!layout.Rows.Select(r => r.Count).SequenceEqual(lengths))
        {
            throw new ArgumentException($"The rows of {layout.Id} don't fit the {layout.Form} form ({string.Join('/', lengths)} keys).", nameof(layout));
        }
    }
}
