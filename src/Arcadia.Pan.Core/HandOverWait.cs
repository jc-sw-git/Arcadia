namespace Arcadia.Pan;

/// <summary>Pan's side of a hand-over pipe for one run, as far as it is Windows' (Arcadia.Pan/Windows).</summary>
public interface IHandOverChannel
{
    /// <summary>Completes when Arcadia's process, the run's <c>--client</c>, has ended.</summary>
    Task ClientGone { get; }

    /// <summary>
    /// Waits for Arcadia's process to connect and reads its one message to the end of the connection: at most
    /// <paramref name="maxBytes"/> bytes plus one, so a longer one shows. Null when the connection broke without a byte.
    /// </summary>
    Task<byte[]?> ReceiveAsync(int maxBytes, CancellationToken cancellationToken);
}

/// <summary>
/// Elevated Pan's wait for Arcadia's one message after the Windows prompt, shared by the driver install
/// (<see cref="InstallHandOver"/>, #413) and the restore (<see cref="RestoreHandOver"/>, #471). Nothing on the machine
/// changes meanwhile; Arcadia's end, a broken pipe or the timeout end the wait without a message.
/// </summary>
public static class HandOverWait
{
    /// <summary>
    /// A message wins over Arcadia's end only when it was read in full first, so an <c>abort</c> right before Arcadia
    /// closes still reads as one. <paramref name="read"/> judges the message.
    /// </summary>
    public static async Task<HandOverResult> WaitAsync(IHandOverChannel channel, TimeSpan timeout, TimeProvider time,
        Func<byte[], HandOverResult> read, CancellationToken cancellationToken = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receive = channel.ReceiveAsync(InstallHandOver.MaxMessageBytes, stop.Token);
        var timer = Task.Delay(timeout, time, stop.Token);
        var first = await Task.WhenAny(receive, channel.ClientGone, timer);
        try
        {
            if (first == receive)
            {
                byte[]? message;
                try
                {
                    message = await receive;
                }
                // A broken or refused pipe brings no go, so Pan ends as without Arcadia: nothing changed (exit 9).
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return new(HandOverEnd.ArcadiaGone);
                }

                return message is null ? new(HandOverEnd.ArcadiaGone) : read(message);
            }

            return first == timer ? new(HandOverEnd.Timeout) : new(HandOverEnd.ArcadiaGone);
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(receive, timer).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
