using Arcadia.Core.Search;
using Arcadia.Input;

namespace Arcadia.App.Keyboard;

/// <summary>
/// The shift (LB) and AltGr (RB) levels of the on-screen keyboard (<see cref="OnScreenKeyboardViewModel"/>):
/// each is on while held, or switched on and off by a tap, and only one is on at a time. AltGr stays off
/// for a layout without that level.
/// </summary>
/// <remarks>Call it from the UI thread, like <see cref="LatchGesture"/>.</remarks>
public sealed class KeyboardLevels
{
    /// <summary>Held at least this long without typing, shift or AltGr go off on release instead of staying on.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(500);

    private readonly LatchGesture _shift;
    private readonly LatchGesture _altGr;
    private readonly Func<bool> _hasAltGr;

    /// <param name="hasAltGr">Whether the shown layout has an AltGr level; read on every press.</param>
    public KeyboardLevels(Func<bool> hasAltGr, TimeProvider time)
    {
        _hasAltGr = hasAltGr;
        _shift = new LatchGesture(HoldTime, time);
        _shift.Changed += _ => Changed?.Invoke();
        _altGr = new LatchGesture(HoldTime, time);
        _altGr.Changed += _ => Changed?.Invoke();
    }

    /// <summary>Raised whenever <see cref="Level"/> may have changed.</summary>
    public event Action? Changed;

    /// <summary>AltGr wins should both be on for a moment.</summary>
    public KeyLevel Level => _altGr.IsOn ? KeyLevel.AltGr : _shift.IsOn ? KeyLevel.Shift : KeyLevel.Normal;

    /// <summary>A key was typed while LB or RB is held: they go off when let go, instead of switching.</summary>
    public void Use()
    {
        _shift.Use();
        _altGr.Use();
    }

    /// <summary>LB down: shift is on at once, so a key pressed while holding it types its shift character.</summary>
    public void PressShift()
    {
        _altGr.Reset();
        _shift.Press();
    }

    /// <summary>LB up: off after a hold, switched after a tap.</summary>
    public void ReleaseShift() => _shift.Release();

    /// <summary>The shift key pressed with A or clicked.</summary>
    public void ToggleShift()
    {
        _altGr.Reset();
        _shift.Toggle();
    }

    /// <summary>RB down: AltGr is on at once, so a key pressed while holding it types its AltGr character.</summary>
    public void PressAltGr()
    {
        if (_hasAltGr())
        {
            _shift.Reset();
            _altGr.Press();
        }
    }

    /// <summary>RB up: off after a hold, switched after a tap.</summary>
    public void ReleaseAltGr() => _altGr.Release();

    /// <summary>The AltGr key pressed with A or clicked.</summary>
    public void ToggleAltGr()
    {
        if (_hasAltGr())
        {
            _shift.Reset();
            _altGr.Toggle();
        }
    }

    /// <summary>LB's and RB's releases may never arrive, e.g. when the window loses focus.</summary>
    public void CancelPresses()
    {
        _shift.Cancel();
        _altGr.Cancel();
    }

    /// <summary>The layout changed to one without AltGr.</summary>
    public void ResetAltGr() => _altGr.Reset();

    /// <summary>Both off, e.g. when the keyboard closes.</summary>
    public void Reset()
    {
        _shift.Reset();
        _altGr.Reset();
    }
}
