using Arcadia.Core.Launchers;
using Arcadia.Input;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.ControllerSupport;

/// <summary>Where the Steam Controller goes in games right now (#213).</summary>
/// <param name="PadWanted">Arcadia's Xbox pad is plugged in (while a Steam Controller is connected).</param>
/// <param name="SteamUsesController">
/// Steam may act on the Steam Controller: a game on "Steam-Eingabe" runs, or Arcadia can't serve the game itself (no
/// driver). Otherwise Arcadia keeps Steam from it: the copy Steam sees reports rest (<see cref="SteamControllerCopyRules"/>,
/// #242), so Steam's desktop layout and Steam's own pad send nothing beside Arcadia's pad.
/// </param>
/// <param name="Why">For the log.</param>
public sealed record SteamControllerRoute(bool PadWanted, bool SteamUsesController, string Why)
{
    /// <summary>Some game runs (or is being started), on either mode.</summary>
    public bool GameRunning { get; init; }
}

/// <summary>Who has the Steam Controller's attention right now, read on every poll.</summary>
/// <param name="ArcadiaActive">Arcadia's window is in front.</param>
/// <param name="MouseModeOn">The controller is Arcadia's mouse.</param>
/// <param name="SteamInFront">A window of Steam itself is in front (Big Picture, the client), not a game.</param>
/// <param name="GameOnArcadiaInFront">A library game whose Steam Controller mode is "Arcadia" owns the foreground window.</param>
public readonly record struct ForegroundNow(bool ArcadiaActive, bool MouseModeOn, bool SteamInFront, bool GameOnArcadiaInFront = false);

/// <summary>
/// When the copy Steam sees passes input and when Arcadia's Xbox pad does (#242, decisions 2 and 4; #315; #357; product rules in
/// <c>docs/product/controls.md</c>). The copy passes to a game on "Steam-Eingabe", and to Steam itself in front while no
/// game runs or only games on "Arcadia" run, when the pad rests so Steam gets the full Steam Controller. While "Steams Desktop-Belegung ignorieren" is
/// off, Steam's desktop layout gets the Steam Controller like any other controller: the copy passes also while Arcadia is
/// in front, the mouse mode is on or no game runs; only a game on "Arcadia" keeps it for the pad. While the setting is on,
/// neither passes while Arcadia is in front or the mouse mode is on. Steam's overlay inside a game can't be told from the
/// game and counts as the game. The two never pass at once.
/// </summary>
public static class SteamControllerCopyRules
{
    public static bool CopyPasses(SteamControllerRoute route, ForegroundNow now, bool desktopLayoutIgnored) =>
        Copy(route, now, desktopLayoutIgnored).Passes;

    /// <summary>Whether the copy passes, and why, for the log.</summary>
    public static (bool Passes, string Why) Copy(SteamControllerRoute route, ForegroundNow now, bool desktopLayoutIgnored)
    {
        if (now.ArcadiaActive || now.MouseModeOn)
        {
            var who = now.MouseModeOn ? "Mausmodus an" : "Arcadia vorne";
            return desktopLayoutIgnored ? (false, $"{who}, Steams Desktop-Belegung ignoriert") : (true, $"{who}, Steams Desktop-Belegung aktiv");
        }

        if (route.SteamUsesController)
        {
            return (true, route.Why);
        }

        if (route.GameRunning)
        {
            // Only games on "Arcadia" run here; Steam in front gets the controller, the pad rests there (#357).
            return now.SteamInFront ? (true, "Steam vorne, Spiele auf Arcadia") : (false, route.Why);
        }

        if (now.SteamInFront)
        {
            return (true, "Steam vorne, kein Spiel");
        }

        return desktopLayoutIgnored ? (false, "kein Spiel, Steams Desktop-Belegung ignoriert") : (true, "kein Spiel, Steams Desktop-Belegung aktiv");
    }

    /// <summary>
    /// Which of Arcadia's combos the copy keeps from Steam: ⧉ + ≡ while the mouse mode can be switched and Steam's desktop
    /// layout is ignored; otherwise Steam gets it as from any other controller (action set switch, Alt+Tab; #315).
    /// </summary>
    public static ComboWithholdingOptions CombosKeptFromSteam(bool mouseModeCanSwitch, bool desktopLayoutIgnored) =>
        new(BackAndStart: mouseModeCanSwitch && desktopLayoutIgnored);

    /// <summary>
    /// Whether the pad of a controller other than the Steam Controller passes (#238): Steam sees these controllers only
    /// through Arcadia's pads, so with Steam's desktop control allowed they pass in front of Arcadia and in the mouse mode
    /// too, as the copy does (#315); otherwise they rest there.
    /// </summary>
    public static bool OtherControllerPasses(ForegroundNow now, bool desktopLayoutIgnored) =>
        !(now.ArcadiaActive || now.MouseModeOn) || !desktopLayoutIgnored;

    /// <summary>
    /// The pad of another controller goes only to Steam right now (in front of Arcadia, in the mouse mode): Steam gets the
    /// combos the copy would (<see cref="CombosKeptFromSteam"/>), otherwise a game's are held back.
    /// </summary>
    public static bool OtherControllerOnlyForSteam(ForegroundNow now) => now.ArcadiaActive || now.MouseModeOn;

    /// <summary>
    /// Arcadia's Xbox pad passes input only while a game on "Arcadia" is in front (#357): that game wanting the input is
    /// the only reason to send it. Everywhere else (Steam, a browser, the desktop, no window, Arcadia, the mouse mode, no
    /// game running) it rests, so Steam never reads the Steam Controller through the pad. A game in front while the route
    /// still says none runs (it is decided every 2 s) rests too: the copy may pass then, and the two never pass at once.
    /// </summary>
    public static bool PadPasses(SteamControllerRoute route, ForegroundNow now) =>
        PadMayPass(route) && now.GameOnArcadiaInFront && !now.SteamInFront && !now.ArcadiaActive && !now.MouseModeOn;

    /// <summary>A game that can get the pad runs: only then is the foreground worth reading for <see cref="PadPasses"/>.</summary>
    public static bool PadMayPass(SteamControllerRoute route) => route.PadWanted && route.GameRunning;
}

/// <summary>
/// Asks <see cref="SteamControllerCopyRules"/> on every report of the copy and logs each change of its answer (#315), so
/// the log tells who had the Steam Controller when. Never throws: an error lets nothing pass.
/// </summary>
public sealed class SteamControllerCopyGate(Func<SteamControllerRoute?> route, Func<ForegroundNow> now, Func<bool> desktopLayoutIgnored, Action<string> log)
{
    // 0 not asked yet, 1 rests, 2 passes; the copy's reader thread asks, so one thread at a time.
    private int _last;

    public bool Passes()
    {
        bool passes;
        string why;
        try
        {
            if (route() is not { } current)
            {
                (passes, why) = (false, "noch nicht entschieden");
            }
            else
            {
                (passes, why) = SteamControllerCopyRules.Copy(current, now(), desktopLayoutIgnored());
            }
        }
        catch (Exception ex)
        {
            (passes, why) = (false, $"Fehler: {ex}");
        }

        var state = passes ? 2 : 1;
        if (Interlocked.Exchange(ref _last, state) != state)
        {
            try
            {
                log($"Kopie für Steam: {(passes ? "gibt Eingaben weiter" : "meldet Ruhe")} ({why})");
            }
            catch (Exception)
            {
                // The log must not stop the copy.
            }
        }

        return passes;
    }
}

/// <summary>
/// Decides from the running games and their Steam Controller mode whether Arcadia's Xbox pad is plugged in and whether
/// Steam may use the Steam Controller (#213): games started from Arcadia, in their launcher, and Steam games started
/// directly in Steam, also when they aren't in the library (#119, Steam's <c>RunningAppID</c>).
/// </summary>
/// <param name="runningSteamGame">The <c>Game.Key</c> of the game Steam runs right now, also one started directly in Steam, or null.</param>
/// <remarks>Read by the pad's tick and asked again by <see cref="Update"/> every 2 s and right before a game starts.</remarks>
public sealed class SteamControllerRouting(
    Func<IReadOnlyCollection<string>> activeGames,
    Func<string, bool> usesArcadiaPad,
    Func<string?> runningSteamGame,
    Func<bool> driverReady,
    Action<string> log)
{
    private readonly Lock _lock = new();
    private volatile SteamControllerRoute _current = new(PadWanted: false, SteamUsesController: true, "noch nicht entschieden");
    private bool _decided;
    private volatile bool _driverReady;

    public SteamControllerRoute Current => _current;

    /// <summary>usbip and VIIPER are there, so the mode "Arcadia" can be chosen; as of the last <see cref="Update"/>.</summary>
    public bool DriverReady => _driverReady;

    /// <summary>Raised when <see cref="DriverReady"/> changed; on the thread that called <see cref="Update"/>.</summary>
    public event Action? DriverChanged;

    /// <summary>
    /// The rule (#213, owner 2026-10-25/26): any running game on "Steam-Eingabe" hands the controller to Steam and
    /// unplugs the pad, so that game has its own Steam configuration and no extra controller. Without the driver no game
    /// gets Arcadia's pad, so Steam keeps the controller during every game. Otherwise the pad is in and Steam is kept from
    /// the controller, also while no game runs (Arcadia reads it itself).
    /// </summary>
    public static SteamControllerRoute Decide(IEnumerable<string> games, Func<string, bool> usesArcadiaPad, bool driverReady)
    {
        var running = games.ToList();
        if (running.FirstOrDefault(key => !usesArcadiaPad(key)) is { } steamInputGame)
        {
            return new(PadWanted: false, SteamUsesController: true, $"{steamInputGame} auf Steam-Eingabe") { GameRunning = true };
        }

        if (!driverReady)
        {
            return running.Count > 0
                ? new(PadWanted: false, SteamUsesController: true, "ohne Treiber, ein Spiel läuft") { GameRunning = true }
                : new(PadWanted: false, SteamUsesController: false, "ohne Treiber, kein Spiel");
        }

        return new(PadWanted: true, SteamUsesController: false, running.Count > 0 ? "Spiele auf Arcadia" : "kein Spiel") { GameRunning = running.Count > 0 };
    }

    /// <summary>Decides anew and applies a change; never throws (an error keeps the last route).</summary>
    public SteamControllerRoute Update()
    {
        bool driverChanged;
        lock (_lock)
        {
            try
            {
                var ready = driverReady();
                var route = Decide(GamesNow(), usesArcadiaPad, ready);
                driverChanged = _decided && ready != _driverReady;
                _driverReady = ready;
                var before = _current;
                _current = route;
                if (!_decided || route.PadWanted != before.PadWanted || route.SteamUsesController != before.SteamUsesController)
                {
                    log($"Steam Controller in Spielen: {(route.PadWanted ? "Xbox-Pad von Arcadia" : "kein Xbox-Pad")}, Steam {(route.SteamUsesController ? "darf ihn verwenden" : "bekommt nichts")} ({route.Why})");
                }

                _decided = true;
            }
            catch (Exception ex)
            {
                log($"Steam Controller in Spielen: Entscheidung fehlgeschlagen: {ex}");
                return _current;
            }
        }

        if (driverChanged)
        {
            DriverChanged?.Invoke();
        }

        return _current;
    }

    private IEnumerable<string> GamesNow()
    {
        var games = activeGames().ToHashSet(StringComparer.Ordinal);
        if (runningSteamGame() is { } key)
        {
            games.Add(key);
        }

        return games;
    }
}
