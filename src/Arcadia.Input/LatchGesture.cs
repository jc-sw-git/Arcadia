namespace Arcadia.Input;

/// <summary>
/// A modifier like shift on a phone keyboard: tapped, it switches on or off and stays so; held,
/// it is on only while held and off again on release. A press that was used while held (a letter
/// typed) or lasted <c>holdTime</c> counts as held.
/// </summary>
/// <remarks>Call it from one thread (the UI thread).</remarks>
public sealed class LatchGesture(TimeSpan holdTime, TimeProvider time)
{
    private bool _wasOn;
    private bool _used;
    private long? _pressedAt;

    /// <summary>Raised whenever <see cref="IsOn"/> changes.</summary>
    public event Action<bool>? Changed;

    public bool IsOn { get; private set; }

    /// <summary>On at once, so a letter typed while holding it is already affected.</summary>
    public void Press()
    {
        if (_pressedAt is not null)
        {
            return;
        }

        _wasOn = IsOn;
        _used = false;
        _pressedAt = time.GetTimestamp();
        Set(true);
    }

    public void Release()
    {
        if (_pressedAt is not { } pressedAt)
        {
            return;
        }

        _pressedAt = null;
        var held = _used || time.GetElapsedTime(pressedAt) >= holdTime;
        Set(!held && !_wasOn);
    }

    /// <summary>Drops a running press as if it never happened, e.g. when the window loses focus and the release may never arrive.</summary>
    public void Cancel()
    {
        if (_pressedAt is not null)
        {
            _pressedAt = null;
            Set(_wasOn);
        }
    }

    /// <summary>The modifier applied to something, e.g. a letter was typed.</summary>
    public void Use()
    {
        if (_pressedAt is not null)
        {
            _used = true;
        }
    }

    /// <summary>A plain switch, e.g. the on-screen shift key pressed with A or clicked.</summary>
    public void Toggle()
    {
        _pressedAt = null;
        Set(!IsOn);
    }

    /// <summary>Back to off, e.g. when the search closes.</summary>
    public void Reset()
    {
        _pressedAt = null;
        Set(false);
    }

    private void Set(bool on)
    {
        if (IsOn != on)
        {
            IsOn = on;
            Changed?.Invoke(on);
        }
    }
}
