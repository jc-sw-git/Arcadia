namespace Arcadia.Input;

/// <summary>
/// Switches the mouse mode by itself, as chosen in the settings: off when Arcadia comes to the front
/// ("Automatisch", or "Von Hand" with "Beenden, wenn Arcadia nach vorne kommt"), on when Arcadia
/// loses the focus to something that isn't a game ("Automatisch"). A mouse mode switched on that way
/// ends again as soon as a game comes to the front; one switched by hand stays until Arcadia comes
/// to the front, so Back + Start can override the automatic mode, e.g. in a game Arcadia doesn't know.
/// </summary>
/// <remarks>Call it from the UI thread. A game is a process from a library game's install folder.</remarks>
public sealed class MouseModeAutomation(MouseMode mode, Func<bool> endWhenArcadiaInFront, Func<bool> startWhenArcadiaLosesFocus)
{
    /// <summary>
    /// Whether the foreground needs watching for a game coming to the front. False while the mode
    /// is off or was switched on by hand: a hand-made choice stays.
    /// </summary>
    public bool WatchesForeground => mode.IsOn && mode.IsAutomatic;

    /// <summary>Only the change to the front counts: a mouse mode switched on by hand inside Arcadia stays on.</summary>
    public void ArcadiaActivated()
    {
        if (mode.IsOn && endWhenArcadiaInFront())
        {
            mode.Request(false);
        }
    }

    public void ArcadiaDeactivated(bool gameInFront)
    {
        if (!mode.IsOn && !gameInFront && startWhenArcadiaLosesFocus())
        {
            mode.Request(true);
        }
    }

    /// <summary>The foreground checked while <see cref="WatchesForeground"/>, e.g. after the Epic launcher showed briefly before its game.</summary>
    public void ForegroundChecked(bool gameInFront)
    {
        if (gameInFront && WatchesForeground)
        {
            mode.Request(false);
        }
    }
}
