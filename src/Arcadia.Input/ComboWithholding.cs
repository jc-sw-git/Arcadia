namespace Arcadia.Input;

/// <summary>Which of Arcadia's own combos are switched on, so a game must not see them (#213).</summary>
/// <param name="BackAndStart">The mouse mode is not "Aus": Back + Start is Arcadia's.</param>
/// <param name="ReturnHold">"Zurück zu Arcadia"'s hold time, null when it is off: View held that long is Arcadia's.</param>
public sealed record ComboWithholdingOptions(bool BackAndStart, TimeSpan? ReturnHold);

/// <summary>
/// Keeps Arcadia's combos (Back + Start, View held) away from the Steam Controller's virtual Xbox pad (#213), so a game
/// never sees them. Feed it every poll of the one controller it guards; it returns the buttons the game may see.
/// </summary>
/// <remarks>
/// A guarded button (View, Menu) is held back as long as it is down, because until it is let go the other one may join
/// and make it Arcadia's combo (<see cref="MouseMode"/> counts Back + Start whenever both are down, however late the
/// second came). Let go alone, the game gets it as a tap of <see cref="TapLength"/>. View held for "Zurück zu Arcadia",
/// and both down together, never reach the game; after a combo nothing guarded does until both are let go.
/// </remarks>
public sealed class ComboWithholding(TimeProvider time)
{
    /// <summary>Long enough for a game that reads its pad once a frame at 30 frames a second.</summary>
    public static readonly TimeSpan TapLength = TimeSpan.FromMilliseconds(50);

    private const PadButtons Guardable = PadButtons.View | PadButtons.Menu;

    private readonly Dictionary<PadButtons, long> _pendingSince = [];
    private PadButtons _tapping;
    private long _tapUntil;

    // Set once a combo was seen: nothing guarded reaches the game until every guarded button is let go.
    private bool _spent;

    public PadButtons Filter(PadButtons buttons, ComboWithholdingOptions options)
    {
        var now = time.GetTimestamp();
        var guarded = (options.BackAndStart ? Guardable : PadButtons.None) | (options.ReturnHold is null ? PadButtons.None : PadButtons.View);
        if (_tapping != PadButtons.None && now >= _tapUntil)
        {
            _tapping = PadButtons.None;
        }

        var down = buttons & guarded;
        var shown = (buttons & ~guarded) | _tapping;
        if (_spent)
        {
            _spent = down != PadButtons.None;
            return shown;
        }

        if (options.BackAndStart && down == Guardable)
        {
            _spent = true;
            _pendingSince.Clear();
            return shown;
        }

        foreach (var button in new[] { PadButtons.View, PadButtons.Menu })
        {
            if ((guarded & button) == 0)
            {
                _pendingSince.Remove(button);
            }
            else if ((down & button) == 0)
            {
                if (_pendingSince.Remove(button))
                {
                    Tap(button, now);
                    shown |= button;
                }
            }
            else if (!_pendingSince.TryGetValue(button, out var since))
            {
                _pendingSince[button] = now;
            }
            else if (button == PadButtons.View && options.ReturnHold is { } hold && time.GetElapsedTime(since, now) >= hold)
            {
                // "Zurück zu Arcadia" fires now: the game never gets this View.
                _pendingSince.Clear();
                _spent = true;
            }
        }

        return shown;
    }

    /// <summary>The controller went away or was replaced: nothing it held carries over.</summary>
    public void Reset()
    {
        _pendingSince.Clear();
        _tapping = PadButtons.None;
        _spent = false;
    }

    private void Tap(PadButtons button, long now)
    {
        _tapping |= button;
        _tapUntil = now + (long)(TapLength.TotalSeconds * time.TimestampFrequency);
    }
}
