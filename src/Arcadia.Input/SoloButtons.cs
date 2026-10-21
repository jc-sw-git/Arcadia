namespace Arcadia.Input;

/// <summary>
/// Two buttons that together form a combination of their own (View + Menu switch the mouse mode).
/// Each acts alone only if the other doesn't join in: when it is let go, or once it has been held
/// alone for <c>soloDelay</c>, so that a hold still works (≡ held on a search hit). Pressed
/// together, neither acts. Without a <c>soloDelay</c> they act only when let go.
/// </summary>
/// <remarks>Call it from one thread (the gamepad's poll thread); <see cref="Tick"/> on every poll.</remarks>
public sealed class SoloButtons(GamepadButton first, GamepadButton second, TimeSpan? soloDelay, TimeProvider time)
{
    private enum Phase
    {
        Up,

        /// <summary>Down alone; not passed on yet.</summary>
        Waiting,

        /// <summary>Its press was passed on; its release will be too.</summary>
        Acting,

        /// <summary>Part of the combination; does nothing until let go.</summary>
        Joined,
    }

    private readonly Phase[] _phases = new Phase[2];
    private readonly long[] _pressedAt = new long[2];

    /// <summary>The button acts alone; <see cref="Released"/> follows, at once for a tap.</summary>
    public event Action<GamepadButton>? Pressed;

    public event Action<GamepadButton>? Released;

    /// <summary>
    /// A button already acting (held alone past the delay) was joined by the other one: drop what its
    /// press started without acting on a release, e.g. the ring of a hold.
    /// </summary>
    public event Action<GamepadButton>? Cancelled;

    public bool Handles(GamepadButton button) => button == first || button == second;

    public void Press(GamepadButton button)
    {
        var index = IndexOf(button);
        if (_phases[index] != Phase.Up)
        {
            return;
        }

        var other = 1 - index;
        if (_phases[other] == Phase.Up)
        {
            _phases[index] = Phase.Waiting;
            _pressedAt[index] = time.GetTimestamp();
            return;
        }

        _phases[index] = Phase.Joined;
        if (_phases[other] == Phase.Acting)
        {
            Cancelled?.Invoke(ButtonAt(other));
        }

        _phases[other] = Phase.Joined;
    }

    public void Release(GamepadButton button)
    {
        var index = IndexOf(button);
        var phase = _phases[index];
        _phases[index] = Phase.Up;
        if (phase == Phase.Waiting)
        {
            Pressed?.Invoke(button);
            Released?.Invoke(button);
        }
        else if (phase == Phase.Acting)
        {
            Released?.Invoke(button);
        }
    }

    /// <summary>Passes on a press that has been alone for the delay.</summary>
    public void Tick()
    {
        if (soloDelay is not { } delay)
        {
            return;
        }

        for (var index = 0; index < 2; index++)
        {
            if (_phases[index] == Phase.Waiting && time.GetElapsedTime(_pressedAt[index]) >= delay)
            {
                _phases[index] = Phase.Acting;
                Pressed?.Invoke(ButtonAt(index));
            }
        }
    }

    /// <summary>Forgets both buttons without acting; a release that follows does nothing.</summary>
    public void Reset()
    {
        _phases[0] = _phases[1] = Phase.Up;
    }

    private int IndexOf(GamepadButton button) =>
        button == first ? 0
        : button == second ? 1
        : throw new ArgumentException($"{button} is neither {first} nor {second}.", nameof(button));

    private GamepadButton ButtonAt(int index) => index == 0 ? first : second;
}
