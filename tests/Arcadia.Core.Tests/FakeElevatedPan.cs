using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;

namespace Arcadia.Tests.Pan;

/// <summary>
/// Elevated Pan of a driver install (#413) as a test sets it: the Windows prompt (confirmed, declined, or held open),
/// the hand-over, Pan's step report and its exit code, and the downloads Arcadia makes in between. Everything that
/// happens lands in <see cref="Calls"/> in order, so a test can check that the prompt came first. Thread-safe: the
/// install runs on the thread pool.
/// </summary>
internal sealed class FakeElevatedPan
{
    private readonly Lock _lock = new();
    private readonly List<string> _calls = [];
    private FakeRun? _run;

    /// <summary>What happened, in order: "prompt", "download usbip-win2", "download HidHide", "go", "abort", "exit 0", …</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public bool Declines { get; set; }

    /// <summary>Set: the Windows prompt stays open until the test answers it (true confirms).</summary>
    public TaskCompletionSource<bool>? Prompt { get; set; }

    /// <summary>Pan ends before Arcadia reaches its pipe (4 data folder, 8 unelevated, 9 pipe taken).</summary>
    public int? EndsBeforeConnect { get; set; }

    /// <summary>Pan ends while it waits for the hand-over, e.g. at its own timeout (9); the run is told at the next download.</summary>
    public int? EndsWhileWaiting { get; set; }

    /// <summary>Pan's exit code after the hand-over.</summary>
    public int ExitCode { get; set; }

    /// <summary>Set: Pan keeps running after the hand-over until the test completes it.</summary>
    public TaskCompletionSource? Running { get; set; }

    /// <summary>Pan writes no report Arcadia can read (an older Pan, a garbled file): the dialog falls back to one step.</summary>
    public bool WritesNoReport { get; set; }

    /// <summary>The step Pan's report names as failed at a failing exit.</summary>
    public PanInstallStep? ReportFailsAt { get; set; }

    /// <summary>Overrides the report at the end, e.g. "all done" with a failing exit code (display only, T413-13).</summary>
    public IReadOnlyDictionary<PanInstallStep, PanStepState>? FinalReport { get; set; }

    public Exception? UsbipDownloadError { get; set; }

    public Exception? HidHideDownloadError { get; set; }

    /// <summary>Downloads never finish; their timeout ends them (virtual time).</summary>
    public bool DownloadsHang { get; set; }

    /// <summary>Runs as Arcadia hands over, e.g. to cancel the install or start a Steam game.</summary>
    public Action<PanInstallPlan>? OnHandOver { get; set; }

    /// <summary>Runs at Pan's exit after a hand-over, with its exit code: the platform fake installs the parts.</summary>
    public Action<PanInstallPlan, int>? OnExit { get; set; }

    /// <summary>Runs as Arcadia starts a download, e.g. to start a Steam game meanwhile.</summary>
    public Action? OnDownload { get; set; }

    public int Prompts => Calls.Count(call => call == "prompt");

    public int HandOvers => Calls.Count(call => call == "go");

    public PanInstallPlan? LastPlan { get; private set; }

    public HandedOverInstallers? LastHandOver { get; private set; }

    public bool Aborted => Calls.Contains("abort");

    public void Note(string call)
    {
        lock (_lock)
        {
            _calls.Add(call);
        }
    }

    /// <summary>Pan's report says <paramref name="step"/> is in <paramref name="state"/> now (while <see cref="Running"/> holds it).</summary>
    public void Report(PanInstallStep step, PanStepState state) => _run?.Report(step, state);

    public async Task<IPanInstallRun?> StartAsync(PanInstallPlan plan, CancellationToken cancellationToken)
    {
        Note("prompt");
        LastPlan = plan;
        var confirmed = Prompt is { } prompt ? await prompt.Task.WaitAsync(cancellationToken) : !Declines;
        if (!confirmed)
        {
            Note("declined");
            return null;
        }

        if (EndsBeforeConnect is { } early)
        {
            Note($"exit {early}");
            throw PanExitCodes.ErrorFor(early, "--install");
        }

        var folder = Path.Combine(Path.GetTempPath(), "ArcadiaTests", "install-" + InstallRun.NewId());
        Directory.CreateDirectory(folder);
        _run = new FakeRun(this, plan, folder);
        if (EndsWhileWaiting is { } waiting)
        {
            _run.End(waiting);
        }

        return _run;
    }

    public async Task<string> DownloadAsync(string name, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        Note("download " + name);
        OnDownload?.Invoke();
        if (DownloadsHang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if ((name == "usbip-win2" ? UsbipDownloadError : HidHideDownloadError) is { } error)
        {
            throw error;
        }

        // Thrown when the run ended meanwhile, as a real download would be cancelled.
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new DownloadProgress(0, 3_000_000));
        progress.Report(new DownloadProgress(1_500_000, 3_000_000));
        var file = Path.Combine(folder, name + ".exe");
        await File.WriteAllTextAsync(file, name, cancellationToken);
        progress.Report(new DownloadProgress(3_000_000, 3_000_000));
        return file;
    }

    private sealed class FakeRun(FakeElevatedPan pan, PanInstallPlan plan, string folder) : IPanInstallRun
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _lock = new();
        private Dictionary<PanInstallStep, PanStepState>? _report;
        private bool _handedOver;
        private int _exitCode;

        public string InstallersFolder { get; } = folder;

        public Task Exited => _exited.Task;

        public int ExitCode => _exitCode;

        public IReadOnlyDictionary<PanInstallStep, PanStepState>? ReadReport()
        {
            lock (_lock)
            {
                return pan.WritesNoReport || _report is null ? null : new Dictionary<PanInstallStep, PanStepState>(_report);
            }
        }

        public void Report(PanInstallStep step, PanStepState state)
        {
            lock (_lock)
            {
                _report ??= [];
                _report[step] = state;
            }
        }

        public async Task HandOverAsync(HandedOverInstallers installers, CancellationToken cancellationToken)
        {
            if (Exited.IsCompleted)
            {
                throw new IOException("Pipe is broken.");
            }

            _handedOver = true;
            pan.LastHandOver = installers;
            pan.Note("go");
            pan.OnHandOver?.Invoke(plan);
            var steps = Steps().ToList();
            Report(steps[0], PanStepState.Running);
            _ = Task.Run(async () =>
            {
                if (pan.Running is { } running)
                {
                    await running.Task;
                }

                lock (_lock)
                {
                    _report = pan.FinalReport is { } final ? new(final) : Final(steps);
                }

                pan.OnExit?.Invoke(plan, pan.ExitCode);
                End(pan.ExitCode);
            }, CancellationToken.None);
            await Task.CompletedTask;
        }

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            await Exited.WaitAsync(cancellationToken);
            if (_exitCode != 0)
            {
                throw PanExitCodes.ErrorFor(_exitCode, "--install");
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!_handedOver && !Exited.IsCompleted)
            {
                pan.Note("abort");
                End(InstallHandOver.EndedExitCode);
            }

            return ValueTask.CompletedTask;
        }

        public void End(int exitCode)
        {
            _exitCode = exitCode;
            if (_exited.TrySetResult())
            {
                pan.Note($"exit {exitCode}");
            }
        }

        private IEnumerable<PanInstallStep> Steps()
        {
            if (plan.Usbip)
            {
                yield return PanInstallStep.Usbip;
            }

            if (plan.HidHide)
            {
                yield return PanInstallStep.HidHide;
            }

            yield return PanInstallStep.Pan;
        }

        /// <summary>Exit 0: all done; exit 6: HidHide failed, Pan done; else the step named failed, the ones after it not run.</summary>
        private Dictionary<PanInstallStep, PanStepState> Final(List<PanInstallStep> steps)
        {
            var report = new Dictionary<PanInstallStep, PanStepState>();
            var failsAt = pan.ExitCode switch
            {
                0 => (PanInstallStep?)null,
                6 => PanInstallStep.HidHide,
                _ => pan.ReportFailsAt ?? steps[0],
            };
            foreach (var step in steps)
            {
                if (step == failsAt)
                {
                    report[step] = PanStepState.Failed;
                    if (pan.ExitCode != 6)
                    {
                        break;
                    }
                }
                else
                {
                    report[step] = PanStepState.Done;
                }
            }

            return report;
        }
    }
}
