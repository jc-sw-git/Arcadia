namespace Arcadia.Input;

/// <summary>
/// Decides which simulated mouse and keyboard input to drop: input another program sends while a
/// controller is in use. Outside games Steam turns the controller into mouse and keys itself (its
/// desktop layout, e.g. Back + Start into Alt+Tab); that must not act beside the mouse mode or
/// Arcadia's own navigation (#64). Real devices and Arcadia's own input always pass.
/// </summary>
/// <remarks>
/// A key or button is let go the way it went down: dropped downs drop their ups, so nothing sticks,
/// and an up whose down passed (pressed before the controller was touched) passes too.
/// Not thread-safe: one hook thread calls it.
/// </remarks>
public sealed class InjectedInputGate
{
    private readonly HashSet<int> _droppedDowns = [];

    private int _droppedCount;

    /// <summary>How many inputs were dropped since the last call; for the log.</summary>
    public int TakeDroppedCount()
    {
        var count = _droppedCount;
        _droppedCount = 0;
        return count;
    }

    /// <param name="key">Any id for the key or mouse button, the same for its down and up.</param>
    /// <param name="foreign">Simulated, and not by Arcadia.</param>
    /// <param name="controllerInUse">Asked only for foreign downs, so a costly check runs rarely.</param>
    /// <returns>True to drop it.</returns>
    public bool Press(int key, bool down, bool foreign, Func<bool> controllerInUse)
    {
        if (!down)
        {
            return Count(_droppedDowns.Remove(key));
        }

        if (!foreign)
        {
            return false;
        }

        if (_droppedDowns.Contains(key))
        {
            // A held key repeats its down.
            return Count(true);
        }

        if (!controllerInUse())
        {
            return false;
        }

        _droppedDowns.Add(key);
        return Count(true);
    }

    /// <summary>A pointer move or a wheel turn: nothing to let go later.</summary>
    public bool Motion(bool foreign, Func<bool> controllerInUse) => Count(foreign && controllerInUse());

    private bool Count(bool dropped)
    {
        if (dropped)
        {
            _droppedCount++;
        }

        return dropped;
    }
}
