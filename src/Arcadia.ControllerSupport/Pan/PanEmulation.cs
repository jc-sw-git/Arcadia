using Arcadia.ControllerSupport.Copy;
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

    /// <summary>On, but Pan can't hide now or the copy didn't come up; it tries again by itself.</summary>
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

    /// <summary>Steam runs and the real puck was visible: the player is asked whether a firmware update runs (<see cref="SteamControllerTakeover"/>).</summary>
    AwaitingConsent,

    /// <summary>The player said no, or released them for a firmware update: Steam Controller and puck are left alone.</summary>
    Excluded,

    /// <summary>Too many failures in <see cref="PanEmulation.FailureWindow"/>.</summary>
    TooManyFailures,
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

    private readonly IVirtualXboxPads _pads;
    private readonly IEmulationGuardian _guardian;
    private readonly ISteamControllerHold _hold;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<long> _failures = [];

    private volatile Session? _session;
    private volatile PanEmulationStatus _status = PanEmulationStatus.Off;
    // Monotonic timestamps; written by "Erneut versuchen" on the UI thread too, so read and written as one long.
    private long _hideRetryAt = long.MinValue;
    private bool _pausedByLoss;
    private volatile bool _gaveUp;

    // Steam tries a firmware update through the copy (#242): the puck must stay hidden, also if Arcadia ends meanwhile.
    private volatile bool _firmwareHold;

    // The hold from before the start has been noted (FirmwareHoldFound raised) since Pan last reported it.
    private bool _holdFromBeforeNoted;
    private string? _lastLogged;

    public PanEmulation(IVirtualXboxPads pads, IEmulationGuardian guardian, ISteamControllerHold hold, TimeProvider time, Action<string> log)
    {
        _pads = pads;
        _guardian = guardian;
        _hold = hold;
        _time = time;
        _log = log;
        Takeover = new SteamControllerTakeover(log, time);
    }

    /// <summary>The switch "Controller-Wächter (Pan)"; read on every tick.</summary>
    public Func<bool> Enabled { get; init; } = () => true;

    /// <summary>Steam runs (its client process); asked before every hide.</summary>
    public Func<bool> SteamRuns { get; init; } = () => false;

    /// <summary>What of the Steam Controller is connected now, for the question.</summary>
    public Func<SteamControllerParts> Connected { get; init; } = () => SteamControllerParts.Puck;

    /// <summary>Whether Arcadia may hide the real puck: never in an unknown state while Steam runs.</summary>
    public SteamControllerTakeover Takeover { get; }

    /// <summary>Attaching the copy waits this long at most: a hanging attach would hold the gate and stop every tick.</summary>
    public static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Pan can run on this system at all (Windows).</summary>
    public bool Supported { get; init; } = true;

    /// <summary>
    /// A Steam Controller is around (its puck, or SDL sees one): only then is there something to hide. Read on every tick
    /// while no emulation runs; once it runs, it stays until the end (a replug while hidden needs nothing).
    /// </summary>
    public Func<bool> SteamControllerPresent { get; init; } = () => true;

    /// <summary>The <c>Game.Key</c> of the library game in front, or null; asked every tick (a short gap mid-game).</summary>
    public Func<string?> GameInFront { get; init; } = () => null;

    public PanEmulationStatus Status => _status;

    /// <summary>The puck is hidden for this emulation (only when this build has the copy).</summary>
    public bool Hides => _hold.CanCopy;

    /// <summary><see cref="Status"/> changed; any thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Pan still holds the puck hidden for a firmware attempt from before Arcadia's start (it ended unexpectedly while
    /// Steam tried): the dialog waits it out. Raised once, by the first tick that sees it; off the UI thread.
    /// </summary>
    public event Action? FirmwareHoldFound;

    /// <summary>A notice for a toast or the in-game notice: hiding lost, back again, given up. Any thread.</summary>
    public event Action<string>? Notice;

    /// <summary>Every 2 s, off the UI thread: starts, checks and ends the emulation as needed. Never throws.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            await StepAsync(cancellationToken);
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
        _log("Pan-Emulation: bisherige Ausfälle zählen nicht mehr");
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
        var guarded = new GuardedPad(pad, session, _guardian);
        if (!session.Add(guarded))
        {
            // The emulation ended while the pad was plugged in.
            await pad.DisposeAsync();
            throw new InvalidOperationException("Pan-Emulation wurde beendet");
        }

        return guarded;
    }

    /// <summary>Arcadia exits (also for an update): pad and copy go, the puck is shown, VIIPER ends, Pan hears the planned exit.</summary>
    public async Task ShutdownAsync()
    {
        await _gate.WaitAsync();
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

    private async Task StepAsync(CancellationToken cancellationToken)
    {
        if (!Supported)
        {
            SetStatus(new(PanEmulationState.Unsupported));
            return;
        }

        NoteHoldFromBeforeStart();
        Takeover.InFront(GameInFront());

        if (!Enabled())
        {
            Takeover.Gap("ausgeschaltet", switchedOff: true);
            await EndIfRunningAsync("ausgeschaltet");
            SetStatus(PanEmulationStatus.Off);
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

        if (!SteamControllerPresent())
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
        await StartOrPauseAsync(cancellationToken);
        if (Hides && _session is not { Hidden: true })
        {
            // Not hidden after all (Pan can't hide, the hide or the copy failed): Steam sees the real puck again.
            Takeover.Gap("nicht versteckt");
        }
    }

    private async Task StartOrPauseAsync(CancellationToken cancellationToken)
    {
        // Whether Pan can hide comes before the question: a question the hide can't follow at once would come back every
        // tick and teach the player to hold "Ja" by reflex (review of 68a163f, point 5).
        if (Hides && Takeover.State is TakeoverState.Unknown or TakeoverState.Granted
            && await _guardian.CheckHidingAsync(0, cancellationToken) is var could && could != PanReason.None)
        {
            // Pan can't hide now (HidHide busy, inverse, ...): no emulation at all, the real controller stays as it is.
            SetStatus(could is null
                ? new(PanEmulationState.Unavailable, Reason: "Pan kann den Steam Controller nicht verstecken – Pan aktualisieren")
                : new(PanEmulationState.Paused, PanEmulationProblem.Hiding, could.Value, $"HidHide: {could}"));
            return;
        }

        if (Hides && !MayHide())
        {
            return;
        }

        SetStatus(new(PanEmulationState.SettingUp));
        uint generation;
        try
        {
            // VIIPER first: Pan takes the client's viiper.exe at the registration, to end it if Arcadia hangs.
            await _pads.PrepareAsync(cancellationToken);
            generation = await _guardian.EmulationStartedAsync(cancellationToken);
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

        var session = new Session(generation);
        if (Hides)
        {
            if (!MayHide())
            {
                // The consent went while VIIPER started or Pan registered (switched off, released): nothing is hidden
                // yet, so undo the registration only.
                await _guardian.EmulationStoppedAsync(generation, CancellationToken.None);
                return;
            }

            var hidden = await _guardian.HideAsync(generation, cancellationToken);
            if (hidden != PanReason.None)
            {
                // The client already asked Pan to undo the rejected hide. Counted: a hide that keeps failing restarts the
                // puck each time (the hide and its undo), so it must not go on forever.
                await _guardian.EmulationStoppedAsync(generation, CancellationToken.None);
                Interlocked.Exchange(ref _hideRetryAt, _time.GetTimestamp());
                Failed(new(PanEmulationState.Paused, PanEmulationProblem.Hiding, hidden, $"nicht versteckt: {hidden}"));
                return;
            }

            session.Hidden = true;
            if (_firmwareHold)
            {
                session.HoldSent = await _guardian.HoldForFirmwareAsync(generation, true, CancellationToken.None);
            }
        }

        _session = session;
        _log($"Pan-Emulation {generation} läuft{(Hides ? ", Steam Controller versteckt" : " (ohne Kopie für Steam, der Steam Controller bleibt sichtbar)")}");
        await KeepAsync(session, cancellationToken, mayRestart: false);
    }

    /// <summary>
    /// Asked before every hide and again right before it is sent, as a hide restarts the puck and would break off a
    /// firmware update Steam runs on it (<see cref="SteamControllerTakeover"/>).
    /// </summary>
    private bool MayHide()
    {
        if (_firmwareHold)
        {
            Takeover.KeepForFirmware();
        }

        if (Takeover.MayHide(SteamRuns(), Connected()))
        {
            return true;
        }

        SetStatus(Takeover.State is TakeoverState.Declined or TakeoverState.Released
            ? new(PanEmulationState.Ready, PanEmulationProblem.Excluded, Reason: "Steam Controller und Puck ausgenommen")
            : new(PanEmulationState.SettingUp, PanEmulationProblem.AwaitingConsent, Reason: "wartet auf die Antwort, ob ein Firmware-Update läuft"));
        return false;
    }

    /// <summary>
    /// Pan still holds the puck hidden from before Arcadia's start: the dialog waits it out, whether Pan is switched on
    /// in Arcadia or not. Once per hold, so a lift Pan didn't take doesn't bring the dialog back each tick.
    /// </summary>
    private void NoteHoldFromBeforeStart()
    {
        if (!_guardian.HeldForFirmware)
        {
            _holdFromBeforeNoted = false;
            return;
        }

        if (_holdFromBeforeNoted)
        {
            return;
        }

        _holdFromBeforeNoted = true;
        _firmwareHold = true;
        _log("Pan-Emulation: ein Firmware-Versuch von vor dem Start hält den Steam Controller versteckt; Arcadia wartet ihn ab");
        FirmwareHoldFound?.Invoke();
    }

    /// <summary>
    /// "Controller freigeben" after Steam tried a firmware update through the copy: pad and copy go, the puck is shown,
    /// and nothing is hidden again until the settings row takes them over.
    /// </summary>
    public async Task ReleaseForFirmwareAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await SetFirmwareHoldAsync(false);
            Takeover.Release();
            await EndIfRunningAsync("für ein Firmware-Update freigegeben");
            MayHide();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Steam tries a firmware update through the copy, or the attempt is over (the dialog's end): Pan keeps the puck
    /// hidden meanwhile, also if Arcadia ends unexpectedly. The wish is set at once, on the caller's thread, so the last
    /// call wins and a quit right after the end already sees it; Pan hears of it on the thread pool. Never throws.
    /// </summary>
    public Task HoldForFirmwareAsync(bool hold)
    {
        _firmwareHold = hold;
        return Task.Run(() => SendFirmwareHoldAsync(hold));
    }

    private async Task SendFirmwareHoldAsync(bool hold)
    {
        await _gate.WaitAsync();
        try
        {
            if (_firmwareHold == hold)
            {
                await SetFirmwareHoldAsync(hold);
            }
        }
        catch (Exception ex)
        {
            _log($"Pan-Emulation: Firmware-Halt nicht gesetzt: {ex}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Inside the gate.</summary>
    private async Task SetFirmwareHoldAsync(bool hold)
    {
        _firmwareHold = hold;
        if (_session is { Hidden: true } session)
        {
            // Only a hold Pan has is lifted: a release without an attempt sends nothing.
            if (session.HoldSent != hold && await _guardian.HoldForFirmwareAsync(session.Generation, hold, CancellationToken.None))
            {
                session.HoldSent = hold;
            }
        }
        else if (!hold && _guardian.HeldForFirmware)
        {
            // A hold from before the start, and no hidden emulation to lift it with: Pan takes the lift without one.
            await _guardian.HoldForFirmwareAsync(_session?.Generation ?? 0, false, CancellationToken.None);
        }
    }

    /// <summary>The settings row "Steam Controller und Puck übernehmen": hidden at the next tick.</summary>
    public void TakeOverAgain() => Takeover.TakeOverAgain();

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
            if (mayRestart && _guardian.Unavailable() is null && SteamControllerPresent())
            {
                await StartAsync(cancellationToken);
            }

            return;
        }

        if (_firmwareHold && session.Hidden && !session.HoldSent)
        {
            // Pan didn't take the hold (journal not writable, link busy): a crash now must not show the puck mid-attempt.
            session.HoldSent = await _guardian.HoldForFirmwareAsync(session.Generation, true, CancellationToken.None);
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

        if (Hides && !await KeepCopyAsync(session, cancellationToken))
        {
            return;
        }

        SetStatus(new(PanEmulationState.Running));
        if (_pausedByLoss)
        {
            _pausedByLoss = false;
            Notice?.Invoke(RunningAgainText);
        }
    }

    /// <summary>False when the copy failed and the emulation ended with it.</summary>
    private async Task<bool> KeepCopyAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.Copy is { IsConnected: false } lost)
        {
            session.Copy = null;
            await DisposeQuietlyAsync(lost, "Kopie entfernen");
            if (_hold.PuckPresent)
            {
                _log("Pan-Emulation: Kopie für Steam verloren");
                NoteFailure();
            }
        }

        if (session.Copy is not null || !_hold.PuckPresent)
        {
            return true;
        }

        try
        {
            using var timeout = new CancellationTokenSource(AttachTimeout, _time);
            using var attach = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                session.Copy = await _hold.AttachAsync(attach.Token);
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
        Takeover.Gap(why);
        var removed = await RemoveDevicesAsync(session) && !session.LeftBehind;
        if (session.HoldSent && !_firmwareHold)
        {
            // The attempt is over (Steam gave up, "Arcadia beenden"), but the lift may not have reached Pan yet.
            session.HoldSent = !await _guardian.HoldForFirmwareAsync(session.Generation, false, CancellationToken.None);
        }

        if (session.Hidden && _firmwareHold)
        {
            // Mid firmware attempt (Arcadia closing, a gap): the copy goes, the puck stays hidden; Pan's teardown after the
            // stop keeps it so until Steam no longer runs.
            _log($"Pan-Emulation {session.Generation}: Firmware-Versuch läuft, der Steam Controller bleibt versteckt");
        }
        else if (session.Hidden && removed)
        {
            // Not shown (HidHide busy): Pan's teardown after the stop shows it, with its own retries.
            await _guardian.UnhideAsync(session.Generation, CancellationToken.None);
        }
        else if (session.Hidden)
        {
            // A device that failed to go may still forward to the puck: Pan's teardown after the stop ends what is left
            // of the forwarding (viiper.exe) before it shows the puck.
            _log($"Pan-Emulation {session.Generation}: nicht alles entfernt, Pan zeigt den Steam Controller nach seinem Aufräumen");
        }

        await _guardian.EmulationStoppedAsync(session.Generation, CancellationToken.None);
    }

    /// <summary>Pan has undone the emulation itself: the devices go, and a teardown is acknowledged.</summary>
    private async Task DropAsync(Session session)
    {
        _log($"Pan-Emulation {session.Generation}: von Pan beendet");
        _session = null;
        Takeover.Gap("von Pan beendet");
        await RemoveDevicesAsync(session);
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
    private sealed class Session(uint generation)
    {
        private readonly List<GuardedPad> _pads = [];

        public uint Generation { get; } = generation;

        public bool Hidden { get; set; }

        /// <summary>Pan holds the puck hidden for a firmware attempt in this generation.</summary>
        public bool HoldSent { get; set; }

        public ISteamControllerCopy? Copy { get; set; }

        public bool Ended { get; private set; }

        /// <summary>A pad that failed to go: it may still be there, so only Pan's teardown may show the puck.</summary>
        public bool LeftBehind { get; set; }

        public bool Add(GuardedPad pad)
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

        public void Remove(GuardedPad pad)
        {
            lock (_pads) _pads.Remove(pad);
        }

        public List<GuardedPad> End()
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

    /// <summary>
    /// Arcadia's Xbox pad within an emulation: gone when the emulation ends or Pan tore it down (no VIIPER failure then),
    /// and unplugged only once, whoever is first.
    /// </summary>
    private sealed class GuardedPad(IVirtualXboxPad pad, Session session, IEmulationGuardian guardian) : IVirtualXboxPad
    {
        private readonly Lock _lock = new();
        private int _disposed;
        private Task? _removal;

        public bool IsConnected => !session.Ended && pad.IsConnected && guardian.IsCurrent(session.Generation);

        public bool TakenAway => session.Ended || !guardian.IsCurrent(session.Generation);

        /// <summary>A pad the emulation took out takes nothing more (its VIIPER device is gone).</summary>
        public Task SendAsync(Arcadia.Input.XboxPadState state, CancellationToken cancellationToken) =>
            Volatile.Read(ref _disposed) == 1 ? Task.CompletedTask : pad.SendAsync(state, cancellationToken);

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
