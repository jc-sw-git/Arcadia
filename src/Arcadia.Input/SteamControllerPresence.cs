namespace Arcadia.Input;

/// <summary>
/// Whether a Steam Controller is connected, as SDL sees it (#212), not only while SISR emulates it: the per-game mode
/// in the preview and its button hint follow it. Fed with every poll on the poll thread.
/// </summary>
public sealed class SteamControllerPresence
{
    public bool IsConnected { get; private set; }

    /// <returns>True when <see cref="IsConnected"/> changed with this poll.</returns>
    public bool Note(IReadOnlyList<GamepadReading> pads)
    {
        var connected = false;
        for (var i = 0; i < pads.Count && !connected; i++)
        {
            connected = pads[i].IsSteamController;
        }

        if (connected == IsConnected)
        {
            return false;
        }

        IsConnected = connected;
        return true;
    }

    /// <summary>The controllers are gone for good (<see cref="IGamepadSource.Lost"/>).</summary>
    /// <returns>True when a Steam Controller counted as connected until now.</returns>
    public bool Drop()
    {
        var was = IsConnected;
        IsConnected = false;
        return was;
    }
}
