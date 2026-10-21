namespace Arcadia.Input;

public enum MouseButton
{
    Left,
    Right,
}

/// <summary>The keys the mouse mode presses.</summary>
public enum OutputKey
{
    Up,
    Down,
    Left,
    Right,
    Escape,
    Tab,
    Shift,
    Enter,
}

/// <summary>Where the mouse mode's pointer movement, clicks, wheel and keys go: the system's input queue.</summary>
public interface IMouseKeyboardOutput
{
    /// <summary>Moves the pointer by whole screen pixels; positive is right and down.</summary>
    void MovePointer(int dx, int dy);

    void SetButton(MouseButton button, bool down);

    /// <summary>Turns the wheel; 120 is one notch. Positive scrolls up and to the right.</summary>
    void Scroll(int vertical, int horizontal);

    /// <summary>A key down (repeated while held, as a keyboard does) or up.</summary>
    void SetKey(OutputKey key, bool down);
}

public static class MouseKeyboardOutput
{
    /// <summary>The system's input queue on Windows; nothing elsewhere for now.</summary>
    public static IMouseKeyboardOutput CreateDefault() =>
        OperatingSystem.IsWindows() ? new SendInputOutput() : new NullMouseKeyboardOutput();
}

/// <summary>Used where Arcadia can't send input (macOS / Linux for now).</summary>
public sealed class NullMouseKeyboardOutput : IMouseKeyboardOutput
{
    public void MovePointer(int dx, int dy)
    {
    }

    public void SetButton(MouseButton button, bool down)
    {
    }

    public void Scroll(int vertical, int horizontal)
    {
    }

    public void SetKey(OutputKey key, bool down)
    {
    }
}
