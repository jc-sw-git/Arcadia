namespace Arcadia.Input;

/// <summary>
/// Whether a controller is in use right now or was a moment ago: a button or trigger held, or a
/// stick pushed. Written on the gamepad's poll thread, read from any thread.
/// </summary>
public sealed class ControllerActivity
{
    /// <summary>
    /// What another program makes of a controller (Steam's desktop layout) arrives a little after the
    /// controller itself, also after it was let go.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(150);

    /// <summary>A stick resting a little off centre isn't in use.</summary>
    private const double StickDeadZone = 0.3;

    private readonly TimeProvider _time;
    private long _lastInUse = long.MinValue;
    private volatile bool _inUse;

    public ControllerActivity(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Slots that answered in the last poll; a fresh look need only ask these.</summary>
    public IReadOnlyList<int> ConnectedSlots { get; private set; } = [];

    public void Note(IReadOnlyList<GamepadReading> pads)
    {
        var inUse = false;
        var slots = new int[pads.Count];
        for (var i = 0; i < pads.Count; i++)
        {
            slots[i] = pads[i].Slot;
            inUse |= IsInUse(pads[i]);
        }

        ConnectedSlots = slots;
        if (inUse)
        {
            Volatile.Write(ref _lastInUse, _time.GetTimestamp());
        }

        _inUse = inUse;
    }

    /// <summary>In use at the last poll, or let go less than <see cref="Grace"/> ago.</summary>
    public bool IsInUse()
    {
        if (_inUse)
        {
            return true;
        }

        var last = Volatile.Read(ref _lastInUse);
        return last != long.MinValue && _time.GetElapsedTime(last) <= Grace;
    }

    public static bool IsInUse(GamepadReading pad) =>
        pad.Buttons != PadButtons.None || Pushed(pad.LeftStick) || Pushed(pad.RightStick);

    private static bool Pushed(StickPosition stick) => Math.Abs(stick.X) > StickDeadZone || Math.Abs(stick.Y) > StickDeadZone;
}
