namespace Arcadia.ControllerSupport.Pan;

/// <summary>What of the Steam Controller is connected, for the question whether Arcadia may take it over (#242).</summary>
[Flags]
public enum SteamControllerParts
{
    None = 0,

    /// <summary>The Steam Controller itself (SDL sees it, through the puck or by cable).</summary>
    Controller = 1,

    /// <summary>The wireless puck, which can get a firmware update of its own.</summary>
    Puck = 2,
}

/// <summary>Whether Arcadia may hide the real Steam Controller and puck now (owner's design, 2026-10-28, PR #286).</summary>
public enum TakeoverState
{
    /// <summary>Not known whether a firmware update runs: the real puck was visible while Steam ran. Nothing is hidden.</summary>
    Unknown,

    /// <summary>The player is asked ("Ja, es läuft gerade kein Firmware-Update" / "Nein, nicht übernehmen").</summary>
    Asking,

    /// <summary>Steam didn't run, or the player said yes: hidden while the emulation runs without a gap.</summary>
    Granted,

    /// <summary>The player said no: nothing is hidden until the settings row takes them over, or Arcadia's next start.</summary>
    Declined,

    /// <summary>Released for a firmware update Steam tried through the copy: as declined, until the settings row.</summary>
    Released,
}

/// <summary>
/// The owner's rule for the firmware: a hide restarts the puck and would break off an update Steam runs on it, and from
/// outside there is no telling whether one runs. So an unknown state never allows a hide. Arcadia takes the controller
/// over without asking only when Steam doesn't run; otherwise the player confirms it (held for 3 s). The consent holds
/// while the emulation runs without a gap, as Steam never sees the real puck then (also when one is plugged in meanwhile);
/// every gap (paused, Pan's teardown, switched off) asks again. A "no" holds until Arcadia's next start.
/// </summary>
public sealed class SteamControllerTakeover(Action<string> log)
{
    private volatile TakeoverState _state = TakeoverState.Unknown;

    public TakeoverState State => _state;

    /// <summary>What was connected when the player was asked.</summary>
    public SteamControllerParts Asked { get; private set; }

    /// <summary>The player must be asked now (raised once per question); any thread.</summary>
    public event Action<SteamControllerParts>? ConsentNeeded;

    /// <summary><see cref="State"/> changed; any thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Whether a hide may start now: granted, or Steam doesn't run (no one can update the puck then). Starts the question
    /// when the state is unknown and Steam runs.
    /// </summary>
    public bool MayHide(bool steamRuns, SteamControllerParts connected)
    {
        switch (_state)
        {
            case TakeoverState.Granted:
                return true;
            case TakeoverState.Unknown when !steamRuns:
                Set(TakeoverState.Granted, "Steam läuft nicht, Arcadia übernimmt den Steam Controller");
                return true;
            case TakeoverState.Unknown:
                Asked = connected;
                Set(TakeoverState.Asking, $"Steam läuft, Arcadia fragt, ob es übernehmen darf ({connected})");
                ConsentNeeded?.Invoke(connected);
                return false;
            default:
                return false;
        }
    }

    /// <summary>The player's answer in the dialog.</summary>
    public void Answer(bool takeOver)
    {
        if (_state != TakeoverState.Asking)
        {
            return;
        }

        Set(takeOver ? TakeoverState.Granted : TakeoverState.Declined,
            takeOver ? "vom Spieler erlaubt: es läuft kein Firmware-Update" : "vom Spieler abgelehnt, bis zum nächsten Start oder bis zur Freigabe in den Einstellungen");
    }

    /// <summary>The settings row "Steam Controller und Puck übernehmen" (held 3 s): the press is the consent.</summary>
    public void TakeOverAgain()
    {
        if (_state is TakeoverState.Declined or TakeoverState.Released)
        {
            Set(TakeoverState.Granted, "in den Einstellungen wieder erlaubt");
        }
    }

    /// <summary>Released for a firmware update Steam tried: hidden no more until the settings row.</summary>
    public void Release() => Set(TakeoverState.Released, "für ein Firmware-Update freigegeben");

    /// <summary>
    /// The real puck was or will be visible (the emulation ended, paused, was torn down, a hide didn't hold): a consent
    /// given before doesn't hold any more. A "no" and an open question stay; switching off (<paramref name="switchedOff"/>)
    /// forgets them too, as switching on asks again.
    /// </summary>
    public void Gap(string why, bool switchedOff = false)
    {
        if (_state == TakeoverState.Granted
            || (switchedOff && _state is TakeoverState.Asking or TakeoverState.Declined or TakeoverState.Released))
        {
            Set(TakeoverState.Unknown, $"Lücke ({why}): Arcadia fragt beim nächsten Verstecken wieder");
        }
    }

    private void Set(TakeoverState state, string why)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        log($"Pan-Übernahme: {state} – {why}");
        Changed?.Invoke();
    }
}
