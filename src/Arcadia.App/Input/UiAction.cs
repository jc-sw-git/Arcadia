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
        _ => null,
    };

    public static UiAction? FromKey(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.Up, _) => UiAction.Up,
        (Key.Down, _) => UiAction.Down,
        // Shift+arrows are LT/RT for keyboards without page keys (owner, 2026-10-18).
        (Key.Left, KeyModifiers.Shift) => UiAction.PreviousPage,
        (Key.Right, KeyModifiers.Shift) => UiAction.NextPage,
        (Key.Left, _) => UiAction.Left,
        (Key.Right, _) => UiAction.Right,
        (Key.Enter or Key.Space, _) => UiAction.Accept,
        (Key.Escape or Key.Back, _) => UiAction.Back,
        (Key.Tab, KeyModifiers.Control | KeyModifiers.Shift) => UiAction.PreviousTab,
        (Key.Tab, KeyModifiers.Control) => UiAction.NextTab,
        (Key.Q, KeyModifiers.None) => UiAction.PreviousTab,
        (Key.E, KeyModifiers.None) => UiAction.NextTab,
        (Key.PageUp, _) => UiAction.PreviousPage,
        (Key.PageDown, _) => UiAction.NextPage,
        (Key.V, KeyModifiers.None) => UiAction.ViewOptions,
        (Key.F2, _) => UiAction.Settings,
        (Key.Apps, _) or (Key.F10, KeyModifiers.Shift) => UiAction.Options,
        _ => null,
    };
}
