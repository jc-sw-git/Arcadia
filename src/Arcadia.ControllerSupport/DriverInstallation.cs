using Arcadia.ControllerSupport.Pan;
using Arcadia.Launchers.Abstractions;
using Arcadia.Pan;
using Arcadia.Platform;

namespace Arcadia.ControllerSupport;

/// <summary>
/// One driver install from the settings (#413), the steps of <see cref="DriverInstallProgress"/> in their order: the
/// Windows prompt first (D7: declined, nothing has happened), then VIIPER, the checks again, Steam closed, the downloads,
/// the hand-over to elevated Pan and its steps, Steam started again. A failure after the prompt ends Pan before the
/// hand-over (<c>abort</c>), then starts Steam again if Arcadia closed it (D8, D13). VIIPER alone needs no prompt (D11).
/// </summary>
internal sealed class DriverInstallation(
    DriverInstallProgress progress,
    IControllerSupportPlatform platform,
    ILauncherProgram steam,
    HttpClient http,
    TimeProvider time,
    IViiperInstallation? viiper)
{
    /// <summary>Pan secures its data folder before it makes the pipe; a Pan that can't be reached in this time ends the run.</summary>
    public static readonly TimeSpan PanConnectTimeout = TimeSpan.FromSeconds(30);

    /// <summary>VIIPER's download from the Arcadia fork.</summary>
    public static readonly TimeSpan ViiperTimeout = TimeSpan.FromMinutes(7);

    /// <summary>Closing Steam; Steam's own wait for its exit is shorter.</summary>
    public static readonly TimeSpan SteamCloseTimeout = TimeSpan.FromMinutes(2);

    /// <summary>usbip-win2's installer, about 20 MB.</summary>
    public static readonly TimeSpan UsbipDownloadTimeout = TimeSpan.FromMinutes(5);

    /// <summary>HidHide's installer, a few MB.</summary>
    public static readonly TimeSpan HidHideDownloadTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Elevated Pan after the hand-over: its steps' budgets plus a margin, as before #413.</summary>
    public static readonly TimeSpan PanTimeout = PanTimeouts.Install + TimeSpan.FromMinutes(3);

    /// <summary>The longest Arcadia may take from the prompt to the hand-over; stays below <see cref="PanTimeouts.HandOver"/>.</summary>
    public static TimeSpan UpToHandOver => PanConnectTimeout + ViiperTimeout + SteamCloseTimeout + UsbipDownloadTimeout + HidHideDownloadTimeout;

    /// <summary>How often Pan's step report is read while Pan runs.</summary>
    public static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>No report of this run after this long: Pan's steps show as one until its exit (an older Pan, a broken report).</summary>
    public static readonly TimeSpan ReportGrace = TimeSpan.FromSeconds(10);

    /// <summary>Why the install can't go on right before Steam closes (a firmware update, a Steam game), or null.</summary>
    public required Func<string?> BlockerBeforeChanges { get; init; }

    /// <summary>The message when the Windows prompt was declined; it names the row's button.</summary>
    public required string DeclinedText { get; init; }

    public Action<string>? Log { get; init; }

    private DriverInstallStepKind _current;
    private bool _panDecided;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var plan = progress.Plan;
        var succeeded = false;
        string? folder = null;
        try
        {
            if (!plan.Prompt)
            {
                await SetUpViiperAsync(cancellationToken);
                succeeded = true;
                return;
            }

            Begin(DriverInstallStepKind.Prompt);
            var run = await platform.StartPanInstallAsync(new PanInstallPlan(plan.Usbip, plan.HidHide, plan.HidHideRepair), cancellationToken)
                ?? throw new InvalidOperationException(DeclinedText);
            progress.Complete(DriverInstallStepKind.Prompt);
            folder = run.InstallersFolder;
            await using (run)
            {
                await PrepareAndHandOverAsync(run, cancellationToken);
                await WaitForPanAsync(run, cancellationToken);
            }

            succeeded = true;
        }
        catch (Exception ex) when (!_panDecided && ex is not OperationCanceledException)
        {
            progress.Fail(_current, ex.Message);
            throw;
        }
        finally
        {
            // After the abort above (the run is disposed by now): Pan ends first, then Steam comes back (D13).
            if (_steamClosed)
            {
                await StartSteamAsync(waitUntilReady: succeeded, cancellationToken);
            }

            DeleteQuietly(folder);
        }
    }

    /// <summary>
    /// Everything between the prompt and the hand-over. A Pan that ends meanwhile (exit 4, 8, 9) ends the run at once with
    /// its message.
    /// </summary>
    private async Task PrepareAndHandOverAsync(IPanInstallRun run, CancellationToken cancellationToken)
    {
        using var panGone = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Not awaited: it runs only if Pan ends, and a disposed source by then is fine.
        _ = run.Exited.ContinueWith(_ => CancelQuietly(panGone), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            var plan = progress.Plan;
            var token = panGone.Token;
            if (plan.Viiper)
            {
                await SetUpViiperAsync(token);
            }

            // The VIIPER download can take minutes: a game started or a firmware update begun meanwhile stops the run
            // here, before Steam closes (QE addition 1).
            _current = plan.SteamCloses ? DriverInstallStepKind.SteamClose : FirstStepAfterViiper(plan);
            if (BlockerBeforeChanges() is { } blocker)
            {
                throw new InvalidOperationException(blocker);
            }

            if (plan.SteamCloses)
            {
                Begin(DriverInstallStepKind.SteamClose);
                if (steam.IsRunning)
                {
                    if (!await WithTimeoutAsync(t => steam.CloseAsync(time, t), SteamCloseTimeout, "Das Beenden von Steam", token))
                    {
                        throw new InvalidOperationException(ControllerSupportService.SteamWontCloseText);
                    }

                    _steamClosed = true;
                    Log?.Invoke("Steam beendet für die Treiber-Installation");
                }

                progress.Complete(DriverInstallStepKind.SteamClose);
            }

            string? usbip = null, usbipSha = null, hidHide = null;
            if (plan.Usbip)
            {
                Begin(DriverInstallStepKind.UsbipDownload);
                usbip = await WithTimeoutAsync(t => platform.DownloadUsbipInstallerAsync(http, run.InstallersFolder, Bytes(DriverInstallStepKind.UsbipDownload), t),
                    UsbipDownloadTimeout, "Der usbip-win2-Download", token);
                usbipSha = Sha256Of(usbip);
                progress.Complete(DriverInstallStepKind.UsbipDownload);
            }

            if (plan.HidHide)
            {
                Begin(DriverInstallStepKind.HidHideDownload);
                hidHide = await WithTimeoutAsync(t => platform.DownloadHidHideInstallerAsync(http, run.InstallersFolder, Bytes(DriverInstallStepKind.HidHideDownload), t),
                    HidHideDownloadTimeout, "Der HidHide-Download", token);
                progress.Complete(DriverInstallStepKind.HidHideDownload);
            }

            _current = FirstPanStep(plan);
            await run.HandOverAsync(new HandedOverInstallers(usbip, usbipSha, hidHide), token);
            Log?.Invoke("Installer an Pan übergeben");
            progress.Start(_current);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException && run.Exited.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            // Pan ended before the hand-over: its exit code says why (a timeout of its own: its own text).
            throw PanExitCodes.ErrorFor(run.ExitCode, "--install");
        }
    }

    /// <summary>Arcadia closed Steam in this run, so it starts it again, also after a failure (D8).</summary>
    private bool _steamClosed;

    /// <summary>
    /// Pan's steps while it runs, from its report (display only); the end and which step failed follow its exit code:
    /// exit 6 is HidHide alone (Pan was set up), any other failure the step the report names, else the first one open.
    /// </summary>
    private async Task WaitForPanAsync(IPanInstallRun run, CancellationToken cancellationToken)
    {
        var start = time.GetUtcNow();
        var sawReport = false;
        while (!run.Exited.IsCompleted)
        {
            sawReport |= Follow(run);
            if (!sawReport && time.GetUtcNow() - start >= ReportGrace)
            {
                progress.FallBack();
            }

            if (time.GetUtcNow() - start >= PanTimeout)
            {
                // The step Pan is at turns red, not the first one Arcadia started (S6).
                var timeout = new TimeoutException($"Die Treiber-Installation hat nicht innerhalb von {PanTimeout.TotalSeconds:0} Sekunden geantwortet.");
                _panDecided = true;
                progress.PanEnded(FailedPanStep(run) ?? progress.OpenPanStep(), timeout.Message);
                throw timeout;
            }

            await Task.WhenAny(run.Exited, Task.Delay(ReportInterval, time, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        Follow(run);
        _panDecided = true;
        if (run.ExitCode == 0)
        {
            progress.PanEnded(null, null);
            return;
        }

        var error = PanExitCodes.ErrorFor(run.ExitCode, "--install");
        if (run.ExitCode == PanExitCodes.HidHideFailed && progress.Has(DriverInstallStepKind.HidHideInstall))
        {
            progress.PanEnded(DriverInstallStepKind.HidHideInstall, error.Message, DriverInstallStepKind.PanSetup);
        }
        else
        {
            progress.PanEnded(FailedPanStep(run) ?? progress.OpenPanStep(), error.Message);
        }

        throw error;
    }

    private bool Follow(IPanInstallRun run)
    {
        if (run.ReadReport() is not { } report)
        {
            return false;
        }

        progress.FollowReport(report);
        return true;
    }

    /// <summary>The step Pan's report says failed or still runs, if the report is there and its step is shown.</summary>
    private DriverInstallStepKind? FailedPanStep(IPanInstallRun run)
    {
        if (progress.Has(DriverInstallStepKind.PanFallback) || run.ReadReport() is not { } report)
        {
            return null;
        }

        var named = report.FirstOrDefault(pair => pair.Value == PanStepState.Failed) is { Key: var failed, Value: PanStepState.Failed }
            ? failed
            : report.Where(pair => pair.Value == PanStepState.Running).Select(pair => (PanInstallStep?)pair.Key).FirstOrDefault();
        return named switch
        {
            PanInstallStep.Usbip => DriverInstallStepKind.UsbipInstall,
            PanInstallStep.HidHide => DriverInstallStepKind.HidHideInstall,
            PanInstallStep.Pan => DriverInstallStepKind.PanSetup,
            _ => null,
        };
    }

    private async Task SetUpViiperAsync(CancellationToken cancellationToken)
    {
        if (viiper is null || !progress.Plan.Viiper)
        {
            return;
        }

        Begin(DriverInstallStepKind.Viiper);
        await WithTimeoutAsync(async t =>
        {
            await viiper.InstallAsync(http, Bytes(DriverInstallStepKind.Viiper), t);
            return true;
        }, ViiperTimeout, "Der VIIPER-Download", cancellationToken);
        Log?.Invoke("VIIPER eingerichtet");
        progress.Complete(DriverInstallStepKind.Viiper);
    }

    /// <summary>"Steam starten" (D8): also after a failure; waits for Steam only after a success, as before #413.</summary>
    private async Task StartSteamAsync(bool waitUntilReady, CancellationToken cancellationToken)
    {
        progress.Start(DriverInstallStepKind.SteamStart);
        steam.Start();
        Log?.Invoke("Steam wieder gestartet");
        if (waitUntilReady)
        {
            try
            {
                await ProcessWatch.WaitUntilAsync(() => steam.IsReady, ControllerSupportService.SteamStartTimeout, time, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Started, just slow: Steam comes on its own.
            }
        }

        progress.Complete(DriverInstallStepKind.SteamStart);
    }

    private void Begin(DriverInstallStepKind kind)
    {
        _current = kind;
        progress.Start(kind);
    }

    private IProgress<DownloadProgress> Bytes(DriverInstallStepKind kind) => new SynchronousProgress(update => progress.Advance(kind, update.Received, update.Total));

    private async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> call, TimeSpan timeout, string what, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout, time);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            return await call(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{what} dauerte länger als {timeout.TotalMinutes:0} Minuten – die Installation wurde abgebrochen.");
        }
    }

    private static DriverInstallStepKind FirstStepAfterViiper(DriverInstallPlan plan) =>
        plan.Usbip ? DriverInstallStepKind.UsbipDownload : plan.HidHide ? DriverInstallStepKind.HidHideDownload : FirstPanStep(plan);

    private static DriverInstallStepKind FirstPanStep(DriverInstallPlan plan) =>
        plan.Usbip ? DriverInstallStepKind.UsbipInstall : plan.HidHide ? DriverInstallStepKind.HidHideInstall : DriverInstallStepKind.PanSetup;

    private static string Sha256Of(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static void CancelQuietly(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void DeleteQuietly(string? folder)
    {
        if (folder is null)
        {
            return;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // %TEMP% is cleaned by Windows; a file Pan still copies stays until then.
        }
    }

    /// <summary>Reports on the thread that downloads: <see cref="Progress{T}"/> would post to a context that may not exist.</summary>
    private sealed class SynchronousProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
