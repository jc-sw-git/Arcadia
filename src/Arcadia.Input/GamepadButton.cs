namespace Arcadia.Input;

/// <summary>Buttons in Xbox naming. PlayStation and Steam controllers map onto the same layout.</summary>
public enum GamepadButton
{
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    LeftStickClick,
    RightStickClick,

    /// <summary>Menu / Options / ☰ — opens the settings.</summary>
    Menu,

    /// <summary>View / Share / ⧉ — opens the context menu.</summary>
    View,

    // The right stick as four directions; the left one counts as the D-pad.
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
}

/// <summary>A source of gamepad button presses and releases, including auto-repeat for held directions.</summary>
public interface IGamepadSource : IDisposable
{
    /// <summary>Raised on a background thread; marshal to the UI thread before touching UI state.</summary>
    event Action<GamepadButton>? ButtonPressed;

    /// <summary>Raised once when a pressed button is let go; same thread rules as <see cref="ButtonPressed"/>.</summary>
    event Action<GamepadButton>? ButtonReleased;

    /// <summary>
    /// Raised on every poll, before that poll's button events and on the same thread, with what each
    /// connected controller reports (sticks included). The list is only valid during the call.
    /// </summary>
    event Action<IReadOnlyList<GamepadReading>>? Polled;

    void Start();

    /// <summary>A short rumble on the controller in <paramref name="slot"/>, if it has motors. Callable from any thread.</summary>
    void Rumble(int slot);
}

public static class GamepadSource
{
    /// <summary>The best source for the current platform.</summary>
    public static IGamepadSource CreateDefault() =>
        OperatingSystem.IsWindows() ? new XInputGamepadSource() : new NullGamepadSource();
}

/// <summary>Used where no gamepad backend exists yet (macOS / Linux before SDL support).</summary>
public sealed class NullGamepadSource : IGamepadSource
{
    public event Action<GamepadButton>? ButtonPressed
    {
        add { }
        remove { }
    }

    public event Action<GamepadButton>? ButtonReleased
    {
        add { }
        remove { }
    }

    public event Action<IReadOnlyList<GamepadReading>>? Polled
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Rumble(int slot)
    {
    }

    public void Dispose()
    {
    }
}
