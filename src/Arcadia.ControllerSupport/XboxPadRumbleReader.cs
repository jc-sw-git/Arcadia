using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Reads VIIPER's output of a virtual Xbox 360 pad, the two motors (one byte each, large then small). It is
/// <c>ViiperDevice.OnOutput</c>, which Viiper.Client's read loop calls again and again until it is cleared.
/// </summary>
/// <param name="ended">The stream ended or failed: the pad is gone, and the caller clears <c>OnOutput</c>.</param>
/// <param name="reading">Cancelled when the pad is unplugged; that ends the read quietly, not as <paramref name="ended"/>.</param>
internal sealed class XboxPadRumbleReader(Action<XboxRumble> rumbled, Action ended, CancellationToken reading)
{
    public async Task ReadAsync(Stream output)
    {
        var motors = new byte[2];
        try
        {
            await output.ReadExactlyAsync(motors, reading);
        }
        catch (OperationCanceledException) when (reading.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // VIIPER closed the stream (it ends with the emulation, e.g. at "Controller wiederherstellen"). Viiper.Client's
            // loop would catch this too, but an exception leaving Arcadia's code into it stops a debugger with "Just My
            // Code" and freezes every thread, the puck's handles included (#470).
            ended();
            return;
        }

        rumbled(new XboxRumble(motors[0], motors[1]));
    }
}
