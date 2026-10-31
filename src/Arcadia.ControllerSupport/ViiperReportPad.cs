using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// A DualShock 4, DualSense (Edge) or Switch 2 Pro pad on VIIPER's raw stream (#238 part 2): input packets out, the
/// game's rumble and light bar in (<see cref="VirtualPadReports"/>).
/// </summary>
internal sealed class ViiperReportPad : IVirtualPad
{
    private readonly ViiperRawDevice _device;
    private readonly CancellationTokenSource _reading = new();
    private readonly Task _reader;
    private volatile bool _broken;

    public ViiperReportPad(PadKind kind, ViiperRawDevice device)
    {
        Kind = kind;
        _device = device;
        _reader = Task.Run(ReadFeedbackAsync, CancellationToken.None);
    }

    public PadKind Kind { get; }

    public bool IsConnected => !_broken && !_device.ServerEnded;

    public bool TakenAway => false;

    public event Action<PadFeedback>? Fed;

    public async Task SendAsync(VirtualPadState state, CancellationToken cancellationToken)
    {
        try
        {
            await _device.Stream.WriteAsync(VirtualPadReports.Input(Kind, state), cancellationToken);
        }
        catch (Exception)
        {
            // Failed or timed out (the caller's token): either way the pad no longer follows the controller.
            _broken = true;
            throw;
        }
    }

    /// <summary>Reads VIIPER's feedback packets until the pad is unplugged or its stream ends. Never throws.</summary>
    private async Task ReadFeedbackAsync()
    {
        var packet = new byte[VirtualPadReports.FeedbackSize(Kind)];
        try
        {
            while (true)
            {
                await _device.Stream.ReadExactlyAsync(packet, _reading.Token);
                Fed?.Invoke(VirtualPadReports.Feedback(Kind, packet));
            }
        }
        catch (OperationCanceledException) when (_reading.IsCancellationRequested)
        {
            // Unplugged.
        }
        catch (Exception)
        {
            // The stream ended or broke: VIIPER or the device is gone, the next tick plugs a new pad.
            _broken = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Fed = null;
        await _reading.CancelAsync();
        try
        {
            await _device.DisposeAsync();
        }
        catch (Exception) when (_broken)
        {
            // The device is gone already.
        }
        finally
        {
            await _reader;
            _reading.Dispose();
        }
    }
}
