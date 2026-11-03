using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>What one install's elevated Pan installs, as its command line announces it (#413).</summary>
public sealed record PanInstallPlan(bool Usbip, bool HidHide, bool HidHideRepair);

/// <summary>
/// Elevated Pan of one driver install, after the user confirmed the Windows prompt (#413, D7 option A): it waits for the
/// installers until <see cref="HandOverAsync"/>. Disposed before that, it is told <c>abort</c> and ends without changing
/// anything, so every failure, exception or cancellation between prompt and hand-over ends Pan, not only expected ones.
/// </summary>
public interface IPanInstallRun : IAsyncDisposable
{
    /// <summary>Where Arcadia downloads the installers; the hand-over names files only from here.</summary>
    string InstallersFolder { get; }

    /// <summary>Completes when Pan has ended, also before the hand-over (exit 4, 8, 9).</summary>
    Task Exited { get; }

    /// <summary>Pan's step report, or null while there is none of this run Arcadia can read (display only).</summary>
    IReadOnlyDictionary<PanInstallStep, PanStepState>? ReadReport();

    /// <summary>Sends <c>go</c> with the installers and closes the pipe; from then on Pan no longer needs Arcadia.</summary>
    Task HandOverAsync(HandedOverInstallers installers, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for Pan's end; returns on exit 0, else throws today's message for its exit code (<see cref="PanExitCodes.ErrorFor"/>).
    /// </summary>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Pan's exit code once <see cref="Exited"/> completed.</summary>
    int ExitCode { get; }
}

/// <summary>The real one: the bundled <c>Pan.exe</c> started elevated, its pipe, its report in Pan's data folder.</summary>
[SupportedOSPlatform("windows")]
internal sealed class PanInstallRun : IPanInstallRun
{
    /// <summary>How long Arcadia waits for Pan's end after <c>abort</c> before it goes on (Steam starts again).</summary>
    private static readonly TimeSpan AbortWait = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly NamedPipeClientStream _pipe;
    private readonly string _runId;
    private bool _handedOver;

    private PanInstallRun(Process process, NamedPipeClientStream pipe, InstallRun run)
    {
        _process = process;
        _pipe = pipe;
        _runId = run.Id;
        InstallersFolder = run.InstallersFolder;
        Exited = process.WaitForExitAsync();
    }

    public string InstallersFolder { get; }

    public Task Exited { get; }

    public int ExitCode => _process.ExitCode;

    /// <summary>Connects to the pipe of the Pan just started; null when Pan ended first (its exit code then says why).</summary>
    public static async Task<PanInstallRun> ConnectAsync(Process process, InstallRun run, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", InstallHandOver.PipeName(run.Id), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
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
                    throw PanExitCodes.ErrorFor(process.ExitCode, "--install");
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Pan war nach {DriverInstallation.PanConnectTimeout.TotalSeconds:0} Sekunden nicht erreichbar – die Installation wurde abgebrochen, es hat sich nichts geändert.");
            }

            // Only the Pan Arcadia started may take the installers: a pipe of another process is left at once.
            if (!PanPipeTransport.GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var server) || server != process.Id)
            {
                throw new InvalidOperationException($"Die Übergabe an Pan gehört nicht dem gestarteten Pan (Prozess {server} statt {process.Id}) – die Installation wurde abgebrochen.");
            }

            return new PanInstallRun(process, pipe, run);
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }
    }

    public IReadOnlyDictionary<PanInstallStep, PanStepState>? ReadReport()
    {
        try
        {
            var path = InstallReport.PathOf(PanPaths.DataDirectory, _runId);
            return File.Exists(path) ? InstallReport.Parse(_runId, File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task HandOverAsync(HandedOverInstallers installers, CancellationToken cancellationToken)
    {
        await _pipe.WriteAsync(InstallHandOver.Go(installers), cancellationToken);
        await _pipe.FlushAsync(cancellationToken);
        _handedOver = true;
        await _pipe.DisposeAsync();
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await Exited.WaitAsync(cancellationToken);
        if (_process.ExitCode != 0)
        {
            throw PanExitCodes.ErrorFor(_process.ExitCode, "--install");
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
