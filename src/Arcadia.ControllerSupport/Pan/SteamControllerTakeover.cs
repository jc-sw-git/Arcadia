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

    /// <summary>The player is asked ("Ja, übernehmen. Es läuft gerade kein Firmware-Update!" / "Nein, nicht übernehmen").</summary>
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
/// Except a short gap mid-game (owner, 2026-10-28): while a game of the library stays in front, Steam's update window
/// can't be, so a gap of less than <see cref="MidGameGapLimit"/> with the same game in front at every check, from
/// just before the gap until the hide, is taken over again without asking. A missing check (Arcadia hung) asks.
/// </summary>
public sealed class SteamControllerTakeover(Action<string> log, TimeProvider? time = null)
{
    /// <summary>A gap mid-game at most this long is taken over again without asking.</summary>
    public static readonly TimeSpan MidGameGapLimit = TimeSpan.FromSeconds(10);

    /// <summary>The checks of what is in front come every 2 s; one further apart missed what was in front meanwhile.</summary>
    public static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private volatile TakeoverState _state = TakeoverState.Unknown;

    // The game in front at every check since Since, the last one At; null without a game.
    private (string Game, long Since, long At)? _lastInFront;

    // A gap that began with this game in front, and when; null once a check saw anything else, or none came in time.
    private (string Game, long Since)? _gapInGame;

    public TakeoverState State => _state;

    /// <summary>What was connected when the player was asked.</summary>
    public SteamControllerParts Asked { get; private set; }

    /// <summary>The player must be asked now (raised once per question); any thread.</summary>
    public event Action<SteamControllerParts>? ConsentNeeded;

    /// <summary><see cref="State"/> changed; any thread.</summary>
    public event Action? Changed;

    // Granted because Steam wasn't running, not by the player: checked again right before the hide. Once hidden, Steam
    // sees only the copy, and every gap asks anew (Gap).
    private bool _becauseSteamWasNotRunning;

    /// <summary>
    /// Whether a hide may start now: granted, or Steam doesn't run (no one can update the puck then). Starts the question
    /// when the state is unknown and Steam runs.
    /// </summary>
    public bool MayHide(bool steamRuns, SteamControllerParts connected)
    {
        if (_state == TakeoverState.Granted && _becauseSteamWasNotRunning && steamRuns)
        {
            // Granted only because Steam wasn't running, and Steam came up before the hide held: Steam may update the
            // real puck now, so the player decides (review of 68a163f, Codex point 1).
            Set(TakeoverState.Unknown, "Steam läuft jetzt, bevor Arcadia übernommen hat");
        }

        switch (_state)
        {
            case TakeoverState.Granted:
                return true;
            case TakeoverState.Unknown when steamRuns && GameStayedInFront() is { } game:
                Set(TakeoverState.Granted, $"kurze Lücke, während {game} vorne blieb: Steam konnte kein Firmware-Update anzeigen");
                return true;
            case TakeoverState.Unknown when !steamRuns:
                Set(TakeoverState.Granted, "Steam läuft nicht, Arcadia übernimmt den Steam Controller");
                _becauseSteamWasNotRunning = true;
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

    /// <summary>
    /// A firmware attempt keeps the real puck hidden (Pan's hold, also from before a crash of Arcadia): Steam never saw it
    /// since, so it is taken over without asking, and showing it is what must not happen now.
    /// </summary>
    public void KeepForFirmware()
    {
        if (_state is TakeoverState.Unknown or TakeoverState.Asking)
        {
            Set(TakeoverState.Granted, "ein Firmware-Versuch hält den Steam Controller versteckt");
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
        var wasGranted = _state == TakeoverState.Granted;
        if (wasGranted
            || (switchedOff && _state is TakeoverState.Asking or TakeoverState.Declined or TakeoverState.Released))
        {
            Set(TakeoverState.Unknown, $"Lücke ({why}): Arcadia fragt beim nächsten Verstecken wieder");
        }

        // The emulation was fine at the check before (every tick checks it), so the gap began after it: the game must have
        // been in front then already. After a hang that check is too long ago, and the run of checks starts anew.
        if (wasGranted && !switchedOff && _lastInFront is { } last && last.Since < last.At
            && _time.GetElapsedTime(last.At) <= CheckLimit)
        {
            _gapInGame = (last.Game, _time.GetTimestamp());
        }
    }

    /// <summary>
    /// Every tick, before anything else: the library game in front, or null. Ends a mid-game gap's exception when
    /// something else is in front or the last check is too long ago.
    /// </summary>
    public void InFront(string? game)
    {
        var now = _time.GetTimestamp();
        if (_gapInGame is { } gap
            && (game != gap.Game || _lastInFront is not { } last || _time.GetElapsedTime(last.At, now) > CheckLimit))
        {
            _gapInGame = null;
            log($"Pan-Übernahme: während der Lücke war {(game is null ? "kein Spiel" : game == gap.Game ? "eine Prüfung zu spät" : "ein anderes Spiel")} vorne, Arcadia fragt");
        }

        _lastInFront = game is null ? null
            : _lastInFront is { } run && run.Game == game && _time.GetElapsedTime(run.At, now) <= CheckLimit ? (game, run.Since, now)
            : (game, now, now);
    }

    private string? GameStayedInFront()
    {
        if (_gapInGame is not { } gap)
        {
            return null;
        }

        _gapInGame = null;
        return _time.GetElapsedTime(gap.Since) < MidGameGapLimit
            && _lastInFront is { } last && last.Game == gap.Game && _time.GetElapsedTime(last.At) <= CheckLimit
            ? gap.Game
            : null;
    }

    private void Set(TakeoverState state, string why)
    {
        _becauseSteamWasNotRunning = false;
        _gapInGame = null;
        if (_state == state)
        {
            return;
        }

        _state = state;
        log($"Pan-Übernahme: {state} – {why}");
        Changed?.Invoke();
    }
}
