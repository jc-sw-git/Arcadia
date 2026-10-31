namespace Arcadia.Input;

/// <summary>
/// The controller whose button symbols the hints show (#215, owner 2026-10-28): the one with the last button press or
/// stick push past the dead zone; stick noise switches nothing. Before any input, the first connected one. A controller
/// plugged in later changes nothing until it is used; when the one in use goes, the first connected of the others takes
/// over, and with none left there is none (the hints show the keyboard). Fed with every poll on the poll thread.
/// </summary>
public sealed class ControllerInUse
{
    /// <summary>
    /// SDL may report a controller a few polls after its start; one that comes this soon still counts as connected at
    /// the start, so the hints don't stay on the keyboard although a controller is there.
    /// </summary>
    public static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(2);

    private readonly TimeProvider _time;
    private readonly List<PadState> _pads = [];
    private long? _firstPoll;
    private int? _current;
    private bool _decided;

    public ControllerInUse(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>SDL's type of the controller in use; null when there is none.</summary>
    public PadType? Current => _current is { } id ? _pads[IndexOf(id)].Type : null;

    /// <returns>True when <see cref="Current"/> changed with this poll, and on the first poll, which decides it at all.</returns>
    public bool Note(IReadOnlyList<GamepadReading> pads)
    {
        var before = Current;
        var now = _time.GetTimestamp();
        _firstPoll ??= now;
        var currentLeft = _current is { } current && !Contains(pads, current);
        for (var i = _pads.Count - 1; i >= 0; i--)
        {
            if (!Contains(pads, _pads[i].Id))
            {
                _pads.RemoveAt(i);
            }
        }

        if (currentLeft)
        {
            _current = null;
        }

        for (var i = 0; i < pads.Count; i++)
        {
            var pad = pads[i];
            var leftPushed = ControllerActivity.Pushed(pad.LeftStick);
            var rightPushed = ControllerActivity.Pushed(pad.RightStick);
            var index = IndexOf(pad.PadId);
            if (index < 0)
            {
                // A controller's first reading is where it starts from, not a press.
                _pads.Add(new PadState(pad.PadId, pad.Type, pad.Buttons, leftPushed, rightPushed));
                continue;
            }

            var last = _pads[index];
            if ((pad.Buttons & ~last.Buttons) != 0 || (leftPushed && !last.LeftPushed) || (rightPushed && !last.RightPushed))
            {
                _current = pad.PadId;
            }

            _pads[index] = last with { Buttons = pad.Buttons, LeftPushed = leftPushed, RightPushed = rightPushed };
        }

        var atStart = !_decided || _time.GetElapsedTime(_firstPoll.Value, now) < StartGrace;
        if (_current is null && (atStart || currentLeft) && _pads.Count > 0)
        {
            _current = FirstConnected();
        }

        var changed = !_decided || Current != before;
        _decided = true;
        return changed;
    }

    /// <summary>The controllers are gone for good (<see cref="IGamepadSource.Lost"/>), or there are none on this system.</summary>
    /// <returns>True when that changed what counted until now.</returns>
    public bool Drop()
    {
        var changed = !_decided || _current is not null;
        _pads.Clear();
        _current = null;
        _decided = true;
        return changed;
    }

    // SDL gives every connect a higher id, so the lowest is the one connected first.
    private int FirstConnected()
    {
        var first = _pads[0].Id;
        foreach (var pad in _pads)
        {
            first = Math.Min(first, pad.Id);
        }

        return first;
    }

    private int IndexOf(int id)
    {
        for (var i = 0; i < _pads.Count; i++)
        {
            if (_pads[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Contains(IReadOnlyList<GamepadReading> pads, int id)
    {
        for (var i = 0; i < pads.Count; i++)
        {
            if (pads[i].PadId == id)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct PadState(int Id, PadType Type, PadButtons Buttons, bool LeftPushed, bool RightPushed);
}
