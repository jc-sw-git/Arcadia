using Arcadia.App.Services;
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
    private SteamControllerForGames(SteamControllerXboxPad pad, TakenOverControllers others, SteamControllerRouting routing, PanEmulation emulation, SteamControllerCopyFilter copyFilter, FirmwareAttemptWatch firmwareAttempt)
    {
        Pad = pad;
        Others = others;
        Routing = routing;
        Emulation = emulation;
        CopyFilter = copyFilter;
        FirmwareAttempt = firmwareAttempt;
    }

    public SteamControllerXboxPad Pad { get; }

    /// <summary>Every other controller, hidden by Pan and emulated for games (#238).</summary>
    public TakenOverControllers Others { get; }

    public SteamControllerRouting Routing { get; }

    public PanEmulation Emulation { get; }

    /// <summary>What passes between Steam and the real puck; its firmware messages feed <see cref="FirmwareAttempt"/>.</summary>
    public SteamControllerCopyFilter CopyFilter { get; }

    /// <summary>A firmware update Steam tries through the copy (#295).</summary>
    public FirmwareAttemptWatch FirmwareAttempt { get; }

    /// <summary>Steam tried a firmware update through the copy while no game was in front: the notice opens. Any thread.</summary>
    public event Action? FirmwareUpdateTried;

    /// <summary>Instead of the notice while a game is in front: Arcadia doesn't come over the game for it.</summary>
    public const string FirmwareInGameText = "Pan: Steam kann die Firmware nicht aktualisieren, solange Arcadia den Steam Controller verwaltet. Für das Update nach dem Spiel Arcadia beenden.";

    /// <param name="pads">The virtual pads; Pan's emulation plugs them in here.</param>
    /// <param name="copy">
    /// Makes the copy of the puck for Steam from what it passes through and when (#242); <see cref="NoSteamControllerCopy"/>
    /// where this build can't make one.
    /// </param>
    /// <param name="controllerSupport">Its "Controller wiederherstellen" ends the pad on purpose (#243).</param>
    /// <param name="activeGames">The keys of the games that run or are being started (<c>RunningGamesTracker.ActiveGames</c>).</param>
    /// <param name="runningSteamAppId">The Steam game running right now, also one started directly in Steam (#119).</param>
    /// <param name="isSteamInFront">A window of Steam itself is in front; asked on every poll, so it must be cheap.</param>
    /// <param name="gameInFront">The key of the library game in front, or null; asked when Steam tries a firmware update.</param>
    /// <param name="gamepad">
    /// Arcadia's controllers: it lets go of the puck (<c>ReadSteamControllers</c>, #308) or of every controller
    /// (<c>ReadControllers</c>, #335) while Pan restarts what it hides, and a game's rumble goes back through it (#238 D5).
    /// </param>
    /// <param name="panSupported">Pan can run on this system (Windows); elsewhere nothing is emulated and the mode doesn't show.</param>
    public static SteamControllerForGames Create(
        bool panSupported,
        IVirtualXboxPadServer pads,
        IEmulationGuardian guardian,
        Func<SteamControllerCopyWiring, ISteamControllerHold> copy,
        ControllerSupportService controllerSupport,
        SettingsService settings,
        Func<IReadOnlyCollection<string>> activeGames,
        Func<int?> runningSteamAppId,
        Func<bool> isArcadiaActive,
        Func<bool> isMouseModeOn,
        Func<bool> isSteamInFront,
        Func<string?> gameInFront,
        IGamepadSource gamepad,
        PanNotices notices,
        TimeProvider time)
    {
        SteamControllerXboxPad? padForPresence = null;
        SteamControllerRouting? routingForCopy = null;
        var filter = new SteamControllerCopyFilter(time, Services.AppLog.Write);
        // Its own combo state: the copy reads it on its reader thread, the pad on the polling thread. With Steam's desktop
        // layout in use, ⧉ + ≡ reaches Steam too, as from any other controller (#315).
        var copyCombos = new CombosFromSettings(settings, toSteam: true);
        var gate = new SteamControllerCopyGate(
            () => routingForCopy?.Current,
            () => new ForegroundNow(isArcadiaActive(), isMouseModeOn(), isSteamInFront()),
            () => settings.Current.IgnoreSteamDesktopLayout,
            Services.AppLog.Write);
        var hold = copy(new SteamControllerCopyWiring(filter, gate.Passes, copyCombos.Current));
        var othersForSteam = new CombosFromSettings(settings, toSteam: true);
        var othersForGames = new CombosFromSettings(settings, toSteam: false);
        var others = new TakenOverControllers(time, Services.AppLog.Write)
        {
            // Unlike the Steam Controller's pad, also while Steam is in front: Steam sees only these pads now.
            ForwardingAllowed = () => SteamControllerCopyRules.OtherControllerPasses(
                new ForegroundNow(isArcadiaActive(), isMouseModeOn(), SteamInFront: false), settings.Current.IgnoreSteamDesktopLayout),
            Combos = () => SteamControllerCopyRules.OtherControllerOnlyForSteam(new ForegroundNow(isArcadiaActive(), isMouseModeOn(), SteamInFront: false))
                ? othersForSteam.Current()
                : othersForGames.Current(),
            ReadControllers = on =>
            {
                gamepad.ReadControllers(on);
                if (!on)
                {
                    padForPresence?.Rest();
                }
            },
            Pulse = guardian.Pulse,
            Rumble = (padId, motors) => Rumble(gamepad, padId, motors),
        };
        var emulation = new PanEmulation(pads, guardian, hold, time, Services.AppLog.Write)
        {
            Others = others,
            Supported = panSupported,
            // While the installed Pan is about to be replaced (#297), nothing starts on it: the update would end it.
            Enabled = () => settings.Current.PanEnabled && !controllerSupport.IsPanUpdateDue,
            SteamControllerPresent = () => hold.PuckPresent || padForPresence?.SteamControllerConnected == true,
            ReadSteamControllers = gamepad.ReadSteamControllers,
        };
        var routing = new SteamControllerRouting(
            activeGames,
            controllerSupport.UsesArcadiaPad,
            runningSteamAppId,
            () => emulation.SteamControllerTakenOver,
            Services.AppLog.Write);
        routingForCopy = routing;
        var combos = new CombosFromSettings(settings, toSteam: false);
        var pad = new SteamControllerXboxPad(emulation, time, Services.AppLog.Write)
        {
            Wanted = () => routing.Current.PadWanted,
            ForwardingAllowed = () => SteamControllerCopyRules.PadPasses(routing.Current, new ForegroundNow(isArcadiaActive(), isMouseModeOn(), isSteamInFront()), settings.Current.IgnoreSteamDesktopLayout),
            Combos = combos.Current,
            FeedAlive = guardian.Pulse,
            Rumble = (padId, motors) => Rumble(gamepad, padId, motors),
        };
        padForPresence = pad;
        pad.GaveUp += text =>
        {
            emulation.PadGaveUp();
            notices.Show(text);
        };
        others.GaveUp += text =>
        {
            emulation.PadGaveUp();
            notices.Show(text);
        };
        emulation.Notice += notices.Show;
        controllerSupport.ControllersRestored += (_, _) =>
        {
            pad.ForgetLosses();
            others.ForgetLosses();
            emulation.ForgetFailures();
        };
        guardian.TornDown += (_, reason) => notices.Show(TornDownText(reason));
        guardian.Lost += _ => notices.Show(PanLostText);
        var attempt = new FirmwareAttemptWatch(time, Services.AppLog.Write);
        filter.UpdateTraffic += attempt.Traffic;
        var forGames = new SteamControllerForGames(pad, others, routing, emulation, filter, attempt);
        // Off the copy's thread, which must not wait on the look at the foreground window.
        attempt.Attempted += () => Task.Run(() =>
        {
            if (gameInFront() is not null)
            {
                notices.Show(FirmwareInGameText);
            }
            else
            {
                forGames.FirmwareUpdateTried?.Invoke();
            }
        }).Forget("Hinweis zum Firmware-Update");
        return forGames;
    }

    private static void Rumble(IGamepadSource gamepad, int padId, XboxRumble motors)
    {
        var (low, high) = XboxPadState.RumbleStrengths(motors);
        gamepad.HoldRumble(padId, low, high);
    }

    public PanEmulationStatus Status => Emulation.Status;

    public Task ReleaseForFirmwareAsync() => Emulation.ReleaseForFirmwareAsync();

    public IReadOnlyList<ControllerNotHidden> NotHidden => Others.NotHidden;

    public IReadOnlyList<ControllerWaitingForTool> WaitingForTool => Others.WaitingForTool;

    public event Action? Changed
    {
        add
        {
            Emulation.Changed += value;
            Others.Changed += value;
        }

        remove
        {
            Emulation.Changed -= value;
            Others.Changed -= value;
        }
    }

    /// <summary>"Erneut versuchen" on a stopped emulation: pad and emulation start counting afresh, and the next tick tries again.</summary>
    public void Retry()
    {
        Pad.ForgetLosses();
        Others.ForgetLosses();
        Emulation.Retry();
    }

    public bool Retrying => Emulation.Retrying;

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
    /// <param name="toSteam">For the copy: while Steam's desktop layout is not ignored, Steam gets ⧉ + ≡ (#315).</param>
    private sealed class CombosFromSettings(SettingsService settings, bool toSteam)
    {
        private AppSettings? _from;
        private ComboWithholdingOptions _options = new(false);

        public ComboWithholdingOptions Current()
        {
            var current = settings.Current;
            if (!ReferenceEquals(current, _from))
            {
                _from = current;
                var mouseModeCanSwitch = current.MouseMode.Activation != MouseModeActivation.Off;
                _options = toSteam
                    ? SteamControllerCopyRules.CombosKeptFromSteam(mouseModeCanSwitch, current.IgnoreSteamDesktopLayout)
                    : new ComboWithholdingOptions(mouseModeCanSwitch);
            }

            return _options;
        }
    }
}
