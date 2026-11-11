using System.Diagnostics;
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

/// <summary>The real one: the bundled <c>Pan.exe</c> started elevated, its pipe (<see cref="PanHandOver"/>), its report in Pan's data folder.</summary>
[SupportedOSPlatform("windows")]
internal sealed class PanInstallRun : IPanInstallRun
{
    private const string Command = "--install";

    private readonly PanHandOver _handOver;
    private readonly string _runId;

    private PanInstallRun(PanHandOver handOver, InstallRun run)
    {
        _handOver = handOver;
        _runId = run.Id;
        InstallersFolder = run.InstallersFolder;
    }

    public string InstallersFolder { get; }

    public Task Exited => _handOver.Exited;

    public int ExitCode => _handOver.ExitCode;

    /// <summary>Connects to the pipe of the Pan just started; throws when Pan ended first (its exit code then says why).</summary>
    public static async Task<PanInstallRun> ConnectAsync(Process process, InstallRun run, CancellationToken cancellationToken) =>
        new(await PanHandOver.ConnectAsync(process, InstallHandOver.PipeName(run.Id), Command, ControllerSupportTexts.PanWhatInstallation, cancellationToken), run);

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

    public Task HandOverAsync(HandedOverInstallers installers, CancellationToken cancellationToken) =>
        _handOver.SendAsync(InstallHandOver.Go(installers), cancellationToken);

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _handOver.WaitForExitAsync(cancellationToken);

    public ValueTask DisposeAsync() => _handOver.DisposeAsync();
}
