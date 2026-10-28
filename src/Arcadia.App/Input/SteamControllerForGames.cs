using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Arcadia.Pan;
using Arcadia.Input;

namespace Arcadia.App.Input;

/// <summary>
/// Wires the Steam Controller for games to Arcadia's state. Pan's emulation (#242, <see cref="PanEmulation"/>) hides the
/// real puck and shows Steam a copy while the switch "Controller-Wächter (Pan)" is on; within it, the running games and
/// their mode ("Arcadia" or "Steam-Eingabe") decide whether Arcadia's Xbox pad is plugged in (<see cref="SteamControllerRouting"/>),
/// and <see cref="SteamControllerCopyRules"/> decide whether the copy or the pad passes input: nothing while Arcadia is
/// in front or the mouse mode is on, and Back + Start is withheld while the mouse mode is not "Aus".
/// Everything stands under Pan's watch (#243): every poll pulses the heartbeat, and a teardown by Pan (Arcadia hung) or a
/// lost Pan shows as a notice (<see cref="PanNotices"/>) while the emulation comes back by itself.
/// "Controller wiederherstellen" and "Erneut versuchen" let pad and emulation count their failures afresh.
/// </summary>
public sealed class SteamControllerForGames : Settings.IPanEmulationView
{
    private SteamControllerForGames(SteamControllerXboxPad pad, SteamControllerRouting routing, PanEmulation emulation, SteamControllerCopyFilter copyFilter)
    {
        Pad = pad;
        Routing = routing;
        Emulation = emulation;
        CopyFilter = copyFilter;
    }

    public SteamControllerXboxPad Pad { get; }

    public SteamControllerRouting Routing { get; }

    public PanEmulation Emulation { get; }

    /// <summary>What passes between Steam and the real puck; its <see cref="SteamControllerCopyFilter.FirmwareBlocked"/> opens the firmware dialog.</summary>
    public SteamControllerCopyFilter CopyFilter { get; }

    /// <param name="pads">The virtual pads; Pan's emulation plugs them in here.</param>
    /// <param name="hold">The copy of the puck for Steam; <see cref="NoSteamControllerCopy"/> until this build can make it.</param>
    /// <param name="controllerSupport">Its "Controller wiederherstellen" ends the pad on purpose (#243).</param>
    /// <param name="activeGames">The keys of the games that run or are being started (<c>RunningGamesTracker.ActiveGames</c>).</param>
    /// <param name="runningSteamAppId">The Steam game running right now, also one started directly in Steam (#119).</param>
    /// <param name="isSteamInFront">A window of Steam itself is in front; asked on every poll, so it must be cheap.</param>
    /// <param name="panSupported">Pan can run on this system (Windows); elsewhere nothing is emulated and the mode doesn't show.</param>
    public static SteamControllerForGames Create(
        bool panSupported,
        IVirtualXboxPads pads,
        IEmulationGuardian guardian,
        ISteamControllerHold hold,
        ControllerSupportService controllerSupport,
        SettingsService settings,
        Func<IReadOnlyCollection<string>> activeGames,
        Func<int?> runningSteamAppId,
        Func<bool> isArcadiaActive,
        Func<bool> isMouseModeOn,
        Func<bool> isSteamInFront,
        PanNotices notices,
        TimeProvider time)
    {
        SteamControllerXboxPad? padForPresence = null;
        var emulation = new PanEmulation(pads, guardian, hold, time, Services.AppLog.Write)
        {
            Supported = panSupported,
            Enabled = () => settings.Current.PanEnabled,
            SteamInFront = isSteamInFront,
            SteamControllerPresent = () => hold.PuckPresent || padForPresence?.SteamControllerConnected == true,
        };
        var routing = new SteamControllerRouting(
            activeGames,
            key => settings.Current.UsesArcadiaPad(key),
            runningSteamAppId,
            () => emulation.Status.State == PanEmulationState.Running,
            Services.AppLog.Write);
        var combos = new CombosFromSettings(settings);
        var pad = new SteamControllerXboxPad(emulation, time, Services.AppLog.Write)
        {
            Wanted = () => routing.Current.PadWanted,
            ForwardingAllowed = () => SteamControllerCopyRules.PadPasses(routing.Current, new ForegroundNow(isArcadiaActive(), isMouseModeOn(), isSteamInFront())),
            Combos = combos.Current,
            FeedAlive = guardian.Pulse,
        };
        padForPresence = pad;
        pad.GaveUp += text =>
        {
            emulation.PadGaveUp();
            notices.Show(text);
        };
        emulation.Notice += notices.Show;
        controllerSupport.ControllersRestored += (_, _) =>
        {
            pad.ForgetLosses();
            emulation.ForgetFailures();
        };
        guardian.TornDown += (_, reason) => notices.Show(TornDownText(reason));
        guardian.Lost += _ => notices.Show(PanLostText);
        return new SteamControllerForGames(pad, routing, emulation, new SteamControllerCopyFilter(time, Services.AppLog.Write));
    }

    public PanEmulationStatus Status => Emulation.Status;

    public event Action? Changed
    {
        add => Emulation.Changed += value;
        remove => Emulation.Changed -= value;
    }

    /// <summary>"Erneut versuchen" on a stopped emulation: pad and emulation start counting afresh, and the next tick tries again.</summary>
    public void Retry()
    {
        Pad.ForgetLosses();
        Emulation.ForgetFailures();
    }

    /// <summary>Every 2 s, off the UI thread: keeps the emulation, decides anew, then plugs the pad in or out.</summary>
    public async Task TickAsync()
    {
        // The emulation first: the route asks whether it runs, and a game's pad needs it.
        await Emulation.TickAsync();
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
