using Arcadia.Input;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// Virtual pads under Pan's watch (#243): no pad without the guardian, every plug-in registered with Pan after the
/// pad exists (so Pan finds the forwarding process it must end), a pad that Pan tore down or whose Pan is gone counts
/// as lost (the next tick unplugs and plugs anew), and unplugging tells Pan or acknowledges its teardown.
/// </summary>
public sealed class GuardedXboxPads(IVirtualXboxPads pads, IEmulationGuardian guardian) : IVirtualXboxPads
{
    public string? Unavailable() => pads.Unavailable() ?? guardian.Unavailable();

    public async Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        var pad = await pads.PlugAsync(cancellationToken);
        uint generation;
        try
        {
            // After the pad: VIIPER runs now, so Pan can take it as the client's child.
            generation = await guardian.EmulationStartedAsync(cancellationToken);
        }
        catch
        {
            await pad.DisposeAsync();
            throw;
        }

        return new GuardedPad(pad, generation, guardian);
    }

    public async Task ShutdownAsync()
    {
        await pads.ShutdownAsync();
        await guardian.PlannedExitAsync();
    }

    private sealed class GuardedPad(IVirtualXboxPad pad, uint generation, IEmulationGuardian guardian) : IVirtualXboxPad
    {
        public bool IsConnected => pad.IsConnected && guardian.IsCurrent(generation);

        public bool TakenAway => !guardian.IsCurrent(generation);

        public Task SendAsync(XboxPadState state, CancellationToken cancellationToken) => pad.SendAsync(state, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            try
            {
                await pad.DisposeAsync();
            }
            finally
            {
                if (guardian.WasTornDown(generation))
                {
                    await guardian.AcknowledgeTeardownAsync(generation, CancellationToken.None);
                }
                else
                {
                    await guardian.EmulationStoppedAsync(generation, CancellationToken.None);
                }
            }
        }
    }
}
