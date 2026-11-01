using Arcadia.ControllerSupport.Copy;
using Arcadia.Input;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>Where Pan's emulation of the Steam Controller stands (#242); what the settings row, the header and the mode show.</summary>
public enum PanEmulationState
{
    /// <summary>The switch "Controller-Wächter (Pan)" is off: nothing is emulated, Steam and games see the real controllers.</summary>
    Off,

    /// <summary>Not on this system (Linux, until there is a way there): like off, and the mode doesn't show.</summary>
    Unsupported,

    /// <summary>On, but a part or Pan itself is missing or not answering (<see cref="PanEmulationStatus.Reason"/>).</summary>
    Unavailable,

    /// <summary>On and ready, but no Steam Controller is connected yet: nothing to hide.</summary>
    Ready,

    /// <summary>Hiding the real puck and bringing up the copy (~2.5 s, the puck restarts), or Pan still cleaning up.</summary>
    SettingUp,

    /// <summary>The puck is hidden, the copy is up (while it is connected), and Arcadia's Xbox pad may be plugged in.</summary>
    Running,

    /// <summary>
    /// On, but Pan can't hide now or the copy didn't come up; it tries again by itself. Or Arcadia doesn't see what Pan
    /// hides (<see cref="PanEmulationProblem.NotVisibleToArcadia"/>) and waits for its restart.
    /// </summary>
    Paused,

    /// <summary>Failed too often in a short time: Arcadia waits for "Erneut versuchen" (or its next start).</summary>
    Stopped,
}

/// <summary>Why the emulation is paused or stopped, for the row under "Controller-Wächter (Pan)".</summary>
public enum PanEmulationProblem
{
    None,

    /// <summary>Pan can't hide the real puck now (<see cref="PanEmulationStatus.Hiding"/> says why), or lost the hiding.</summary>
    Hiding,

    /// <summary>The copy for Steam (or VIIPER) didn't come up; the real puck is shown again.</summary>
    CopyFailed,

    /// <summary>Pan still shows the real controllers after the last emulation; no problem, it goes on by itself.</summary>
    CleaningUp,

    /// <summary>A Valve bootloader is connected (Steam flashes a firmware update): Pan hides nothing until it is gone (#295).</summary>
    FirmwareUpdate,

    /// <summary>The player released them for a firmware update: Steam Controller and puck are left alone until Pan is switched off and on.</summary>
    Excluded,

    /// <summary>Too many failures in <see cref="PanEmulation.FailureWindow"/>.</summary>
    TooManyFailures,

    /// <summary>
    /// Controllers Arcadia saw before a hide didn't come back for Arcadia itself (#396): HidHide doesn't let this process
    /// see them, e.g. as it was installed while Arcadia ran. They are shown again; only a restart of Arcadia helps.
    /// </summary>
    NotVisibleToArcadia,
}

/// <param name="Reason">For the log and the row's subtitle: what is missing or failed.</param>
/// <param name="Hiding">Pan's answer when <paramref name="Problem"/> is <see cref="PanEmulationProblem.Hiding"/>.</param>
public sealed record PanEmulationStatus(
    PanEmulationState State,
    PanEmulationProblem Problem = PanEmulationProblem.None,
    PanReason Hiding = PanReason.None,
    string? Reason = null)
{
    public static readonly PanEmulationStatus Off = new(PanEmulationState.Off);

    /// <summary>On but not running: the mode greys out with a triangle, the header and the category show the triangle.</summary>
    public bool HasProblem => State is PanEmulationState.Unavailable or PanEmulationState.Paused or PanEmulationState.Stopped;
}

/// <summary>
/// Pan's emulation of the Steam Controller (#242): one generation under Pan's watch (#243) per run, in this order: VIIPER
/// runs, Pan registers the emulation, Pan hides the real puck, the copy for Steam comes up, then Arcadia's Xbox pad
/// is plugged in and out within it as games need (<see cref="SteamControllerXboxPad"/> plugs it through this class).
/// Undone in reverse: pad and copy go, Pan shows the puck again, the emulation is unregistered.
/// </summary>
/// <remarks>
/// The owner's rules (decisions 1, 5, 6): hide once, at the start (or when the puck arrives), and show at the end, so
/// switching a game's mode or starting a game never makes the controller drop out. No hiding, no emulation: without a
/// working hide neither copy nor pad runs, and a copy that fails to come up shows the puck again. Hiding lost while
/// running (HidHide's configuration client, an uninstall) ends pad and copy at once, with a notice, and the emulation
/// comes back by itself once hiding works. Everything runs under one gate in <see cref="TickAsync"/> (every 2 s).
/// </remarks>
public sealed class PanEmulation : IVirtualXboxPads
{
    public const int MaxFailuresPerWindow = 3;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);

    /// <summary>A hide that failed (the puck didn't restart, HidHide changed nothing) is tried again after this (designs: "alle 10 Sekunden").</summary>
    public static readonly TimeSpan HideRetry = TimeSpan.FromSeconds(10);

    public const string PausedText = "Pan: Controller-Wächter pausiert. Die echten Controller werden nicht mehr versteckt.";
    public const string RunningAgainText = "Pan: Controller-Wächter läuft wieder";
    public const string StoppedText = "Pan: Emulation angehalten – sie ist mehrmals kurz hintereinander ausgefallen. „Erneut versuchen“ in den Controller-Einstellungen startet sie neu.";
    public const string RetryFailedText = "Pan: Erneuter Versuch ohne Erfolg. Was fehlt, steht in den Controller-Einstellungen.";
    public const string RetryNoControllerText = "Pan: Kein Controller verbunden, es gibt nichts zu emulieren.";
    public const string RetryExcludedText = "Pan: Steam Controller und Puck bleiben ausgenommen.";

    public const string FirmwareUpdateReason = "Steam spielt ein Firmware-Update auf";

    public const string NotVisibleText = "Pan: Arcadia sieht die versteckten Controller nicht mehr und hat sie wieder freigegeben. Starte Arcadia neu, die Controller-Einstellungen bieten das an.";

    /// <summary>The name the Steam Controller has in <see cref="NotVisibleToArcadia"/>.</summary>
    public const string SteamControllerName = "Steam Controller";

    private readonly IVirtualXboxPadServer _pads;
    private readonly IEmulationGuardian _guardian;
    private readonly ISteamControllerHold _hold;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Cancelled when Arcadia exits: a tick in a long request would hold the gate past the exit's wait.
    private readonly CancellationTokenSource _exiting = new();
    private readonly List<long> _failures = [];

    private volatile Session? _session;
    private volatile PanEmulationStatus _status = PanEmulationStatus.Off;
    // Monotonic timestamps; written by "Erneut versuchen" on the UI thread too, so read and written as one long.
    private long _hideRetryAt = long.MinValue;
    private bool _pausedByLoss;

    // "Erneut versuchen" or "Controller wiederherstellen" was pressed: that attempt shows it runs, the automatic ones don't.
    private volatile bool _retryAsked;

    // "Erneut versuchen" (#308): pressed, not seen by a tick yet (1), or tried by a tick that started after the press (2).
    private int _retry;
    private volatile bool _gaveUp;

    // "Controller freigeben" (#295): Steam Controller and puck stay Steam's until the switch goes off, or Arcadia restarts.
    private volatile bool _released;
    private string? _lastLogged;

    // The controllers that didn't come back for Arcadia after a hide (#396): kept until Arcadia restarts, as nothing
    // else gives this process HidHide's allow list back.
    private volatile IReadOnlyList<string>? _notVisible;

    public PanEmulation(IVirtualXboxPadServer pads, IEmulationGuardian guardian, ISteamControllerHold hold, TimeProvider time, Action<string> log)
    {
        _pads = pads;
        _guardian = guardian;
        _hold = hold;
        _time = time;
        _log = log;
        guardian.PuckRestarting += OnPanRestartsPuck;
        guardian.PuckRestarted += OnPanRestartedPuck;
    }

    /// <summary>The switch "Controller-Wächter (Pan)"; read on every tick.</summary>
    public Func<bool> Enabled { get; init; } = () => true;

    /// <summary>
    /// Why nothing may be hidden now, or null; read on every tick. A HidHide install runs or Arcadia restarts after one
    /// (#396): this process would not see what Pan hides.
    /// </summary>
    public Func<string?> HeldBack { get; init; } = () => null;

    /// <summary>The controllers Arcadia saw before a hide and no longer sees since (#396); empty while that didn't happen.</summary>
    public IReadOnlyList<string> NotVisibleToArcadia => _notVisible ?? [];

    /// <summary>Attaching the copy waits this long at most: a hanging attach would hold the gate and stop every tick.</summary>
    public static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long the exit waits for a running tick to let go of the gate after cancelling it; below the 5 s Arcadia's exit
    /// waits for the whole cleanup.
    /// </summary>
    public static readonly TimeSpan ShutdownGateTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// After the copy followed the controller (#345), how long it waits for the new device to come back from Pan's
    /// restart before it copies whatever is there (a cable unplugged right away, for instance).
    /// </summary>
    public static readonly TimeSpan FollowWait = TimeSpan.FromSeconds(10);

    /// <summary>Pan can run on this system at all (Windows).</summary>
    public bool Supported { get; init; } = true;

    /// <summary>
    /// A Steam Controller is around (its puck, or SDL sees one): only then is there something to hide. Read on every tick
    /// while no emulation runs; once it runs, it stays until the end (a replug while hidden needs nothing).
    /// </summary>
    public Func<bool> SteamControllerPresent { get; init; } = () => true;

    /// <summary>
    /// Every other controller, for games (#238): Pan's emulation runs for them too, also without a Steam Controller or
    /// after "Controller freigeben" (D2), and hides and emulates them within it. Null: only the Steam Controller.
    /// </summary>
    public TakenOverControllers? Others { get; init; }

    private bool OthersConnected => Others?.AnyConnected == true;

    /// <summary>
    /// The Steam Controller is Arcadia's for games now: the emulation runs and its puck is hidden (or this build has no
    /// copy, so nothing is hidden for it). Only then may Arcadia's Xbox pad stand in for it: never both (#238 D7).
    /// </summary>
    public bool SteamControllerTakenOver =>
        _session is { Ended: false } session && _status.State == PanEmulationState.Running && (session.Hidden || !Hides);

    /// <summary>
    /// Stops (false) or resumes Arcadia's own reading of the real puck (SDL, <c>IGamepadSource.ReadSteamControllers</c>).
    /// A hide or show restarts the puck, and Windows refuses that while any program holds it open, Arcadia too (#308).
    /// </summary>
    public Action<bool> ReadSteamControllers { get; init; } = _ => { };

    public PanEmulationStatus Status => _status;

    /// <summary>Pan hides the puck now; any thread.</summary>
    public bool HidesPuck => _session is { Ended: false, Hidden: true };

    /// <summary>The puck is hidden for this emulation (only when this build has the copy).</summary>
    public bool Hides => _hold.CanCopy;

    /// <summary><see cref="Status"/> or <see cref="Retrying"/> changed; any thread.</summary>
    public event Action? Changed;

    /// <summary>A notice for a toast or the in-game notice: hiding lost, back again, given up. Any thread.</summary>
    public event Action<string>? Notice;

    /// <summary>Every 2 s, off the UI thread: starts, checks and ends the emulation as needed. Never throws.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (_exiting.IsCancellationRequested || !await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        using var tick = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _exiting.Token);
        try
        {
            // Only a tick that began after the press answers it; one that ran meanwhile shows the old state.
            Interlocked.CompareExchange(ref _retry, 2, 1);
            await StepAsync(tick.Token);
            EndRetryOnceDecided();
        }
        catch (OperationCanceledException) when (_exiting.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Arcadia exits; ShutdownAsync cleans up.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"Pan-Emulation: Fehler: {ex}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// "Erneut versuchen" or "Controller wiederherstellen": the failures so far no longer count, and a hide is tried at
    /// the next tick.
    /// </summary>
    public void ForgetFailures()
    {
        lock (_failures)
        {
            _failures.Clear();
            _gaveUp = false;
        }

        Interlocked.Exchange(ref _hideRetryAt, long.MinValue);
        _retryAsked = true;
        _log("Pan-Emulation: bisherige Ausfälle zählen nicht mehr");
    }

    /// <summary>"Erneut versuchen" was pressed and its attempt hasn't ended: the row shows it runs (#308). Any thread.</summary>
    public bool Retrying => Volatile.Read(ref _retry) != 0;

    /// <summary>
    /// "Erneut versuchen": as <see cref="ForgetFailures"/>, and <see cref="Retrying"/> until the attempt has an outcome,
    /// which a <see cref="Notice"/> then tells (owner, 2026-10-29: a sign that something happens, a toast when done).
    /// </summary>
    public void Retry()
    {
        ForgetFailures();
        Interlocked.Exchange(ref _retry, 1);
        Changed?.Invoke();
    }

    /// <summary>
    /// After a tick: an attempt still setting up (Pan cleaning up, the question about the takeover open) goes on; running
    /// is a success, paused, stopped or unavailable a failure; no Steam Controller or "Nein" to the takeover say so.
    /// Only switching Pan off meanwhile ends it without a toast: the switch says it.
    /// </summary>
    private void EndRetryOnceDecided()
    {
        var state = _status.State;
        if (Volatile.Read(ref _retry) != 2 || state == PanEmulationState.SettingUp || Interlocked.CompareExchange(ref _retry, 0, 2) != 2)
        {
            return;
        }

        var text = state switch
        {
            PanEmulationState.Running => RunningAgainText,
            PanEmulationState.Paused or PanEmulationState.Stopped or PanEmulationState.Unavailable => RetryFailedText,
            PanEmulationState.Ready when _status.Problem == PanEmulationProblem.Excluded => RetryExcludedText,
            PanEmulationState.Ready => RetryNoControllerText,
            _ => null,
        };
        _log($"Pan-Emulation: „Erneut versuchen“ beendet ({state})");
        Changed?.Invoke();
        if (text is not null)
        {
            Notice?.Invoke(text);
        }
    }

    /// <summary>Arcadia's Xbox pad gave up after too many losses: the whole emulation stops until "Erneut versuchen".</summary>
    public void PadGaveUp()
    {
        _gaveUp = true;
    }

    public string? Unavailable() =>
        _session is { Ended: false } && _status.State == PanEmulationState.Running ? null : _status.Reason ?? "Pan-Emulation läuft nicht";

    public async Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        var session = _session ?? throw new InvalidOperationException("Pan-Emulation läuft nicht");
        var pad = await _pads.PlugAsync(cancellationToken);
        var guarded = new GuardedXboxPad(pad, session, _guardian);
        if (!session.Add(guarded))
        {
            // The emulation ended while the pad was plugged in.
            await pad.DisposeAsync();
            throw new InvalidOperationException("Pan-Emulation wurde beendet");
        }

        return guarded;
    }

    /// <summary>A pad of the kind (#238 D3), taken out with the emulation like the Xbox pad.</summary>
    public async Task<IVirtualPad> PlugAsync(PadKind kind, CancellationToken cancellationToken)
    {
        var session = _session ?? throw new InvalidOperationException("Pan-Emulation läuft nicht");
        var pad = await _pads.PlugAsync(kind, cancellationToken);
        var guarded = new GuardedKindPad(pad, session, _guardian);
        if (!session.Add(guarded))
        {
            await pad.DisposeAsync();
            throw new InvalidOperationException("Pan-Emulation wurde beendet");
        }

        return guarded;
    }

    /// <summary>Arcadia exits (also for an update): pad and copy go, the puck is shown, VIIPER ends, Pan hears the planned exit.</summary>
    /// <remarks>
    /// A tick still running is cancelled first. If it doesn't let go within <see cref="ShutdownGateTimeout"/>, nothing is
    /// cleaned up here rather than beside it: when Arcadia's process ends without the planned exit, Pan tears the
    /// emulation down and shows the puck itself (teardown <c>ClientGone</c>, docs/controller-support/pan.md).
    /// </remarks>
    public async Task ShutdownAsync()
    {
        await _exiting.CancelAsync();
        if (!await TryEnterGateAsync(ShutdownGateTimeout))
        {
            _log($"Pan-Emulation: ein Durchlauf hält nach {ShutdownGateTimeout.TotalSeconds:0} s noch an; Pan räumt beim Beenden selbst auf.");
            return;
        }

        try
        {
            if (_session is { } session)
            {
                await EndAsync(session, "Arcadia wird beendet");
            }

            await _pads.ShutdownAsync();
            await _guardian.PlannedExitAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TryEnterGateAsync(TimeSpan timeout)
    {
        // A cancelled wait leaves the semaphore's queue, unlike a WaitAsync(timeout) on the returned task.
        using var expiry = new CancellationTokenSource(timeout, _time);
        try
        {
            await _gate.WaitAsync(expiry.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task StepAsync(CancellationToken cancellationToken)
    {
        if (!Supported)
        {
            SetStatus(new(PanEmulationState.Unsupported));
            return;
        }

        if (!Enabled())
        {
            _released = false;
            await EndIfRunningAsync("ausgeschaltet");
            SetStatus(PanEmulationStatus.Off);
            return;
        }

        if (_notVisible is { } notVisible)
        {
            // A new emulation would hide them from Arcadia once more: nothing starts until Arcadia restarts.
            await EndIfRunningAsync("Arcadia sieht die versteckten Controller nicht");
            SetStatus(NotVisibleStatus(notVisible));
            return;
        }

        if (HeldBack() is { } heldBack)
        {
            await EndIfRunningAsync(heldBack);
            SetStatus(new(PanEmulationState.SettingUp, Reason: heldBack));
            return;
        }

        if (_gaveUp)
        {
            await EndIfRunningAsync("zu viele Ausfälle");
            SetStatus(new(PanEmulationState.Stopped, PanEmulationProblem.TooManyFailures, Reason: "zu viele Ausfälle in 5 Minuten"));
            return;
        }

        if ((_pads.Unavailable() ?? _guardian.Unavailable()) is { } missing)
        {
            await EndIfRunningAsync(missing);
            SetStatus(missing == PanClient.CleaningUp
                ? new(PanEmulationState.SettingUp, PanEmulationProblem.CleaningUp, Reason: missing)
                : new(PanEmulationState.Unavailable, Reason: missing));
            return;
        }

        if (_session is { } session)
        {
            await KeepAsync(session, cancellationToken);
            return;
        }

        if (!SteamControllerPresent() && !OthersConnected)
        {
            SetStatus(new(PanEmulationState.Ready));
            return;
        }

        if (Interlocked.Read(ref _hideRetryAt) is var retryAt && retryAt != long.MinValue && _time.GetElapsedTime(retryAt) < HideRetry)
        {
            return;
        }

        await StartAsync(cancellationToken);
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        var puckWanted = Hides && SteamControllerPresent() && !_released;
        if ((puckWanted || OthersConnected)
            && await _guardian.CheckHidingAsync(0, cancellationToken) is var could && could != PanReason.None)
        {
            // Pan can't hide now (HidHide busy, inverse, ...): no emulation at all, the real controller stays as it is.
            SetStatus(could is null
                ? new(PanEmulationState.Unavailable, Reason: "Pan kann den Steam Controller nicht verstecken – Pan aktualisieren")
                : new(PanEmulationState.Paused, PanEmulationProblem.Hiding, could.Value, $"HidHide: {could}"));
            return;
        }

        // Released for a firmware update: only the Steam Controller and its puck stay out; every other controller is
        // still taken over (#238 D2).
        if (_released && Hides && SteamControllerPresent() && !OthersConnected)
        {
            SetExcluded();
            return;
        }

        // An automatic retry after a failed hide keeps its warning until it works, or the row would flicker every 10 s
        // (#308); a retry the player asked for shows at once that it runs (~11 s on the owner's PC).
        if (_retryAsked || _status is not { State: PanEmulationState.Paused, Problem: PanEmulationProblem.Hiding })
        {
            SetStatus(new(PanEmulationState.SettingUp));
        }

        _retryAsked = false;

        uint generation;
        int usbipPort;
        try
        {
            // VIIPER first: Pan takes the client's viiper.exe at the registration, to end it if Arcadia hangs, and journals
            // its usbip port, so its teardown detaches exactly that server's devices (#304).
            usbipPort = await _pads.PrepareAsync(cancellationToken);
            generation = await _guardian.EmulationStartedAsync(usbipPort, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"Pan-Emulation: Start fehlgeschlagen: {ex.Message}");
            if ((_pads.Unavailable() ?? _guardian.Unavailable()) is { } why)
            {
                SetStatus(why == PanClient.CleaningUp
                    ? new(PanEmulationState.SettingUp, PanEmulationProblem.CleaningUp, Reason: why)
                    : new(PanEmulationState.Unavailable, Reason: why));
                return;
            }

            Failed(new(PanEmulationState.Paused, PanEmulationProblem.CopyFailed, Reason: ex.Message));
            return;
        }

        var session = new Session(generation, usbipPort);
        if (puckWanted)
        {
            var seenBefore = _hold.Connected is not null;
            var hidden = await WithPuckLetGoAsync(() => _guardian.HideAsync(generation, cancellationToken));
            if (hidden != PanReason.None && !OthersConnected)
            {
                // The client already asked Pan to undo the rejected hide. Counted: a hide that keeps failing restarts
                // the puck each time (the hide and its undo), so it must not go on forever. A firmware update being
                // flashed is no failure: Pan restarted nothing, and the hide comes once the bootloader is gone.
                await _guardian.EmulationStoppedAsync(generation, CancellationToken.None);
                Interlocked.Exchange(ref _hideRetryAt, _time.GetTimestamp());
                if (hidden == PanReason.BootloaderPresent)
                {
                    SetStatus(new(PanEmulationState.SettingUp, PanEmulationProblem.FirmwareUpdate, hidden, FirmwareUpdateReason));
                }
                else
                {
                    Failed(new(PanEmulationState.Paused, PanEmulationProblem.Hiding, hidden, $"nicht versteckt: {hidden}"));
                }

                return;
            }

            PuckHidden(session, hidden, seenBefore);
        }

        _session = session;
        _log($"Pan-Emulation {generation} läuft{(session.Hidden ? ", Steam Controller versteckt" : Hides ? ", Steam Controller nicht versteckt" : " (ohne Kopie für Steam, der Steam Controller bleibt sichtbar)")}");
        await KeepAsync(session, cancellationToken, mayRestart: false);
    }

    /// <summary>
    /// After a hide of the puck: hidden, or (other controllers keep the emulation running) the Steam Controller stays
    /// Steam's and the hide is tried again later, counted, as a hide that keeps failing restarts the puck each time.
    /// </summary>
    /// <param name="seenBefore">Arcadia saw the puck right before the hide: it must see it again soon after (#396).</param>
    private void PuckHidden(Session session, PanReason hidden, bool seenBefore)
    {
        if (hidden != PanReason.None)
        {
            _log($"Pan-Emulation {session.Generation}: Steam Controller nicht versteckt ({hidden}), die anderen Controller laufen weiter");
            session.PuckRejection = hidden;
            Interlocked.Exchange(ref _hideRetryAt, _time.GetTimestamp());
            if (hidden != PanReason.BootloaderPresent)
            {
                NoteFailure();
            }

            return;
        }

        session.Hidden = true;
        session.PuckRejection = PanReason.None;
        session.AwaitingSightSince = seenBefore ? _time.GetTimestamp() : null;
    }

    /// <summary>
    /// The emulation runs for the other controllers and the Steam Controller (or its puck) is there but not hidden: it
    /// came later, or an earlier hide failed (or a firmware update was being flashed). Hidden now unless released.
    /// </summary>
    private async Task HidePuckLaterAsync(Session session, CancellationToken cancellationToken)
    {
        if (!Hides || session.Hidden || _released || !SteamControllerPresent()
            || (Interlocked.Read(ref _hideRetryAt) is var retryAt && retryAt != long.MinValue && _time.GetElapsedTime(retryAt) < HideRetry))
        {
            return;
        }

        var seenBefore = _hold.Connected is not null;
        var hidden = await WithPuckLetGoAsync(() => _guardian.HideAsync(session.Generation, cancellationToken));
        PuckHidden(session, hidden, seenBefore);
        if (session.Hidden)
        {
            _log($"Pan-Emulation {session.Generation}: Steam Controller versteckt");
        }
    }

    private void SetExcluded() =>
        SetStatus(new(PanEmulationState.Ready, PanEmulationProblem.Excluded, Reason: "Steam Controller und Puck für ein Firmware-Update freigegeben"));

    /// <summary>
    /// "Controller freigeben" after Steam tried a firmware update through the copy (#295): pad and copy go, the puck is
    /// shown, and nothing of the Steam Controller is hidden again until Pan is switched off and on, or Arcadia restarts.
    /// Every other controller stays taken over.
    /// </summary>
    public async Task ReleaseForFirmwareAsync()
    {
        _released = true;
        await _gate.WaitAsync();
        try
        {
            if (_session is { } session && (session.Hidden || !OthersConnected))
            {
                await EndAsync(session, "für ein Firmware-Update freigegeben");
            }

            if (Enabled() && !OthersConnected)
            {
                SetExcluded();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The emulation runs: Pan still has it, the hiding still holds, the copy is up while the puck is there.</summary>
    /// <param name="mayRestart">
    /// False right after a start: a Pan that tore every new generation down at once would otherwise restart the puck
    /// over and over within one tick. The next tick starts again.
    /// </param>
    private async Task KeepAsync(Session session, CancellationToken cancellationToken, bool mayRestart = true)
    {
        if (!_guardian.IsCurrent(session.Generation))
        {
            // Pan tore it down (Arcadia hung, Pan stopped) or the link broke: Pan has undone everything already, and a
            // new generation can start at once, so a game gets its pad back within the same tick.
            await DropAsync(session);
            SetStatus(new(PanEmulationState.SettingUp));
            if (mayRestart && _guardian.Unavailable() is null && (SteamControllerPresent() || OthersConnected))
            {
                await StartAsync(cancellationToken);
            }

            return;
        }

        if (_pads.ServerPort != session.UsbipPort)
        {
            // VIIPER ended (or was replaced) under the emulation: Pan knows only the old server's port, so this emulation
            // ends first, and the next start tells Pan the new one before any device goes onto it (#304, decision 11).
            _log($"Pan-Emulation {session.Generation}: VIIPER verloren");
            await EndAsync(session, "VIIPER verloren");
            Failed(new(PanEmulationState.Paused, PanEmulationProblem.CopyFailed, Reason: "VIIPER verloren"));
            return;
        }

        if (session.Hidden && await _guardian.CheckHidingAsync(session.Generation, cancellationToken) is { } holds && holds != PanReason.None)
        {
            _log($"Pan-Emulation {session.Generation}: Verstecken verloren ({holds})");
            await EndAsync(session, $"Verstecken verloren ({holds})");
            _pausedByLoss = true;
            SetStatus(new(PanEmulationState.Paused, PanEmulationProblem.Hiding, holds, $"Verstecken verloren: {holds}"));
            Notice?.Invoke(PausedText);
            return;
        }

        await HidePuckLaterAsync(session, cancellationToken);
        if (Hides && session.Hidden && !await KeepCopyAsync(session, cancellationToken))
        {
            return;
        }

        SetStatus(new(PanEmulationState.Running));
        if (Others is { } others)
        {
            await others.KeepAsync(session.Generation, _guardian, this, cancellationToken);
        }

        if (!await StillVisibleAsync(session))
        {
            return;
        }

        if (_pausedByLoss)
        {
            _pausedByLoss = false;
            // "Erneut versuchen" says the same when its tick ends.
            if (Volatile.Read(ref _retry) != 2)
            {
                Notice?.Invoke(RunningAgainText);
            }
        }
    }

    /// <summary>
    /// After a hide, Arcadia itself must see the hidden controllers again within <see cref="TakenOverControllers.SightWait"/>:
    /// it is on HidHide's allow list, and reads them for the copy and the pads. A process HidHide doesn't let through
    /// (HidHide installed while it ran, owner's PC 2026-11-01) sees nothing, and would otherwise run without copy or pads
    /// with every controller hidden (#396). Then the emulation ends, Pan shows them, and none starts until a restart.
    /// False then.
    /// </summary>
    private async Task<bool> StillVisibleAsync(Session session)
    {
        List<string> notVisible = [];
        if (session.AwaitingSightSince is { } since && _time.GetElapsedTime(since) >= SightGrace)
        {
            // Not before SightGrace: the presence the hold answers with may still be the one from before the hide.
            if (_hold.Connected is not null)
            {
                session.AwaitingSightSince = null;
            }
            else if (_time.GetElapsedTime(since) >= TakenOverControllers.SightWait)
            {
                notVisible.Add(SteamControllerName);
            }
        }

        notVisible.AddRange(Others?.NotVisibleSinceHide ?? []);
        if (notVisible.Count == 0)
        {
            return true;
        }

        _notVisible = notVisible;
        _log($"Pan-Emulation {session.Generation}: Arcadia sieht die versteckten Controller nicht mehr ({string.Join(", ", notVisible)}); HidHide lässt diesen Prozess nicht durch, erst ein Neustart von Arcadia hilft");
        await EndAsync(session, "Arcadia sieht die versteckten Controller nicht");
        SetStatus(NotVisibleStatus(notVisible));
        Notice?.Invoke(NotVisibleText);
        return false;
    }

    /// <summary>A hide restarts the puck (~2.5 s) and the hold caches its answer for 1 s: no verdict before this.</summary>
    private static readonly TimeSpan SightGrace = TimeSpan.FromSeconds(1.5);

    private static PanEmulationStatus NotVisibleStatus(IReadOnlyList<string> notVisible) =>
        // Paused, not stopped: the player can act on it ("Arcadia neu starten"), so a warning, as the owner chose (#396).
        new(PanEmulationState.Paused, PanEmulationProblem.NotVisibleToArcadia, Reason: $"Arcadia sieht {string.Join(", ", notVisible)} nicht mehr");

    /// <summary>False when the copy failed and the emulation ended with it.</summary>
    private async Task<bool> KeepCopyAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.Copy is { IsConnected: true } copied && _hold.Connected is { } now && now != copied.Connection)
        {
            // The controller was plugged in by cable after it was switched off on the puck (#345): the copy follows it.
            session.Copy = null;
            if (!await RemoveCopyAsync(session, copied) || !await FollowAsync(session, now, cancellationToken))
            {
                return false;
            }
        }

        if (session.Copy is { IsConnected: false } lost)
        {
            session.Copy = null;
            if (!await RemoveCopyAsync(session, lost))
            {
                return false;
            }

            // Asked after the removal, which makes the hold look again: the device that left may have taken the controller
            // to the other one (the cable unplugged moves it to the puck, #345), which is no failure; with neither there,
            // the copy comes back with the controller.
            var connected = _hold.Connected;
            if (connected == lost.Connection)
            {
                _log("Pan-Emulation: Kopie für Steam verloren");
                NoteFailure();
            }
            else if (connected is { } other && !await FollowAsync(session, other, cancellationToken))
            {
                return false;
            }
        }

        if (session.Copy is not null || _hold.Connected is not { } connection)
        {
            return true;
        }

        if (session.Following is { } following)
        {
            if (connection != following.To && _time.GetElapsedTime(following.Since) < FollowWait)
            {
                // Pan's restart takes the new device away for a moment, and only the device left behind is listed meanwhile:
                // a copy of that one now would be followed back again a tick later.
                return true;
            }

            session.Following = null;
        }

        try
        {
            using var timeout = new CancellationTokenSource(AttachTimeout, _time);
            using var attach = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                session.Copy = await _hold.AttachAsync(connection, attach.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Kopie für Steam nach {AttachTimeout.TotalSeconds:0} s nicht angesteckt");
            }

            _log("Pan-Emulation: Kopie für Steam angesteckt");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No copy, no emulation: Steam must not be left without the controller (decision 5).
            _log($"Pan-Emulation: Kopie für Steam startet nicht: {ex.Message}");
            await EndAsync(session, "Kopie startet nicht");
            Failed(new(PanEmulationState.Paused, PanEmulationProblem.CopyFailed, Reason: ex.Message));
            return false;
        }
    }

    /// <summary>False when a copy that failed to go ended the emulation (its forwarding isn't confirmed stopped).</summary>
    private async Task<bool> RemoveCopyAsync(Session session, ISteamControllerCopy copy)
    {
        if (await DisposeQuietlyAsync(copy, "Kopie entfernen"))
        {
            return true;
        }

        // Its forwarding isn't confirmed stopped: no new copy beside it; Pan's teardown ends viiper.exe first.
        session.LeftBehind = true;
        await EndAsync(session, "Kopie lässt sich nicht sauber entfernen");
        Failed(new(PanEmulationState.Paused, PanEmulationProblem.CopyFailed, Reason: "Kopie lässt sich nicht sauber entfernen"));
        return false;
    }

    /// <summary>
    /// The Steam Controller is now on the other device (#345). Before its copy attaches, Pan hides it once more, with
    /// Arcadia letting go: Pan hides a device that arrives on its own, but the copy must not hold it open before Pan's
    /// restart, or the restart is vetoed and Steam may keep a handle it opened in between. A hide refused ends the
    /// emulation, so Steam has the real controller rather than a copy beside it. False then.
    /// </summary>
    private async Task<bool> FollowAsync(Session session, SteamControllerConnection now, CancellationToken cancellationToken)
    {
        _log($"Pan-Emulation: Steam Controller jetzt {(now == SteamControllerConnection.Cable ? "am Kabel" : "am Puck")}, die Kopie folgt");
        var hidden = await WithPuckLetGoAsync(() => _guardian.HideAsync(session.Generation, cancellationToken));
        if (hidden == PanReason.None)
        {
            session.Following = (now, _time.GetTimestamp());
            return true;
        }

        _log($"Pan-Emulation {session.Generation}: Steam Controller nach dem Wechsel nicht versteckt ({hidden})");
        await EndAsync(session, $"Steam Controller nach dem Wechsel nicht versteckt ({hidden})");
        Failed(new(PanEmulationState.Paused, PanEmulationProblem.Hiding, hidden, $"Verstecken nach dem Wechsel: {hidden}"));
        return false;
    }

    private async Task EndIfRunningAsync(string why)
    {
        if (_session is { } session)
        {
            await EndAsync(session, why);
        }
    }

    /// <summary>Pan's order: pad and copy go first (nothing forwards to the puck), then the puck is shown, then the emulation ends.</summary>
    private async Task EndAsync(Session session, string why)
    {
        _log($"Pan-Emulation {session.Generation} endet ({why})");
        _session = null;
        var removed = await RemoveDevicesAsync(session) && !session.LeftBehind;
        Others?.EmulationEnded();
        if (session.Hidden && removed)
        {
            // Not shown (HidHide busy): Pan's teardown after the stop shows it, with its own retries.
            await WithPuckLetGoAsync(() => _guardian.UnhideAsync(session.Generation, CancellationToken.None));
        }
        else if (session.Hidden)
        {
            // A device that failed to go may still forward to the puck: Pan's teardown after the stop ends what is left
            // of the forwarding (viiper.exe) before it shows the puck.
            _log($"Pan-Emulation {session.Generation}: nicht alles entfernt, Pan zeigt den Steam Controller nach seinem Aufräumen");
        }

        await _guardian.EmulationStoppedAsync(session.Generation, CancellationToken.None);
    }

    /// <summary>
    /// Pan hides or shows the puck and restarts it: Arcadia lets go of it first, as Windows refuses the restart of a device
    /// a program holds open (System log, event 225, owner's PC 2026-10-29), and the hide would fail every time (#308).
    /// </summary>
    private async Task<T> WithPuckLetGoAsync<T>(Func<Task<T>> restart)
    {
        LetGoOfPuck();
        try
        {
            return await restart();
        }
        finally
        {
            ReadPuckAgain();
        }
    }

    // Who wants the puck let go right now (a hide or show of Arcadia's, a restart of Pan's own); read again at 0.
    private readonly Lock _letGoLock = new();
    private int _letGo;
    private bool _panRestartsPuck;

    private void LetGoOfPuck()
    {
        lock (_letGoLock)
        {
            if (_letGo++ == 0)
            {
                ReadSteamControllers(false);
            }
        }
    }

    private void ReadPuckAgain()
    {
        lock (_letGoLock)
        {
            if (--_letGo == 0)
            {
                ReadSteamControllers(true);
            }
        }
    }

    /// <summary>Pan restarts the puck on its own (after a teardown, its retry, an arrival): let go until it's over (#308).</summary>
    private void OnPanRestartsPuck()
    {
        lock (_letGoLock)
        {
            if (_panRestartsPuck)
            {
                return;
            }

            _panRestartsPuck = true;
            LetGoOfPuck();
        }
    }

    private void OnPanRestartedPuck()
    {
        lock (_letGoLock)
        {
            if (!_panRestartsPuck)
            {
                return;
            }

            _panRestartsPuck = false;
            ReadPuckAgain();
        }
    }

    /// <summary>Pan has undone the emulation itself: the devices go, and a teardown is acknowledged.</summary>
    private async Task DropAsync(Session session)
    {
        _log($"Pan-Emulation {session.Generation}: von Pan beendet");
        _session = null;
        await RemoveDevicesAsync(session);
        Others?.EmulationEnded();
        if (_guardian.WasTornDown(session.Generation))
        {
            await _guardian.AcknowledgeTeardownAsync(session.Generation, CancellationToken.None);
        }
    }

    /// <summary>False if a device failed to go.</summary>
    private async Task<bool> RemoveDevicesAsync(Session session)
    {
        var removed = true;
        foreach (var pad in session.End())
        {
            removed &= await DisposeQuietlyAsync(pad, "Xbox-Pad entfernen");
        }

        if (session.Copy is { } copy)
        {
            session.Copy = null;
            removed &= await DisposeQuietlyAsync(copy, "Kopie entfernen");
        }

        return removed;
    }

    private async Task<bool> DisposeQuietlyAsync(IAsyncDisposable device, string what)
    {
        try
        {
            await device.DisposeAsync();
            return true;
        }
        catch (Exception ex)
        {
            _log($"Pan-Emulation: {what} fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    private void Failed(PanEmulationStatus status)
    {
        SetStatus(status);
        NoteFailure();
    }

    private void NoteFailure()
    {
        lock (_failures)
        {
            _failures.RemoveAll(at => _time.GetElapsedTime(at) > FailureWindow);
            _failures.Add(_time.GetTimestamp());
            if (_failures.Count <= MaxFailuresPerWindow || _gaveUp)
            {
                return;
            }

            _gaveUp = true;
        }

        _log($"Pan-Emulation: {MaxFailuresPerWindow + 1} Ausfälle in {FailureWindow.TotalMinutes:0} Minuten, Arcadia versucht es erst nach „Erneut versuchen“ wieder");
        Notice?.Invoke(StoppedText);
    }

    private void SetStatus(PanEmulationStatus status)
    {
        if (status == _status)
        {
            return;
        }

        _status = status;
        var line = $"Pan-Emulation: {status.State}{(status.Problem == PanEmulationProblem.None ? string.Empty : $" ({status.Problem})")}{(status.Reason is null ? string.Empty : $": {status.Reason}")}";
        if (line != _lastLogged)
        {
            _lastLogged = line;
            _log(line);
        }

        Changed?.Invoke();
    }

    /// <summary>One generation: what Arcadia plugged in within it. Its pads are taken out together when it ends.</summary>
    private sealed class Session(uint generation, int usbipPort)
    {
        private readonly List<GuardedDevice> _pads = [];

        public uint Generation { get; } = generation;

        /// <summary>The usbip port of the VIIPER Pan journaled for this generation; devices go only onto that server.</summary>
        public int UsbipPort { get; } = usbipPort;

        public bool Hidden { get; set; }

        /// <summary>Why the puck isn't hidden in an emulation that runs for the other controllers; None when it is or wasn't tried.</summary>
        public PanReason PuckRejection { get; set; }

        /// <summary>When the puck Arcadia saw was hidden, until Arcadia sees it again (#396); null once it did, or it wasn't seen.</summary>
        public long? AwaitingSightSince { get; set; }

        public ISteamControllerCopy? Copy { get; set; }

        /// <summary>The device the copy follows the controller to (#345), and since when; null when it isn't following.</summary>
        public (SteamControllerConnection To, long Since)? Following { get; set; }

        public bool Ended { get; private set; }

        /// <summary>A pad that failed to go: it may still be there, so only Pan's teardown may show the puck.</summary>
        public bool LeftBehind { get; set; }

        public bool Add(GuardedDevice pad)
        {
            lock (_pads)
            {
                if (Ended)
                {
                    return false;
                }

                _pads.Add(pad);
                return true;
            }
        }

        public void Remove(GuardedDevice pad)
        {
            lock (_pads) _pads.Remove(pad);
        }

        public List<GuardedDevice> End()
        {
            lock (_pads)
            {
                Ended = true;
                var pads = _pads.ToList();
                _pads.Clear();
                return pads;
            }
        }
    }

    /// <summary>Arcadia's Xbox pad (the Steam Controller's) within an emulation, see <see cref="GuardedDevice"/>.</summary>
    private sealed class GuardedXboxPad(IVirtualXboxPad pad, Session session, IEmulationGuardian guardian)
        : GuardedDevice(pad, session, guardian), IVirtualXboxPad
    {
        public bool IsConnected => IsCurrent && pad.IsConnected;

        public event Action<XboxRumble>? Rumbled
        {
            add => pad.Rumbled += value;
            remove => pad.Rumbled -= value;
        }

        /// <summary>A pad the emulation took out takes nothing more (its VIIPER device is gone).</summary>
        public Task SendAsync(XboxPadState state, CancellationToken cancellationToken) =>
            Disposed ? Task.CompletedTask : pad.SendAsync(state, cancellationToken);
    }

    /// <summary>The pad of another controller (#238) within an emulation, see <see cref="GuardedDevice"/>.</summary>
    private sealed class GuardedKindPad(IVirtualPad pad, Session session, IEmulationGuardian guardian)
        : GuardedDevice(pad, session, guardian), IVirtualPad
    {
        public PadKind Kind => pad.Kind;

        public bool IsConnected => IsCurrent && pad.IsConnected;

        public event Action<PadFeedback>? Fed
        {
            add => pad.Fed += value;
            remove => pad.Fed -= value;
        }

        public Task SendAsync(VirtualPadState state, CancellationToken cancellationToken) =>
            Disposed ? Task.CompletedTask : pad.SendAsync(state, cancellationToken);
    }

    /// <summary>
    /// A pad within an emulation: gone when the emulation ends or Pan tore it down (no VIIPER failure then), and
    /// unplugged only once, whoever is first.
    /// </summary>
    private abstract class GuardedDevice(IAsyncDisposable pad, Session session, IEmulationGuardian guardian) : IAsyncDisposable
    {
        private readonly Lock _lock = new();
        private int _disposed;
        private Task? _removal;

        protected bool IsCurrent => !session.Ended && guardian.IsCurrent(session.Generation);

        protected bool Disposed => Volatile.Read(ref _disposed) == 1;

        public bool TakenAway => session.Ended || !guardian.IsCurrent(session.Generation);

        /// <summary>
        /// Unplugged once; a second caller (the emulation's end) awaits the same removal, so the puck is never shown while
        /// the pad may still forward to it. The session lists the pad until the removal is done.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                Volatile.Write(ref _disposed, 1);
                _removal ??= RemoveAsync();
                return new ValueTask(_removal);
            }
        }

        private async Task RemoveAsync()
        {
            await Task.Yield();
            try
            {
                await pad.DisposeAsync();
            }
            catch
            {
                session.LeftBehind = true;
                throw;
            }
            finally
            {
                session.Remove(this);
            }
        }
    }
}
