namespace Arcadia.Input;

/// <summary>"Zurück zu Arcadia" from the settings, read on every poll.</summary>
/// <param name="Enabled">Off, View in the background does nothing.</param>
/// <param name="HoldTime">How long View must be held; at least <see cref="ReturnWithView.SoloDelay"/>.</param>
public sealed record ReturnWithViewOptions(bool Enabled, TimeSpan HoldTime);

/// <summary>
/// View held alone on any controller while Arcadia is in the background brings it back (#64), in
/// the mouse mode and without it. Feed it every poll of the gamepad source.
/// </summary>
/// <remarks>
/// View with Menu is Back + Start, the mouse mode's switch, so View must be alone. A tap does nothing here:
/// there is no "at once" (owner, 2026-10-22), so a tap is free to move the mouse mode's on-screen keyboard.
/// <see cref="Update"/> and all events run on the gamepad's poll thread.
/// </remarks>
public sealed class ReturnWithView
{
    /// <summary>View held alone this long is no start of Back + Start any more; the feedback shows from here, too.</summary>
    public static readonly TimeSpan SoloDelay = TimeSpan.FromMilliseconds(200);

    private readonly Func<ReturnWithViewOptions> _options;
    private readonly Func<bool> _arcadiaInFront;
    private readonly TimeProvider _time;

    private long? _since;
    private bool _feedback;

    // Set once View did its job or Menu joined: nothing more until View is let go.
    private bool _spent;

    /// <param name="arcadiaInFront">Read on every poll; View in Arcadia is Arcadia's own (the context menu).</param>
    public ReturnWithView(Func<ReturnWithViewOptions> options, Func<bool> arcadiaInFront, TimeProvider time)
    {
        _options = options;
        _arcadiaInFront = arcadiaInFront;
        _time = time;
    }

    /// <summary>View has been held alone past <see cref="SoloDelay"/>; the argument is the rest of the hold time.</summary>
    public event Action<TimeSpan>? HoldStarted;

    /// <summary>View was let go (or Menu joined) before the hold time.</summary>
    public event Action? HoldCancelled;

    /// <summary>Arcadia should come to the front.</summary>
    public event Action? Requested;

    public void Update(IReadOnlyList<GamepadReading> pads)
    {
        var held = PadButtons.None;
        foreach (var pad in pads)
        {
            held |= pad.Buttons;
        }

        var viewDown = (held & PadButtons.View) != 0;
        if (!viewDown)
        {
            Reset();
            return;
        }

        if (_spent)
        {
            return;
        }

        if ((held & PadButtons.Menu) != 0 || !Eligible())
        {
            Reset();
            _spent = true;
            return;
        }

        _since ??= _time.GetTimestamp();
        var holdTime = _options().HoldTime;
        var heldFor = _time.GetElapsedTime(_since.Value);
        if (heldFor >= SoloDelay && heldFor >= holdTime)
        {
            _since = null;
            _feedback = false;
            _spent = true;
            Requested?.Invoke();
        }
        else if (!_feedback && holdTime > SoloDelay && heldFor >= SoloDelay)
        {
            _feedback = true;
            HoldStarted?.Invoke(holdTime - SoloDelay);
        }
    }

    private bool Eligible() => _options().Enabled && !_arcadiaInFront();

    private void Reset()
    {
        _since = null;
        _spent = false;
        if (_feedback)
        {
            _feedback = false;
            HoldCancelled?.Invoke();
        }
    }
}
