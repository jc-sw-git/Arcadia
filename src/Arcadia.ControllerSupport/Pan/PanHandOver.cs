using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// Arcadia's end of the hand-over pipe of a Pan it started elevated and that waits for its one message after the
/// Windows prompt: a driver install's installers (#413, <see cref="PanInstallRun"/>) or a restore's <c>go</c> (#471).
/// Disposed before <see cref="SendAsync"/>, Pan is told <c>abort</c> and ends without changing anything, so every
/// failure, exception or cancellation between prompt and hand-over ends Pan, not only expected ones.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class PanHandOver : IAsyncDisposable
{
    /// <summary>How long Arcadia waits for Pan's end after <c>abort</c> before it goes on.</summary>
    private static readonly TimeSpan AbortWait = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly NamedPipeClientStream _pipe;
    private readonly string _command;
    private bool _handedOver;

    private PanHandOver(Process process, NamedPipeClientStream pipe, string command)
    {
        _process = process;
        _pipe = pipe;
        _command = command;
        Exited = process.WaitForExitAsync();
    }

    /// <summary>Completes when Pan has ended, also before the hand-over (exit 3, 4, 8, 9).</summary>
    public Task Exited { get; }

    public int ExitCode => _process.ExitCode;

    /// <summary>
    /// Connects to the pipe <paramref name="pipeName"/> of the Pan just started as <paramref name="command"/>; throws when
    /// Pan ended first (its exit code says why), didn't answer, or the pipe belongs to another process.
    /// <paramref name="what"/> names the step in Arcadia's message ("die Installation").
    /// </summary>
    public static async Task<PanHandOver> ConnectAsync(Process process, string pipeName, string command, string what, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(DriverInstallation.PanConnectTimeout);
            var connect = pipe.ConnectAsync(limit.Token);
            var exited = process.WaitForExitAsync(limit.Token);
            if (await Task.WhenAny(connect, exited) != connect || !connect.IsCompletedSuccessfully)
            {
                await limit.CancelAsync();
                await Ignore(connect);
                if (process.HasExited)
                {
                    throw PanExitCodes.ErrorFor(process.ExitCode, command);
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException(ControllerSupportTexts.PanUnreachableAborted(seconds: DriverInstallation.PanConnectTimeout.TotalSeconds, what: what));
            }

            // Only the Pan Arcadia started may take the message: a pipe of another process is left at once.
            if (!PanPipeTransport.GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var server) || server != process.Id)
            {
                throw new InvalidOperationException(ControllerSupportTexts.PanHandOverNotOurs(server: server, started: process.Id, what: what));
            }

            return new PanHandOver(process, pipe, command);
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }
    }

    /// <summary>Sends the one message and closes the pipe; from then on Pan no longer needs Arcadia.</summary>
    public async Task SendAsync(byte[] message, CancellationToken cancellationToken)
    {
        await _pipe.WriteAsync(message, cancellationToken);
        await _pipe.FlushAsync(cancellationToken);
        _handedOver = true;
        await WaitUntilPanReadAsync(cancellationToken);
        await _pipe.DisposeAsync();
    }

    /// <summary>
    /// Closing the pipe before Pan has taken the client's connection loses the message: Pan's wait for the connection
    /// then fails as if Arcadia were gone (exit 9). So the pipe stays open until Pan has read it all. Pan reads at once;
    /// should it not within the limit, the pipe closes anyway and Pan's exit code tells what happened.
    /// </summary>
    private async Task WaitUntilPanReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(_pipe.WaitForPipeDrain, CancellationToken.None).WaitAsync(DriverInstallation.PanConnectTimeout, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            // IOException: Pan has read the message and closed its end already.
        }
    }

    /// <summary>Waits for Pan's end; returns on exit 0, else throws today's message for its exit code (<see cref="PanExitCodes.ErrorFor"/>).</summary>
    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await Exited.WaitAsync(cancellationToken);
        if (_process.ExitCode != 0)
        {
            throw PanExitCodes.ErrorFor(_process.ExitCode, _command);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_handedOver && !_process.HasExited)
        {
            try
            {
                await _pipe.WriteAsync(InstallHandOver.Abort());
                await _pipe.FlushAsync();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The pipe broke: Pan ends at once on that too.
            }

            await _pipe.DisposeAsync();
            await Ignore(Exited.WaitAsync(AbortWait));
        }

        await _pipe.DisposeAsync();
        _process.Dispose();
    }

    private static async Task Ignore(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException)
        {
        }
    }
}
