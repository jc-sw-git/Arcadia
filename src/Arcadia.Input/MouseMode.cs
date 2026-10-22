namespace Arcadia.Input;

/// <summary>The settings the mouse mode reads on every poll (from "Mausmodus" in the settings).</summary>
/// <param name="Enabled">"Mausmodus" not "Aus": off, the mode ends and nothing switches it on.</param>
/// <param name="HoldTime">"Haltedauer": how long Back + Start must be held.</param>
/// <param name="PointerSpeed">"Zeigergeschwindigkeit", 1–9.</param>
/// <param name="BlockedHere">
/// A game is in front in which "Beenden des Mausmodus in Spielen erzwingen" forbids it: the mode ends,
/// and Back + Start doesn't switch it on (owner, 2026-10-20).
/// </param>
/// <param name="ReturnHoldTime">
/// "Zurück zu Arcadia"'s hold time, null when it is off; with the on-screen keyboard open, View let go
/// sooner moves the keyboard to the other edge.
/// </param>
public sealed record MouseModeOptions(bool Enabled, TimeSpan HoldTime, int PointerSpeed, bool BlockedHere = false, TimeSpan? ReturnHoldTime = null);

/// <summary>The mouse mode went on or off.</summary>
/// <param name="Slot">The controller whose Back + Start switched it; null when Arcadia switched it by itself.</param>
/// <param name="ByGame">Ended because a game in which it is forbidden came to the front.</param>
public sealed record MouseModeSwitch(bool IsOn, int? Slot, bool ByGame = false)
{
    public bool IsAutomatic => Slot is null;
}

/// <summary>
/// Turns the controllers into a mouse (#64): Back + Start, held on one controller for the hold time,
/// switches between the mouse mode and the normal controller, also while Arcadia is in the
/// background. Feed it every poll of the gamepad source.
/// </summary>
/// <remarks>
/// <see cref="Update"/> and all events run on the gamepad's poll thread, so the pointer moves even
/// while Arcadia's UI thread is busy. <see cref="IsOn"/> and <see cref="Request"/> work from any thread.
/// </remarks>
public sealed class MouseMode
{
    /// <summary>The edge of the screen starts to fill after this, as the ring around a held A does.</summary>
    public static readonly TimeSpan FeedbackDelay = TimeSpan.FromMilliseconds(200);

    private const PadButtons Combo = PadButtons.View | PadButtons.Menu;

    private readonly Func<MouseModeOptions> _options;
    private readonly TimeProvider _time;
    private readonly MouseModeMapping _mapping;

    // Slots whose combination already switched; they must let go before it counts again.
    private readonly HashSet<int> _spentCombos = [];
    private int? _comboSlot;
    private long _comboSince;
    private bool _comboFeedback;
    private PadButtons _held;

    private volatile bool _isOn;
    private volatile bool _isAutomatic;

    // A switch asked for from another thread, applied on the next poll: 0 none, 1 on, 2 off.
    private int _request;

    public MouseMode(IMouseKeyboardOutput output, Func<MouseModeOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
        _mapping = new MouseModeMapping(output, time);
        _mapping.KeyboardOpened += () => KeyboardOpened?.Invoke();
        _mapping.KeyboardButtonChanged += (button, down) => KeyboardButtonChanged?.Invoke(button, down);
    }

    /// <summary>Back + Start have been held past <see cref="FeedbackDelay"/>; the argument is the rest of the hold time.</summary>
    public event Action<TimeSpan>? HoldStarted;

    /// <summary>Back or Start was let go before the hold time; the feedback runs back.</summary>
    public event Action? HoldCancelled;

    public event Action<MouseModeSwitch>? Switched;

    /// <summary>Back + Start were held long enough, but the game in front forbids the mode; the argument is the controller's slot.</summary>
    public event Action<int>? Refused;

    /// <summary>Y opened the on-screen keyboard (#110); the buttons are the keyboard's until <see cref="CloseKeyboard"/> or the mode ends.</summary>
    public event Action? KeyboardOpened;

    /// <summary>A button of the open keyboard went down (true) or up; see <see cref="KeyboardButton"/>.</summary>
    public event Action<KeyboardButton, bool>? KeyboardButtonChanged;

    public bool IsOn => _isOn;

    /// <summary>The on-screen keyboard is open; it closes with the mode. Readable from any thread.</summary>
    public bool IsKeyboardOpen => _mapping.IsKeyboardOpen;

    /// <summary>On because Arcadia switched it on by itself (and may switch it off again by itself).</summary>
    public bool IsAutomatic => _isAutomatic;

    /// <summary>Back + Start are held on a controller and may switch the mode yet.</summary>
    public bool IsComboHeld => _comboSlot is not null;

    /// <summary>Arcadia switches by itself (the automation in the settings); applied on the next poll.</summary>
    public void Request(bool on) => Interlocked.Exchange(ref _request, on ? 1 : 2);

    /// <summary>The keyboard closed (B, or Arcadia closed it); from the next poll the buttons are the mouse's again. Callable from any thread.</summary>
    public void CloseKeyboard() => _mapping.CloseKeyboard();

    public void Update(IReadOnlyList<GamepadReading> pads)
    {
        var options = _options();
        _held = PadButtons.None;
        foreach (var pad in pads)
        {
            _held |= pad.Buttons;
        }

        switch (Interlocked.Exchange(ref _request, 0))
        {
            case 1 when options.Enabled && !options.BlockedHere:
                Switch(true, slot: null);
                break;
            case 2:
                Switch(false, slot: null);
                break;
        }

        // Switched off in the settings while on: the controller is a controller again at once.
        if (!options.Enabled)
        {
            Switch(false, slot: null);
        }
        else if (options.BlockedHere)
        {
            Switch(false, slot: null, byGame: true);
        }

        UpdateCombo(pads, options);
        if (_isOn)
        {
            _mapping.Update(pads, options.PointerSpeed, options.ReturnHoldTime);
        }
    }

    /// <summary>Lets go of everything the mode holds down, e.g. when Arcadia closes in the middle of a drag.</summary>
    public void Stop() => Switch(false, slot: null, announce: false);

    private void UpdateCombo(IReadOnlyList<GamepadReading> pads, MouseModeOptions options)
    {
        if (_spentCombos.Count > 0)
        {
            _spentCombos.RemoveWhere(slot => !pads.Any(p => p.Slot == slot && p.IsDown(Combo)));
        }

        if (!options.Enabled)
        {
            CancelCombo();
            return;
        }

        if (_comboSlot is { } comboSlot)
        {
            if (!pads.Any(p => p.Slot == comboSlot && p.IsDown(Combo)))
            {
                CancelCombo();
            }
            else
            {
                var holdTime = options.HoldTime < FeedbackDelay ? FeedbackDelay : options.HoldTime;
                var held = _time.GetElapsedTime(_comboSince);
                if (held >= holdTime)
                {
                    _comboSlot = null;
                    _comboFeedback = false;
                    _spentCombos.Add(comboSlot);
                    if (!_isOn && options.BlockedHere)
                    {
                        Refused?.Invoke(comboSlot);
                    }
                    else
                    {
                        Switch(!_isOn, comboSlot);
                    }
                }
                else if (!_comboFeedback && held >= FeedbackDelay)
                {
                    _comboFeedback = true;
                    HoldStarted?.Invoke(holdTime - FeedbackDelay);
                }
            }
        }

        if (_comboSlot is null)
        {
            foreach (var pad in pads)
            {
                if (pad.IsDown(Combo) && !_spentCombos.Contains(pad.Slot))
                {
                    _comboSlot = pad.Slot;
                    _comboSince = _time.GetTimestamp();
                    break;
                }
            }
        }
    }

    private void CancelCombo()
    {
        _comboSlot = null;
        if (_comboFeedback)
        {
            _comboFeedback = false;
            HoldCancelled?.Invoke();
        }
    }

    private void Switch(bool on, int? slot, bool announce = true, bool byGame = false)
    {
        if (_isOn == on)
        {
            return;
        }

        if (on)
        {
            _mapping.Begin(_held);
        }
        else
        {
            _mapping.End();
        }

        _isAutomatic = on && slot is null;
        _isOn = on;
        if (announce)
        {
            Switched?.Invoke(new MouseModeSwitch(on, slot, byGame));
        }
    }
}
