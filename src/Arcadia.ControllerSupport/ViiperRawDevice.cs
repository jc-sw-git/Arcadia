using Viiper.Client;

namespace Arcadia.ControllerSupport;

/// <summary>
/// A VIIPER device driven by its raw stream (#242: the copy of the puck). Disposing it closes the stream and removes
/// its bus, which removes the device; that throws when VIIPER still runs but didn't remove it.
/// </summary>
public sealed class ViiperRawDevice(ViiperClient client, uint bus, Stream stream, Func<bool> serverEnded) : IAsyncDisposable
{
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromSeconds(3);

    public Stream Stream { get; } = stream;

    /// <summary>VIIPER's server has ended, and the device with it.</summary>
    public bool ServerEnded => serverEnded();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Stream.DisposeAsync();
            using var timeout = new CancellationTokenSource(RemoveTimeout);
            await client.BusRemoveAsync(bus, timeout.Token);
        }
        catch (Exception) when (serverEnded())
        {
            // The server is gone, and the device with it.
        }
        finally
        {
            client.Dispose();
        }
    }
}
