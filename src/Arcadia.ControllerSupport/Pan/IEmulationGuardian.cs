namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// What Arcadia needs from Pan, its guardian service (#243): every emulation is registered with a generation, the
/// feeding thread's pulse becomes a heartbeat, and a teardown Pan did on its own (Arcadia hung) comes back as an
/// event that Arcadia acknowledges by dropping its devices. Without a guardian there is no emulation (owner,
/// 2026-10-26).
/// </summary>
public interface IEmulationGuardian
{
    /// <summary>Why no emulation can start right now (Pan not set up, not answering, wrong protocol), or null.</summary>
    string? Unavailable();

    /// <summary>
    /// Registers an emulation served by the VIIPER on <paramref name="usbipPort"/> (#304); the generation Pan gave it.
    /// Throws when Pan refuses or is gone.
    /// </summary>
    Task<uint> EmulationStartedAsync(int usbipPort, CancellationToken cancellationToken);

    /// <summary>Arcadia removed its emulation itself. Never throws.</summary>
    Task EmulationStoppedAsync(uint generation, CancellationToken cancellationToken);

    /// <summary>Arcadia dropped its devices after Pan's teardown; only then does Pan accept a new emulation. Never throws.</summary>
    Task AcknowledgeTeardownAsync(uint generation, CancellationToken cancellationToken);

    /// <summary>True while the emulation of this generation stands: Pan connected and no teardown since.</summary>
    bool IsCurrent(uint generation);

    /// <summary>True if Pan tore this generation down (as opposed to the connection being lost).</summary>
    bool WasTornDown(uint generation);

    /// <summary>The thread that feeds the emulated controller is alive; called on every poll, must not block.</summary>
    void Pulse();

    /// <summary>Arcadia exits on purpose after cleaning up. Never throws.</summary>
    Task PlannedExitAsync();

    /// <summary>
    /// Hides the real Steam Controller from every program but Arcadia, within this generation's emulation (#242).
    /// <see cref="Arcadia.Pan.PanReason.None"/> when it is hidden, else why not; after a rejected hide it has asked Pan to
    /// undo whatever the hide did already. Never throws but for cancellation before it is sent.
    /// </summary>
    Task<Arcadia.Pan.PanReason> HideAsync(uint generation, CancellationToken cancellationToken);

    /// <summary>
    /// Shows the real Steam Controller again; only once nothing forwards to it (the copy is gone). False if Pan didn't:
    /// its teardown after <see cref="EmulationStoppedAsync"/> does it then. Never throws but for cancellation.
    /// </summary>
    Task<bool> UnhideAsync(uint generation, CancellationToken cancellationToken);

    /// <summary>
    /// Hides every connected controller of one model from every program but Arcadia, within this generation's emulation
    /// (#335); Arcadia has let go of the model first, as Pan restarts it. Never throws but for cancellation before it is sent.
    /// </summary>
    Task<ControllerHidingResult> HideControllerAsync(uint generation, Arcadia.Pan.ControllerModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Whether Pan could hide now and, while it hides for <paramref name="generation"/>, whether that hiding still holds
    /// (<see cref="Arcadia.Pan.PanReason.HidingLost"/>); null while Pan isn't connected. Never throws but for cancellation.
    /// </summary>
    Task<Arcadia.Pan.PanReason?> CheckHidingAsync(uint generation, CancellationToken cancellationToken);

    /// <summary>
    /// Steam tries a firmware update through the copy (#242): while held, Pan's teardown keeps the real Steam Controller
    /// hidden until Steam no longer runs, also after a crash of Arcadia. False if Pan didn't take it. Never throws.
    /// </summary>
    Task<bool> HoldForFirmwareAsync(uint generation, bool hold, CancellationToken cancellationToken);

    /// <summary>
    /// Pan said at the connection that a firmware attempt held the real Steam Controller hidden when the last Arcadia
    /// ended, and Steam still runs: Arcadia takes it over without asking and waits the attempt out.
    /// </summary>
    bool HeldForFirmware { get; }

    /// <summary>Pan tore an emulation down: the generation and why. Off the UI thread.</summary>
    event Action<uint, Arcadia.Pan.PanReason>? TornDown;

    /// <summary>Pan broke off or stopped answering while an emulation ran: the reason, for a toast. Off the UI thread.</summary>
    event Action<string>? Lost;

    /// <summary>
    /// Pan is about to restart the real puck on its own (a teardown's show, its retry, a device arrival while hiding):
    /// handlers let go of it before they return, as Windows refuses the restart while Arcadia holds it (#308); then Pan is
    /// told. Followed by <see cref="PuckRestarted"/>. Off the UI thread.
    /// </summary>
    event Action? PuckRestarting;

    /// <summary>Pan's own restart is over (done or not), or Pan was lost meanwhile: read the puck again. Off the UI thread.</summary>
    event Action? PuckRestarted;
}

/// <summary>
/// Asks Pan whether it could hide the Steam Controller now (#243 part B2), so the settings can say why not: HidHide's
/// driver not running, inverse mode, cloaking off with the user's own entries, or its configuration client open.
/// </summary>
public interface IHidingCheck
{
    /// <summary>
    /// Pan's answer (<see cref="Arcadia.Pan.PanReason.None"/>: it could hide), or null while Pan isn't connected or
    /// can't hide at all (a Pan before part B). Never throws but for cancellation.
    /// </summary>
    Task<Arcadia.Pan.PanReason?> CheckHidingAsync(CancellationToken cancellationToken);
}
