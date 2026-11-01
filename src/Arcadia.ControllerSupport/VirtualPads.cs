using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>One emulated pad of any kind (#238 part 2); disposing it unplugs it.</summary>
public interface IVirtualPad : IAsyncDisposable
{
    PadKind Kind { get; }

    /// <summary>False once the pad is gone without being unplugged (VIIPER ended, its connection broke).</summary>
    bool IsConnected { get; }

    /// <summary>The pad is gone because Pan took it away on purpose: no failure of VIIPER's.</summary>
    bool TakenAway { get; }

    /// <summary>What the pad reports from now on; throws when the pad is gone.</summary>
    Task SendAsync(VirtualPadState state, CancellationToken cancellationToken);

    /// <summary>A game set the pad's motors or light bar (#238 D5, A5); on VIIPER's thread, the handler must not block.</summary>
    event Action<PadFeedback>? Fed;
}

/// <summary>What a game sent an emulated pad; null where this report leaves it as it was.</summary>
public readonly record struct PadFeedback(XboxRumble? Rumble, PadLight? Light);

/// <summary>An Xbox 360 pad as a pad of any kind: takes what an Xbox 360 pad has, passes its rumble back.</summary>
public sealed class XboxPadAsVirtualPad : IVirtualPad
{
    private readonly IVirtualXboxPad _pad;

    public XboxPadAsVirtualPad(IVirtualXboxPad pad)
    {
        _pad = pad;
        _pad.Rumbled += motors => Fed?.Invoke(new PadFeedback(motors, null));
    }

    public PadKind Kind => PadKind.Xbox360;

    public bool IsConnected => _pad.IsConnected;

    public bool TakenAway => _pad.TakenAway;

    public event Action<PadFeedback>? Fed;

    public Task SendAsync(VirtualPadState state, CancellationToken cancellationToken) =>
        _pad.SendAsync(XboxPadState.From(state), cancellationToken);

    public ValueTask DisposeAsync() => _pad.DisposeAsync();
}
