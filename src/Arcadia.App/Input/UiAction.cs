using Arcadia.Input;
using Avalonia.Input;

namespace Arcadia.App.Input;

/// <summary>What the user wants to do, independent of whether it came from a gamepad, keyboard or mouse.</summary>
public enum UiAction
{
    Up,
    Down,
    Left,
    Right,
    Accept,
    Back,
    PreviousTab,
    NextTab,
    PreviousPage,
    NextPage,
    ViewOptions,
    Settings,

    /// <summary>The context menu of the focused game.</summary>
    Options,

    /// <summary>Opens the search (#12); inside it, deletes the last character.</summary>
    Search,

    /// <summary>The on-screen keyboard's layout menu.</summary>
    KeyboardLayout,

    /// <summary>L3: the search's launcher filter and sort order.</summary>
    Filter,

    // The right stick: a second focus beside the D-pad's, e.g. the hits beside the on-screen keyboard.
    SecondaryUp,
    SecondaryDown,
    SecondaryLeft,
    SecondaryRight,
}

public static class UiActionMap
{
    public static UiAction? FromGamepad(GamepadButton button) => button switch
    {
        GamepadButton.DPadUp => UiAction.Up,
        GamepadButton.DPadDown => UiAction.Down,
        GamepadButton.DPadLeft => UiAction.Left,
        GamepadButton.DPadRight => UiAction.Right,
        GamepadButton.A => UiAction.Accept,
        GamepadButton.B => UiAction.Back,
        GamepadButton.LeftShoulder => UiAction.PreviousTab,
        GamepadButton.RightShoulder => UiAction.NextTab,
        GamepadButton.LeftTrigger => UiAction.PreviousPage,
        GamepadButton.RightTrigger => UiAction.NextPage,
        GamepadButton.Y => UiAction.ViewOptions,
        // The owner's choice (2026-10-17): the context menu is used far more often than the settings.
        GamepadButton.View => UiAction.Options,
        GamepadButton.Menu => UiAction.Settings,
        GamepadButton.X => UiAction.Search,
        GamepadButton.RightStickClick => UiAction.KeyboardLayout,
        GamepadButton.LeftStickClick => UiAction.Filter,
        GamepadButton.RightStickUp => UiAction.SecondaryUp,
        GamepadButton.RightStickDown => UiAction.SecondaryDown,
        GamepadButton.RightStickLeft => UiAction.SecondaryLeft,
        GamepadButton.RightStickRight => UiAction.SecondaryRight,
        _ => null,
    };

    /// <summary>
    /// The keyboard plays like a game, with one hand on WASD (owner, 2026-10-21): WASD move, Space is A, Q/E are LT/RT,
    /// Shift+Q/E LB/RB, R is ⧉, F is X, V is Y, Esc is B and Enter is ≡. The older keys stay as quiet alternatives.
    /// </summary>
    public static UiAction? FromKey(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.W, KeyModifiers.None) or (Key.Up, _) => UiAction.Up,
        (Key.S, KeyModifiers.None) or (Key.Down, _) => UiAction.Down,
        // Shift+arrows are LT/RT for keyboards without page keys (owner, 2026-10-18).
        (Key.Left, KeyModifiers.Shift) => UiAction.PreviousPage,
        (Key.Right, KeyModifiers.Shift) => UiAction.NextPage,
        (Key.A, KeyModifiers.None) or (Key.Left, _) => UiAction.Left,
        (Key.D, KeyModifiers.None) or (Key.Right, _) => UiAction.Right,
        (Key.Space, _) => UiAction.Accept,
        (Key.Enter, _) or (Key.F2, _) => UiAction.Settings,
        (Key.Escape or Key.Back, _) => UiAction.Back,
        (Key.Q, KeyModifiers.Shift) or (Key.Tab, KeyModifiers.Control | KeyModifiers.Shift) => UiAction.PreviousTab,
        (Key.E, KeyModifiers.Shift) or (Key.Tab, KeyModifiers.Control) => UiAction.NextTab,
        (Key.Q, KeyModifiers.None) or (Key.PageUp, _) => UiAction.PreviousPage,
        (Key.E, KeyModifiers.None) or (Key.PageDown, _) => UiAction.NextPage,
        (Key.V, KeyModifiers.None) => UiAction.ViewOptions,
        (Key.R, KeyModifiers.None) or (Key.Apps, _) or (Key.F10, KeyModifiers.Shift) => UiAction.Options,
        (Key.F, KeyModifiers.None or KeyModifiers.Control) => UiAction.Search,
        _ => null,
    };

    /// <summary>1 … 9 and 0 pick launcher tab 1 … 10 (0-based index), top row or number pad.</summary>
    public static int? TabNumber(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (>= Key.D1 and <= Key.D9, KeyModifiers.None) => key - Key.D1,
        (>= Key.NumPad1 and <= Key.NumPad9, KeyModifiers.None) => key - Key.NumPad1,
        (Key.D0 or Key.NumPad0, KeyModifiers.None) => 9,
        _ => null,
    };
}
