using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Arcadia.Pan;
using Arcadia.Input;

namespace Arcadia.App.Input;

/// <summary>
/// Wires the Steam Controller's virtual Xbox pad for games (#213) to Arcadia's state: the running games and their mode
/// ("Arcadia" or "Steam-Eingabe") decide whether it is plugged in (<see cref="SteamControllerRouting"/>), a game gets
/// nothing while Arcadia is in front or the mouse mode is on, and Back + Start is withheld while the mouse mode is not "Aus".
/// The pad stands under Pan's watch (#243): no pad without Pan, every poll pulses the heartbeat, and a teardown by Pan
/// (Arcadia hung) or a lost Pan shows as a notice (<see cref="PanNotices"/>) while the pad comes back by itself.
/// </summary>
public sealed class SteamControllerForGames
{
    private SteamControllerForGames(SteamControllerXboxPad pad, SteamControllerRouting routing)
    {
        Pad = pad;
        Routing = routing;
    }

    public SteamControllerXboxPad Pad { get; }

    public SteamControllerRouting Routing { get; }

    /// <param name="pads">The virtual pads; they are put under <paramref name="guardian"/>'s watch here.</param>
    /// <param name="activeGames">The keys of the games that run or are being started (<c>RunningGamesTracker.ActiveGames</c>).</param>
    /// <param name="runningSteamAppId">The Steam game running right now, also one started directly in Steam (#119).</param>
    public static SteamControllerForGames Create(
        IVirtualXboxPads pads,
        IEmulationGuardian guardian,
        SettingsService settings,
        Func<IReadOnlyCollection<string>> activeGames,
        Func<int?> runningSteamAppId,
        Func<bool> isArcadiaActive,
        Func<bool> isMouseModeOn,
        PanNotices notices,
        TimeProvider time)
    {
        var guarded = new GuardedXboxPads(pads, guardian);
        var routing = new SteamControllerRouting(
            activeGames,
            key => settings.Current.UsesArcadiaPad(key),
            runningSteamAppId,
            () => guarded.Unavailable() is null,
            Services.AppLog.Write);
        var combos = new CombosFromSettings(settings);
        var pad = new SteamControllerXboxPad(guarded, time, Services.AppLog.Write)
        {
            Wanted = () => routing.Current.PadWanted,
            ForwardingAllowed = () => !isMouseModeOn() && !isArcadiaActive(),
            Combos = combos.Current,
            FeedAlive = guardian.Pulse,
        };
        pad.GaveUp += notices.Show;
        guardian.TornDown += (_, reason) => notices.Show(TornDownText(reason));
        guardian.Lost += _ => notices.Show(PanLostText);
        return new SteamControllerForGames(pad, routing);
    }

    /// <summary>Every 2 s, off the UI thread: decides anew, then plugs the pad in or out.</summary>
    public async Task TickAsync()
    {
        await Task.Run(Routing.Update);
        await Pad.TickAsync();
    }

    /// <summary>
    /// Right before a game starts (it is already among the active games): a game on "Steam-Eingabe" must not find
    /// Arcadia's pad when it looks for controllers, so the pad goes now, not at the next tick.
    /// </summary>
    public async Task BeforeGameStartsAsync(CancellationToken cancellationToken)
    {
        // A VIIPER that hangs must not hold up the game: it starts anyway, and the next tick tries again.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(BeforeStartLimit);
        try
        {
            await Task.Run(Routing.Update, limit.Token);
            await Pad.TickNowAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Services.AppLog.Write($"Xbox-Pad: vor dem Spielstart nicht innerhalb von {BeforeStartLimit.TotalSeconds:0} s angepasst");
        }
    }

    private static readonly TimeSpan BeforeStartLimit = TimeSpan.FromSeconds(3);

    /// <summary>Pan broke off while a pad was in; the pad comes back by itself once Pan is back.</summary>
    public const string PanLostText = "Steam Controller: Verbindung zu Pan verloren – Arcadia entfernt den Xbox Controller und verbindet sich neu";

    /// <summary>Pan tore the emulation down; the next tick plugs the pad in again.</summary>
    public static string TornDownText(PanReason reason) => reason switch
    {
        PanReason.Hang => "Steam Controller: Arcadia hat 3 Sekunden nicht reagiert, Pan hat den Xbox Controller entfernt – er kommt gleich zurück",
        PanReason.ServiceStopping => "Steam Controller: Pan wurde beendet und hat den Xbox Controller entfernt – er kommt zurück, sobald Pan wieder läuft",
        _ => $"Steam Controller: Pan hat den Xbox Controller entfernt ({reason}) – er kommt gleich zurück",
    };

    /// <summary>Read on every poll; rebuilt only when the settings changed.</summary>
    private sealed class CombosFromSettings(SettingsService settings)
    {
        private AppSettings? _from;
        private ComboWithholdingOptions _options = new(false);

        public ComboWithholdingOptions Current()
        {
            var current = settings.Current;
            if (!ReferenceEquals(current, _from))
            {
                _from = current;
                _options = new ComboWithholdingOptions(current.MouseMode.Activation != MouseModeActivation.Off);
            }

            return _options;
        }
    }
}
