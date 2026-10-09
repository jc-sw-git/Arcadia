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
        GamepadButton.View => UiAction.Settings,
        _ => null,
    };

    public static UiAction? FromKey(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.Up, _) => UiAction.Up,
        (Key.Down, _) => UiAction.Down,
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
        _ => null,
    };
}
