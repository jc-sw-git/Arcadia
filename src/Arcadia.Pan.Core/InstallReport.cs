using System.Text;

namespace Arcadia.Pan;

/// <summary>The steps of an install that elevated Pan runs after the hand-over (#413), in their order.</summary>
public enum PanInstallStep
{
    Usbip,
    HidHide,
    Pan,
}

public enum PanStepState
{
    Running,
    Done,
    Failed,
}

/// <summary>
/// Pan's step report for one run (#413): a file per run in Pan's data folder (<c>install-&lt;id&gt;.report</c>), which users
/// may read but not write, so the report needs no new rights and no second prompt. Display only: Arcadia's decisions
/// follow Pan's exit code, so a broken or forged report can't change what Arcadia does. Lines: <c>run &lt;id&gt;</c>,
/// then <c>&lt;step&gt; &lt;state&gt;</c> each time a step starts or ends.
/// </summary>
public static class InstallReport
{
    private const string Prefix = "install-";
    private const string Extension = ".report";
    private const string RunKey = "run ";

    public static string FileName(string runId) => Prefix + runId + Extension;

    public static string PathOf(string dataDirectory, string runId) => Path.Combine(dataDirectory, FileName(runId));

    public static string Header(string runId) => RunKey + runId;

    public static string Line(PanInstallStep step, PanStepState state) => $"{Name(step)} {Name(state)}";

    /// <summary>
    /// Each step's latest state, or null when the text isn't a report of run <paramref name="runId"/> (another run's,
    /// an older Pan's, a half written or garbled one): Arcadia then shows one step until Pan's exit.
    /// </summary>
    public static IReadOnlyDictionary<PanInstallStep, PanStepState>? Parse(string runId, string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[0].TrimEnd('\r') != Header(runId))
        {
            return null;
        }

        var states = new Dictionary<PanInstallStep, PanStepState>();
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].TrimEnd('\r').Split(' ');
            if (parts.Length != 2 || StepOf(parts[0]) is not { } step || StateOf(parts[1]) is not { } state)
            {
                // A line still being written ends the file without its break: what came before stands.
                if (i == lines.Length - 1 && !text.EndsWith('\n'))
                {
                    break;
                }

                return null;
            }

            states[step] = state;
        }

        return states;
    }

    /// <summary>Whether <paramref name="fileName"/> is some run's report (Pan removes earlier runs' reports at a new run).</summary>
    public static bool IsReport(string fileName) =>
        fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    private static string Name(PanInstallStep step) => step switch
    {
        PanInstallStep.Usbip => "usbip",
        PanInstallStep.HidHide => "hidhide",
        _ => "pan",
    };

    private static string Name(PanStepState state) => state switch
    {
        PanStepState.Running => "running",
        PanStepState.Done => "done",
        _ => "failed",
    };

    private static PanInstallStep? StepOf(string name) => name switch
    {
        "usbip" => PanInstallStep.Usbip,
        "hidhide" => PanInstallStep.HidHide,
        "pan" => PanInstallStep.Pan,
        _ => null,
    };

    private static PanStepState? StateOf(string name) => name switch
    {
        "running" => PanStepState.Running,
        "done" => PanStepState.Done,
        "failed" => PanStepState.Failed,
        _ => null,
    };
}

/// <summary>
/// Writes a run's report in Pan's data folder, which Pan has secured by then; a write that fails never stops the install
/// (Arcadia falls back to one step). Earlier runs' reports go first, so the folder keeps one.
/// </summary>
public sealed class InstallReportWriter
{
    private static readonly Encoding NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly string _path;
    private readonly Action<string> _log;
    private bool _broken;

    public InstallReportWriter(string dataDirectory, string runId, Action<string> log)
    {
        _path = InstallReport.PathOf(dataDirectory, runId);
        _log = log;
        try
        {
            foreach (var old in Directory.EnumerateFiles(dataDirectory).Where(file => InstallReport.IsReport(Path.GetFileName(file))))
            {
                File.Delete(old);
            }

            File.WriteAllText(_path, InstallReport.Header(runId) + "\n", NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Broken(ex);
        }
    }

    public void Write(PanInstallStep step, PanStepState state)
    {
        if (_broken)
        {
            return;
        }

        try
        {
            File.AppendAllText(_path, InstallReport.Line(step, state) + "\n", NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Broken(ex);
        }
    }

    private void Broken(Exception ex)
    {
        _broken = true;
        _log($"Step report not written ({ex.Message}); Arcadia shows the install as one step");
    }
}
