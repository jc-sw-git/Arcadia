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

    /// <summary>Registers an emulation; the generation Pan gave it. Throws when Pan refuses or is gone.</summary>
    Task<uint> EmulationStartedAsync(CancellationToken cancellationToken);

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

    /// <summary>Pan tore an emulation down: the generation and why. Off the UI thread.</summary>
    event Action<uint, Arcadia.Pan.PanReason>? TornDown;

    /// <summary>Pan broke off or stopped answering while an emulation ran: the reason, for a toast. Off the UI thread.</summary>
    event Action<string>? Lost;
}
