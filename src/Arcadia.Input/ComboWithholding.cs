namespace Arcadia.Input;

/// <summary>Which of Arcadia's own combos are switched on, so a game must not see them (#213).</summary>
/// <param name="BackAndStart">The mouse mode is not "Aus": Back + Start is Arcadia's.</param>
public sealed record ComboWithholdingOptions(bool BackAndStart);

/// <summary>
/// Keeps Back + Start, the mouse mode's combo, away from the Steam Controller's virtual Xbox pad (#213), so a game never
/// sees it. Feed it every poll of the one controller it guards; it returns the buttons the game may see.
/// </summary>
/// <remarks>
/// View and Menu are held back when they go down, for as long as the other one may still join and make it the combo
/// (<see cref="MouseMode.ComboWindow"/>, the same rule the mouse mode counts by). Let go before that, the game gets a
/// tap of <see cref="TapLength"/>; held alone past it, the button goes through as held (owner, 2026-10-26), also View
/// while "Zurück zu Arcadia" fires. Both down in time: neither reaches the game until both are let go.
/// </remarks>
public sealed class ComboWithholding(TimeProvider time)
{
    /// <summary>Long enough for a game that reads its pad once a frame at 30 frames a second.</summary>
    public static readonly TimeSpan TapLength = TimeSpan.FromMilliseconds(50);

    private const PadButtons Combo = PadButtons.View | PadButtons.Menu;

    // Since when the first of View and Menu is down while it may still become the combo; null when nothing waits.
    private long? _firstDownAt;
    private PadButtons _waiting;
    private PadButtons _passing;
    private PadButtons _tapping;
    private long _tapUntil;

    // Set once the combo was seen: neither button reaches the game until both are let go.
    private bool _spent;

    public PadButtons Filter(PadButtons buttons, ComboWithholdingOptions options)
    {
        var now = time.GetTimestamp();
        if (_tapping != PadButtons.None && now >= _tapUntil)
        {
            _tapping = PadButtons.None;
        }

        if (!options.BackAndStart)
        {
            Reset();
            return buttons;
        }

        var down = buttons & Combo;
        var shown = (buttons & ~Combo) | _tapping;
        if (_spent)
        {
            _spent = down != PadButtons.None;
            return shown;
        }

        // A button already passing stays the game's until let go; the other one joining then is no combo either.
        _passing &= down;
        if (_passing != PadButtons.None)
        {
            _firstDownAt = null;
            _passing = down;
            return shown | down;
        }

        if (_firstDownAt is not { } since)
        {
            if (down != PadButtons.None && down != Combo)
            {
                _firstDownAt = now;
                _waiting = down;
            }
            else if (down == Combo)
            {
                _spent = true;
            }

            return shown;
        }

        if (down == Combo)
        {
            _firstDownAt = null;
            _spent = true;
            return shown;
        }

        if (down == PadButtons.None)
        {
            // Let go in time: the game gets the press it missed as a tap. The button is the one that was waiting.
            _firstDownAt = null;
            Tap(_waiting, now);
            return shown | _waiting;
        }

        _waiting = down;
        if (time.GetElapsedTime(since, now) > MouseMode.ComboWindow)
        {
            _firstDownAt = null;
            _passing = down;
            return shown | down;
        }

        return shown;
    }

    /// <summary>The controller went away or was replaced: nothing it held carries over.</summary>
    public void Reset()
    {
        _firstDownAt = null;
        _passing = PadButtons.None;
        _tapping = PadButtons.None;
        _waiting = PadButtons.None;
        _spent = false;
    }

    private void Tap(PadButtons button, long now)
    {
        _tapping |= button;
        _tapUntil = now + (long)(TapLength.TotalSeconds * time.TimestampFrequency);
    }
}
