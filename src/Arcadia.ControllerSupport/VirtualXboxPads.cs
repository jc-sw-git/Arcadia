using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Plugs virtual Xbox 360 controllers in, which every program sees like a real one (#213), and, where the server has
/// them, the other kinds of #238 (DualShock 4, DualSense, Switch 2 Pro).
/// </summary>
public interface IVirtualXboxPads
{
    /// <summary>Why no pad can be plugged in (e.g. VIIPER or the usbip driver missing), or null when one can.</summary>
    string? Unavailable();

    /// <summary>Plugs a new pad in; throws when that fails. The pad goes when it is disposed.</summary>
    Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken);

    /// <summary>Plugs a new pad of this kind in (#238 D3); throws when that fails. The pad goes when it is disposed.</summary>
    Task<IVirtualPad> PlugAsync(PadKind kind, CancellationToken cancellationToken);

    /// <summary>Arcadia exits: whatever is left (VIIPER's server) goes too. Never throws.</summary>
    Task ShutdownAsync();
}

/// <summary>
/// The pads' server itself (VIIPER), as Pan's emulation drives it (#304): started only at an emulation's start, and a
/// pad goes only onto the server that start got ready.
/// </summary>
public interface IVirtualXboxPadServer : IVirtualXboxPads
{
    /// <summary>
    /// Gets ready to plug pads in (VIIPER's server runs, started and checked if it didn't), so Pan can take the
    /// forwarding process at the emulation's registration (#242). Returns the server's usbip port, which Pan journals
    /// and its teardown detaches (#304); throws when that fails.
    /// </summary>
    Task<int> PrepareAsync(CancellationToken cancellationToken);

    /// <summary>The usbip port of the server that runs now, or null when none does; a new one belongs to a new emulation.</summary>
    int? ServerPort { get; }
}

/// <summary>One virtual Xbox 360 controller; disposing it unplugs it.</summary>
public interface IVirtualXboxPad : IAsyncDisposable
{
    /// <summary>False once the pad is gone without being unplugged (VIIPER ended, its connection broke).</summary>
    bool IsConnected { get; }

    /// <summary>The pad is gone because Pan took it away on purpose (its teardown, or Pan itself gone): no failure of VIIPER's.</summary>
    bool TakenAway => false;

    /// <summary>What the pad reports from now on; throws when the pad is gone.</summary>
    Task SendAsync(XboxPadState state, CancellationToken cancellationToken);

    /// <summary>A game set the pad's motors (#238 D5); on VIIPER's thread, the handler must not block.</summary>
    event Action<XboxRumble>? Rumbled;
}

/// <summary>
/// No VIIPER: this build has no pinned one (#304), or it isn't Windows. Never another program's VIIPER, nor the one an
/// older SISR left: without Arcadia's own build there is no pad.
/// </summary>
public sealed class NoVirtualXboxPads : IVirtualXboxPadServer
{
    public const string Reason = "Diese Arcadia-Version hat keine geprüfte VIIPER-Version";

    public int? ServerPort => null;

    public string? Unavailable() => OperatingSystem.IsWindows() ? Reason : "nur unter Windows";

    public Task<int> PrepareAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(Unavailable());

    public Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(Unavailable());

    public Task<IVirtualPad> PlugAsync(PadKind kind, CancellationToken cancellationToken) => throw new InvalidOperationException(Unavailable());

    public Task ShutdownAsync() => Task.CompletedTask;
}
