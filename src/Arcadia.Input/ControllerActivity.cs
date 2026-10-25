namespace Arcadia.Input;

/// <summary>
/// Whether a controller is in use right now or was a moment ago: a button or trigger held, a
/// stick pushed or a trackpad touched (Steam's desktop layout moves the mouse with the Steam Controller's
/// trackpads, #212). Written on the gamepad's poll thread, read from any thread.
/// </summary>
public sealed class ControllerActivity
{
    /// <summary>
    /// What another program makes of a controller (Steam's desktop layout) arrives a little after the
    /// controller itself, also after it was let go.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Steam's desktop layout lets the trackpad pointer glide on after the finger left the pad; the owner saw it
    /// move for up to about a second (#212, 2026-10-25). Only simulated input is dropped, so a real mouse is unaffected.
    /// </summary>
    public static readonly TimeSpan TrackpadGrace = TimeSpan.FromSeconds(1);

    /// <summary>A stick resting a little off centre isn't in use.</summary>
    private const double StickDeadZone = 0.3;

    /// <summary>⧉ + ≡: Arcadia's own gesture (the mouse mode), and Steam's desktop layout makes Alt+Tab of it.</summary>
    private const PadButtons Combo = PadButtons.View | PadButtons.Menu;

    private readonly TimeProvider _time;
    private long _lastInUse = long.MinValue;
    private long _lastCombo = long.MinValue;
    private long _lastTrackpad = long.MinValue;
    private volatile bool _inUse;
    private volatile bool _comboHeld;

    public ControllerActivity(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>How many controllers the last poll read.</summary>
    public int ConnectedPads { get; private set; }

    public void Note(IReadOnlyList<GamepadReading> pads)
    {
        bool inUse = false, comboHeld = false, trackpad = false;
        for (var i = 0; i < pads.Count; i++)
        {
            inUse |= IsInUse(pads[i]);
            comboHeld |= IsComboHeld(pads[i]);
            trackpad |= Touched(pads[i].LeftTouchpad) || Touched(pads[i].RightTouchpad);
        }

        ConnectedPads = pads.Count;
        var now = _time.GetTimestamp();
        if (inUse)
        {
            Volatile.Write(ref _lastInUse, now);
        }

        if (comboHeld)
        {
            Volatile.Write(ref _lastCombo, now);
        }

        if (trackpad)
        {
            Volatile.Write(ref _lastTrackpad, now);
        }

        _inUse = inUse;
        _comboHeld = comboHeld;
    }

    /// <summary>In use at the last poll, let go less than <see cref="Grace"/> ago, or a trackpad less than <see cref="TrackpadGrace"/> ago.</summary>
    public bool IsInUse() => _inUse || WithinGrace(ref _lastInUse, Grace) || WithinGrace(ref _lastTrackpad, TrackpadGrace);

    /// <summary>⧉ + ≡ held together on a controller at the last poll, or less than <see cref="Grace"/> ago.</summary>
    public bool IsComboHeld() => _comboHeld || WithinGrace(ref _lastCombo, Grace);

    public static bool IsInUse(GamepadReading pad) =>
        pad.Buttons != PadButtons.None || Pushed(pad.LeftStick) || Pushed(pad.RightStick) || Touched(pad.LeftTouchpad) || Touched(pad.RightTouchpad);

    public static bool IsComboHeld(GamepadReading pad) => pad.IsDown(Combo);

    private bool WithinGrace(ref long timestamp, TimeSpan grace)
    {
        var last = Volatile.Read(ref timestamp);
        return last != long.MinValue && _time.GetElapsedTime(last) <= grace;
    }

    private static bool Touched(TouchpadReading touchpad) => touchpad.Touched || touchpad.Clicked;

    private static bool Pushed(StickPosition stick) => Math.Abs(stick.X) > StickDeadZone || Math.Abs(stick.Y) > StickDeadZone;
}
