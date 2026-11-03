using System.Globalization;

namespace Arcadia.ControllerSupport;

/// <summary>The steps of a driver install from the settings (#413), in the order they run.</summary>
public enum DriverInstallStepKind
{
    Prompt,
    Viiper,
    SteamClose,
    UsbipDownload,
    HidHideDownload,
    UsbipInstall,
    HidHideInstall,
    PanSetup,

    /// <summary>Pan's steps as one, when Pan reports none Arcadia can read (an older Pan, a broken report).</summary>
    PanFallback,
    SteamStart,
    ArcadiaRestart,
}

public enum DriverInstallStepState
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <param name="Detail">Beside a running step: a download's bytes, or that usbip-win2 takes minutes.</param>
public sealed record DriverInstallStep(DriverInstallStepKind Kind, string Title, DriverInstallStepState State, string? Detail = null);

/// <summary>What a run installs; its steps follow from it (D4: steps that don't run aren't listed).</summary>
/// <param name="Prompt">Elevated Pan runs: every run but VIIPER alone (D11).</param>
/// <param name="SteamCloses">HidHide's installer runs while Steam runs: Arcadia closes Steam and starts it again.</param>
/// <param name="Restarts">HidHide is installed in this run: Arcadia restarts at the end (D6).</param>
public sealed record DriverInstallPlan(bool Prompt, bool Viiper, bool SteamCloses, bool Usbip, bool HidHide, bool HidHideRepair, bool Restarts)
{
    public IEnumerable<DriverInstallStepKind> Steps()
    {
        if (Prompt)
        {
            yield return DriverInstallStepKind.Prompt;
        }

        if (Viiper)
        {
            yield return DriverInstallStepKind.Viiper;
        }

        if (SteamCloses)
        {
            yield return DriverInstallStepKind.SteamClose;
        }

        if (Usbip)
        {
            yield return DriverInstallStepKind.UsbipDownload;
        }

        if (HidHide)
        {
            yield return DriverInstallStepKind.HidHideDownload;
        }

        if (Usbip)
        {
            yield return DriverInstallStepKind.UsbipInstall;
        }

        if (HidHide)
        {
            yield return DriverInstallStepKind.HidHideInstall;
        }

        if (Prompt)
        {
            yield return DriverInstallStepKind.PanSetup;
        }

        if (SteamCloses)
        {
            yield return DriverInstallStepKind.SteamStart;
        }

        if (Restarts)
        {
            yield return DriverInstallStepKind.ArcadiaRestart;
        }
    }
}

/// <summary>
/// The progress dialog's model for one driver install (#413): the run's steps, each pending, running, done or failed,
/// and a bar weighted per step, downloads by their bytes, that never goes backwards. Written by the install on the thread
/// pool, read by the settings on the UI thread: every read is a snapshot taken under the lock.
/// </summary>
public sealed class DriverInstallProgress
{
    public const string RunningTitle = "Treiber werden installiert";
    public const string FailedTitle = "Installation fehlgeschlagen";
    public const string PromptRunningText = "Bitte bestätige die Windows-Abfrage";
    public const string UsbipRunningDetail = "kann einige Minuten dauern";

    private static readonly DriverInstallStepKind[] PanSteps = [DriverInstallStepKind.UsbipInstall, DriverInstallStepKind.HidHideInstall, DriverInstallStepKind.PanSetup];

    private readonly Lock _lock = new();
    private readonly List<Entry> _steps;
    private double _value;

    public DriverInstallProgress(DriverInstallPlan plan)
    {
        Plan = plan;
        _steps = [.. plan.Steps().Select(kind => new Entry(kind))];
    }

    /// <summary>Something changed; raised outside the lock, on the thread that changed it.</summary>
    public event EventHandler? Changed;

    public DriverInstallPlan Plan { get; }

    /// <summary>usbip-win2 is installed in this run: mouse, keyboard and controllers may freeze for a moment (D5).</summary>
    public bool ShowsUsbHint => Plan.Usbip;

    /// <summary>The install ended (done or failed); the dialog closes on success, offers "Schließen" after a failure.</summary>
    public bool IsOver { get { lock (_lock) { return _over; } } }

    public bool Failed { get { lock (_lock) { return _error is not null; } } }

    /// <summary>The failed step's message, as the row shows it afterwards.</summary>
    public string? Error { get { lock (_lock) { return _error; } } }

    private bool _over;
    private string? _error;

    /// <summary>0 to 1; grows with each step and with a download's bytes, never shrinks.</summary>
    public double Value { get { lock (_lock) { return _value; } } }

    public IReadOnlyList<DriverInstallStep> Steps
    {
        get
        {
            lock (_lock)
            {
                return [.. _steps.Select(step => step.ToStep())];
            }
        }
    }

    /// <summary>The step a failure or the running step is at, counted from 1, for "Schritt n von m".</summary>
    public int CurrentNumber
    {
        get
        {
            lock (_lock)
            {
                var at = _steps.FindIndex(step => step.State is DriverInstallStepState.Failed);
                if (at < 0)
                {
                    at = _steps.FindIndex(step => step.State is DriverInstallStepState.Running);
                }

                if (at < 0)
                {
                    at = _steps.FindLastIndex(step => step.State is DriverInstallStepState.Done);
                }

                return Math.Max(1, at + 1);
            }
        }
    }

    public DriverInstallStepState StateOf(DriverInstallStepKind kind)
    {
        lock (_lock)
        {
            return _steps.Find(step => step.Kind == kind)?.State ?? DriverInstallStepState.Pending;
        }
    }

    public bool Has(DriverInstallStepKind kind)
    {
        lock (_lock)
        {
            return _steps.Exists(step => step.Kind == kind);
        }
    }

    public void Start(DriverInstallStepKind kind) => Update(kind, entry =>
    {
        entry.State = DriverInstallStepState.Running;
        entry.Detail = kind == DriverInstallStepKind.UsbipInstall ? UsbipRunningDetail : null;
    });

    /// <summary>A download's bytes; <paramref name="total"/> null when the size isn't known (no fine progress then).</summary>
    public void Advance(DriverInstallStepKind kind, long received, long? total) => Update(kind, entry =>
    {
        if (entry.State != DriverInstallStepState.Running)
        {
            return;
        }

        entry.Fraction = total is > 0 ? Math.Clamp((double)received / total.Value, 0, 1) : 0;
        entry.Detail = total is > 0 ? $"{Megabytes(received)} von {Megabytes(total.Value)} MB" : null;
    });

    public void Complete(DriverInstallStepKind kind) => Update(kind, entry =>
    {
        entry.State = DriverInstallStepState.Done;
        entry.Detail = null;
    });

    /// <summary>The step turns red with <paramref name="error"/>; the bar keeps its value; the install is over.</summary>
    public void Fail(DriverInstallStepKind kind, string error)
    {
        lock (_lock)
        {
            if (_steps.Find(step => step.Kind == kind) is { } entry)
            {
                entry.State = DriverInstallStepState.Failed;
                entry.Detail = null;
            }

            _error ??= error;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pan's report, read while Pan runs: display only (the exit code decides the end). Pan's steps that the run has follow
    /// it; once done or failed a step stays so, as a running step never goes back to pending.
    /// </summary>
    public void FollowReport(IReadOnlyDictionary<Arcadia.Pan.PanInstallStep, Arcadia.Pan.PanStepState> report)
    {
        foreach (var (step, state) in report)
        {
            var kind = step switch
            {
                Arcadia.Pan.PanInstallStep.Usbip => DriverInstallStepKind.UsbipInstall,
                Arcadia.Pan.PanInstallStep.HidHide => DriverInstallStepKind.HidHideInstall,
                _ => DriverInstallStepKind.PanSetup,
            };
            if (StateOf(kind) is DriverInstallStepState.Done or DriverInstallStepState.Failed || !Has(kind))
            {
                continue;
            }

            switch (state)
            {
                case Arcadia.Pan.PanStepState.Running:
                    Start(kind);
                    break;
                case Arcadia.Pan.PanStepState.Done:
                    Complete(kind);
                    break;
                default:
                    // Red only by the exit code; the report may say so first, the row then shows it running.
                    Start(kind);
                    break;
            }
        }
    }

    /// <summary>Pan's steps as one "Treiber werden installiert" (no readable report); its weight is theirs.</summary>
    public void FallBack()
    {
        lock (_lock)
        {
            var first = _steps.FindIndex(step => PanSteps.Contains(step.Kind));
            if (first < 0 || _steps.Exists(step => step.Kind == DriverInstallStepKind.PanFallback))
            {
                return;
            }

            var replaced = _steps.Where(step => PanSteps.Contains(step.Kind)).ToList();
            _steps.RemoveAll(step => PanSteps.Contains(step.Kind));
            _steps.Insert(first, new Entry(DriverInstallStepKind.PanFallback, replaced.Sum(step => step.Weight)) { State = DriverInstallStepState.Running });
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pan's exit decided (the report never does): on success every Pan step is ✓; on a failure <paramref name="failed"/>
    /// is red and the Pan steps before it ✓ (<paramref name="doneAfter"/>: also ✓ though later, e.g. Pan after HidHide).
    /// </summary>
    public void PanEnded(DriverInstallStepKind? failed, string? error, params DriverInstallStepKind[] doneAfter)
    {
        lock (_lock)
        {
            var failedAt = failed is { } kind ? _steps.FindIndex(step => step.Kind == kind) : -1;
            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                if (!PanSteps.Contains(step.Kind) && step.Kind != DriverInstallStepKind.PanFallback)
                {
                    continue;
                }

                if (i == failedAt)
                {
                    step.State = DriverInstallStepState.Failed;
                }
                else if (failedAt < 0 || i < failedAt || doneAfter.Contains(step.Kind))
                {
                    step.State = DriverInstallStepState.Done;
                }
                else
                {
                    step.State = DriverInstallStepState.Pending;
                }

                step.Detail = null;
            }

            if (error is not null)
            {
                _error ??= error;
            }

            Recalculate();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The run is over: a failure keeps the dialog with "Schließen", a success lets it close.</summary>
    public void End()
    {
        lock (_lock)
        {
            _over = true;
            Recalculate();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The first Pan step that hasn't ended, else the last one: where a failure without a report is shown.</summary>
    public DriverInstallStepKind? OpenPanStep()
    {
        lock (_lock)
        {
            var pan = _steps.Where(step => PanSteps.Contains(step.Kind) || step.Kind == DriverInstallStepKind.PanFallback).ToList();
            return (pan.Find(step => step.State is DriverInstallStepState.Running or DriverInstallStepState.Failed)
                ?? pan.Find(step => step.State is DriverInstallStepState.Pending)
                ?? pan.LastOrDefault())?.Kind;
        }
    }

    private void Update(DriverInstallStepKind kind, Action<Entry> change)
    {
        lock (_lock)
        {
            if (_steps.Find(step => step.Kind == kind) is not { } entry)
            {
                return;
            }

            change(entry);
            Recalculate();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Recalculate()
    {
        var total = _steps.Sum(step => step.Weight);
        var reached = _steps.Sum(step => step.State switch
        {
            DriverInstallStepState.Done => step.Weight,
            DriverInstallStepState.Running => step.Weight * step.Fraction,
            _ => 0,
        });
        _value = Math.Max(_value, total > 0 ? Math.Min(1, reached / total) : 0);
    }

    private static string Megabytes(long bytes) => (bytes / 1_048_576d).ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>The weights of the design (T413Dialog): usbip-win2's install is the long one, HidHide's next.</summary>
    private static double WeightOf(DriverInstallStepKind kind) => kind switch
    {
        DriverInstallStepKind.UsbipInstall => 4,
        DriverInstallStepKind.HidHideInstall => 2,
        _ => 1,
    };

    private static string TitleOf(DriverInstallStepKind kind) => kind switch
    {
        DriverInstallStepKind.Prompt => "Windows-Abfrage bestätigen",
        DriverInstallStepKind.Viiper => "VIIPER herunterladen und einrichten",
        DriverInstallStepKind.SteamClose => "Steam beenden",
        DriverInstallStepKind.UsbipDownload => "usbip-win2 herunterladen",
        DriverInstallStepKind.HidHideDownload => "HidHide herunterladen",
        DriverInstallStepKind.UsbipInstall => "usbip-win2 installieren",
        DriverInstallStepKind.HidHideInstall => "HidHide installieren",
        DriverInstallStepKind.PanSetup => "Pan einrichten",
        DriverInstallStepKind.PanFallback => RunningTitle,
        DriverInstallStepKind.SteamStart => "Steam starten",
        _ => "Arcadia neu starten",
    };

    private sealed class Entry(DriverInstallStepKind kind, double? weight = null)
    {
        public DriverInstallStepKind Kind { get; } = kind;

        public double Weight { get; } = weight ?? WeightOf(kind);

        public DriverInstallStepState State { get; set; }

        public double Fraction { get; set; }

        public string? Detail { get; set; }

        public DriverInstallStep ToStep() => new(Kind,
            Kind == DriverInstallStepKind.Prompt && State == DriverInstallStepState.Running ? PromptRunningText : TitleOf(Kind), State, Detail);
    }
}
