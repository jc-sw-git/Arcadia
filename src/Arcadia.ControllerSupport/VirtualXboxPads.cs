using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>Plugs virtual Xbox 360 controllers in, which every program sees like a real one (#213).</summary>
public interface IVirtualXboxPads
{
    /// <summary>Why no pad can be plugged in (e.g. VIIPER or the usbip driver missing), or null when one can.</summary>
    string? Unavailable();

    /// <summary>Plugs a new pad in; throws when that fails. The pad goes when it is disposed.</summary>
    Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken);

    /// <summary>Arcadia exits: whatever is left (VIIPER's server) goes too. Never throws.</summary>
    Task ShutdownAsync();
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
}
