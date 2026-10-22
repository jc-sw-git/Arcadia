namespace Arcadia.Input;

/// <summary>
/// What the controller's buttons do while the mouse mode's on-screen keyboard is open (#110). The sticks,
/// the triggers and L3 stay the mouse; every other button changes its meaning.
/// </summary>
public enum KeyboardButton
{
    /// <summary>D-pad: the focus over the keys.</summary>
    Up,
    Down,
    Left,
    Right,

    /// <summary>A: the focused key.</summary>
    Type,

    /// <summary>X: backspace.</summary>
    Erase,

    /// <summary>Y.</summary>
    Space,

    /// <summary>LB: the shift level, tapped or held.</summary>
    Shift,

    /// <summary>RB: the AltGr level, tapped or held.</summary>
    AltGr,

    /// <summary>≡ alone, on release, as in the mouse mode.</summary>
    Enter,

    /// <summary>R3: the layout menu.</summary>
    Layout,

    /// <summary>⧉ tapped alone: the keyboard moves to the other edge of the screen.</summary>
    Dock,

    /// <summary>B: closes the layout menu, else the keyboard.</summary>
    Hide,
}
