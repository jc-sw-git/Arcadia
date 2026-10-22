using Arcadia.Core.Search;

namespace Arcadia.App.Keyboard;

/// <summary>How a key of the on-screen keyboard shows; the part of Arcadia that uses the keyboard decides, at any time.</summary>
public enum BoardKeyState
{
    Shown,

    /// <summary>Greyed out: there, but it does nothing and the focus passes over it.</summary>
    Disabled,

    /// <summary>Left out: its place stays empty, so the keyboard keeps the real one's shape.</summary>
    Hidden,
}

/// <summary>
/// What the on-screen keyboard types into and what its own keys do. Each part of Arcadia that shows the keyboard
/// gives it one: the search its field, the mouse mode the window in front (#110), later settings or plugins.
/// </summary>
public interface IKeyboardTarget
{
    /// <summary>A character key, the space bar.</summary>
    void Type(char character);

    /// <summary>
    /// Backspace, tab, enter or an arrow. <paramref name="withShift"/>: shift is on, so tab goes back and the
    /// arrows select, as on a real keyboard (backspace never comes with it).
    /// </summary>
    void Press(BoardKeyKind key, bool withShift);

    /// <summary>"Ausblenden" or B: the keyboard is to go away.</summary>
    void Hide();

    /// <summary>"Nach oben"/"Nach unten" or ⧉ tapped; <paramref name="atTop"/> is the new edge.</summary>
    void Dock(bool atTop);
}
