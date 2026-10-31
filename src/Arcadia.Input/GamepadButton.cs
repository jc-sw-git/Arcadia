namespace Arcadia.Input;

/// <summary>Buttons in Xbox naming, by position: A is the bottom face button on every controller (#72).</summary>
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

    /// <summary>
    /// Raised once, on the poll thread, when the controllers stop for good after an error: whatever is held is to be
    /// dropped, not released (a released A would start a game), as no poll will let go of it.
    /// </summary>
    event Action? Lost;

    void Start();

    /// <summary>A short rumble on the controller <paramref name="padId"/>, if it has motors. Callable from any thread.</summary>
    void Rumble(int padId);

    /// <summary>
    /// Keeps the motors of <paramref name="padId"/> running at these strengths (0–65535, low and high frequency) until
    /// the next call; 0 and 0 stop them. What a game asks of the emulated pad (#238 D5). Callable from any thread.
    /// </summary>
    void HoldRumble(int padId, ushort low, ushort high);

    /// <summary>
    /// Sets the light bar of <paramref name="padId"/>, if it has one: the colour a game set on its emulated PlayStation
    /// pad (#238 A5). Callable from any thread.
    /// </summary>
    void SetLight(int padId, PadLight light);

    /// <summary>
    /// Switches the gyro and accelerometer of every controller that has them on or off, from the next poll on: on while
    /// an emulated pad carries motion (#238). Callable from any thread.
    /// </summary>
    void UseMotionSensors(bool on);

    /// <summary>
    /// Lets go of every controller (false): SDL closes them and ends, so Arcadia holds no handle and Windows lets Pan
    /// restart a controller it hides or shows (#335, check A3). No poll runs meanwhile. True reads them again from the
    /// next poll on, each with a new <see cref="GamepadReading.PadId"/>. Callable from any thread; never throws.
    /// </summary>
    void ReadControllers(bool on);

    /// <summary>
    /// A fresh look at the controllers between two polls: does one of them pass <paramref name="test"/> right now?
    /// For the desktop filter's hook, which must not miss a press Steam turned into a key before the next poll.
    /// Callable from any thread; false if it can't look quickly.
    /// </summary>
    bool AnyNow(Func<GamepadReading, bool> test);

    /// <summary>
    /// Stops (false) or resumes reading the Steam Controller and its puck; stopped, Arcadia holds no handle to them, so
    /// Windows lets Pan restart the puck when it hides it (#308). Callable from any thread; never throws.
    /// </summary>
    void ReadSteamControllers(bool on);
}

/// <summary>Used where Arcadia ships no SDL (macOS / Linux for now).</summary>
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

    public event Action? Lost
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Rumble(int padId)
    {
    }

    public void HoldRumble(int padId, ushort low, ushort high)
    {
    }

    public void SetLight(int padId, PadLight light)
    {
    }

    public void UseMotionSensors(bool on)
    {
    }

    public void ReadControllers(bool on)
    {
    }

    public bool AnyNow(Func<GamepadReading, bool> test) => false;

    public void ReadSteamControllers(bool on)
    {
    }

    public void Dispose()
    {
    }
}
