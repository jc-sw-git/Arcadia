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

    /// <summary>Menu / Options / ☰ — opens the context menu.</summary>
    Menu,

    /// <summary>View / Share / ⧉.</summary>
    View,
}

/// <summary>A source of gamepad button presses, including auto-repeat for held directions.</summary>
public interface IGamepadSource : IDisposable
{
    /// <summary>Raised on a background thread; marshal to the UI thread before touching UI state.</summary>
    event Action<GamepadButton>? ButtonPressed;

    void Start();
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

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
