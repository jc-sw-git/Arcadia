using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.Core.Search;

namespace Arcadia.App.Tests;

/// <summary>
/// The search's on-screen keyboard as the user works it with the controller: the D-pad walks the focus to a key, A
/// presses it. Every step is a real press; a key the D-pad can't reach fails the test.
/// </summary>
internal static class OnScreenKeys
{
    public static OnScreenKeyboardViewModel Keyboard(this ArcadiaDriver arcadia) => arcadia.Main.Search!.Keyboard;

    /// <summary>The focused key's label: its character on the shown level, or its symbol (⇧, ↵, ⌫ …).</summary>
    public static string FocusedLabel(this ArcadiaDriver arcadia) => LabelOf(arcadia.Keyboard().FocusedKey);

    /// <summary>The character keys of <paramref name="row"/> (0 = number row) from left to right, as they read now.</summary>
    public static string Row(this ArcadiaDriver arcadia, int row) =>
        string.Concat(arcadia.Keyboard().Keys.Where(k => k.IsCharacter && k.Key.Row == row).OrderBy(k => k.Key.Left).Select(k => k.Label));

    /// <summary>The character key that types <paramref name="normal"/> on the normal level.</summary>
    public static BoardKeyViewModel CharacterKey(this ArcadiaDriver arcadia, char normal) =>
        arcadia.Keyboard().Keys.Single(k => k.IsCharacter && k.Key.Cap!.Normal == normal);

    /// <summary>Walks the focus with the D-pad to the key showing <paramref name="label"/> (see <see cref="FocusedLabel"/>).</summary>
    public static ArcadiaDriver GoTo(this ArcadiaDriver arcadia, string label) =>
        arcadia.GoTo(arcadia.Keyboard().Keys.Where(k => !k.IsSkipped && LabelOf(k) == label).OrderBy(k => k.Key.Left).FirstOrDefault()
            ?? throw new InvalidOperationException($"Keine Taste „{label}“ auf der Tastatur"));

    public static ArcadiaDriver GoTo(this ArcadiaDriver arcadia, BoardKeyViewModel target)
    {
        var keyboard = arcadia.Keyboard();
        foreach (var direction in PathTo(keyboard, target))
        {
            var from = keyboard.FocusedKey;
            arcadia.Press(direction);
            if (keyboard.FocusedKey == from)
            {
                Assert.Fail($"Das D-Pad kommt von „{LabelOf(from)}“ nicht weiter zu „{LabelOf(target)}“ ({direction})");
            }
        }

        Assert.Same(target, keyboard.FocusedKey);
        return arcadia;
    }

    /// <summary>Types <paramref name="text"/> key by key with the D-pad and A, on the level the keyboard shows.</summary>
    public static ArcadiaDriver TypeOnKeys(this ArcadiaDriver arcadia, string text)
    {
        foreach (var character in text)
        {
            arcadia.GoTo(character.ToString()).Press(UiAction.Accept);
        }

        return arcadia;
    }

    /// <summary>
    /// The shortest walk of D-pad presses to <paramref name="target"/>, found with the same rules the keyboard moves by
    /// (<see cref="KeyGrid"/>) over the keys as they are now. Where a press really lands is checked by <see cref="GoTo(ArcadiaDriver, BoardKeyViewModel)"/>.
    /// </summary>
    private static IReadOnlyList<UiAction> PathTo(OnScreenKeyboardViewModel keyboard, BoardKeyViewModel target)
    {
        var views = keyboard.Keys.ToDictionary(k => k.Key);
        var rows = FullKeyboard.For(keyboard.Layout).Rows;
        var spots = Enumerable.Range(0, rows.Count)
            .Select(row => (IReadOnlyList<KeySpot>)rows[row]
                .Select(key => views[key])
                .Select(key => new KeySpot(key.Key.CentreIn(row), key.IsSkipped, key.Index, key.Key.WidthIn(row)))
                .ToList())
            .ToList();
        var focused = keyboard.FocusedKey.Key;
        var start = (focused.Row, Column: rows[focused.Row].ToList().IndexOf(focused));
        var paths = new Dictionary<(int Row, int Column), List<UiAction>> { [start] = [] };
        var queue = new Queue<(int Row, int Column)>([start]);
        while (queue.TryDequeue(out var spot))
        {
            if (views[rows[spot.Row][spot.Column]] == target)
            {
                return paths[spot];
            }

            foreach (var (direction, next) in new[]
            {
                (UiAction.Up, KeyGrid.Vertical(spots, spot.Row, spot.Column, -1)),
                (UiAction.Down, KeyGrid.Vertical(spots, spot.Row, spot.Column, 1)),
                (UiAction.Left, KeyGrid.Sideways(spots, spot.Row, spot.Column, -1)),
                (UiAction.Right, KeyGrid.Sideways(spots, spot.Row, spot.Column, 1)),
            })
            {
                if (paths.TryAdd(next, [.. paths[spot], direction]))
                {
                    queue.Enqueue(next);
                }
            }
        }

        Assert.Fail($"Das D-Pad erreicht „{LabelOf(target)}“ nicht");
        return [];
    }

    private static string LabelOf(BoardKeyViewModel key) => key.Key.Kind switch
    {
        BoardKeyKind.Character => key.Label,
        BoardKeyKind.AltGr => "AltGr",
        BoardKeyKind.Layout => "Layout",
        BoardKeyKind.Dock => "Dock",
        BoardKeyKind.Hide => "Ausblenden",
        BoardKeyKind.CapsLock => "⇪",
        _ => key.Symbol,
    };
}
