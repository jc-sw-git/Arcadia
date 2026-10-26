using System.Globalization;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.ControllerSupport;

/// <summary>Where the Steam Controller goes in games right now (#213).</summary>
/// <param name="PadWanted">Arcadia's Xbox pad is plugged in (while a Steam Controller is connected).</param>
/// <param name="SteamUsesController">
/// Steam may act on the Steam Controller: a game on "Steam-Eingabe" runs, or Arcadia can't serve the game itself (no
/// driver). Otherwise Arcadia keeps Steam from it (<see cref="ISteamControllerHold"/>), so Steam's desktop layout and
/// Steam's own pad send nothing beside Arcadia's pad.
/// </param>
/// <param name="Why">For the log.</param>
public sealed record SteamControllerRoute(bool PadWanted, bool SteamUsesController, string Why);

/// <summary>
/// Keeps Steam from acting on the Steam Controller while Arcadia serves it, and lets Steam have it again. #242 does it
/// with an emulated copy of the controller that Steam sees instead of the real one (spike #239): false = the copy reports
/// only rest, true = it forwards again. This is all <see cref="SteamControllerRouting"/> knows of it.
/// </summary>
public interface ISteamControllerHold
{
    /// <summary>Called on every change of <see cref="SteamControllerRoute.SteamUsesController"/>, off the UI thread. Never throws.</summary>
    void Apply(bool steamUsesController);
}

/// <summary>
/// Decides from the running games and their Steam Controller mode whether Arcadia's Xbox pad is plugged in and whether
/// Steam may use the Steam Controller (#213): games started from Arcadia, in their launcher, and Steam games started
/// directly in Steam, also when they aren't in the library (#119, Steam's <c>RunningAppID</c>).
/// </summary>
/// <remarks>Read by the pad's tick and asked again by <see cref="Update"/> every 2 s and right before a game starts.</remarks>
public sealed class SteamControllerRouting(
    Func<IReadOnlyCollection<string>> activeGames,
    Func<string, bool> usesArcadiaPad,
    Func<int?> runningSteamAppId,
    Func<bool> driverReady,
    Action<string> log)
{
    private readonly Lock _lock = new();
    private volatile SteamControllerRoute _current = new(PadWanted: false, SteamUsesController: true, "noch nicht entschieden");
    private bool _decided;
    private volatile bool _driverReady;

    /// <summary>What keeps Steam from the controller; until #242, nothing does.</summary>
    public ISteamControllerHold Hold { get; init; } = new NoHold();

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
            return new(PadWanted: false, SteamUsesController: true, $"{steamInputGame} auf Steam-Eingabe");
        }

        if (!driverReady)
        {
            return running.Count > 0
                ? new(PadWanted: false, SteamUsesController: true, "ohne Treiber, ein Spiel läuft")
                : new(PadWanted: false, SteamUsesController: false, "ohne Treiber, kein Spiel");
        }

        return new(PadWanted: true, SteamUsesController: false, running.Count > 0 ? "Spiele auf Arcadia" : "kein Spiel");
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

                if (!_decided || route.SteamUsesController != before.SteamUsesController)
                {
                    Hold.Apply(route.SteamUsesController);
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
        if (runningSteamAppId() is { } appId)
        {
            games.Add(Game.KeyOf(KnownLaunchers.Steam.Id, appId.ToString(CultureInfo.InvariantCulture)));
        }

        return games;
    }

    private sealed class NoHold : ISteamControllerHold
    {
        public void Apply(bool steamUsesController)
        {
        }
    }
}
