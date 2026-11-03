namespace Arcadia.Pan.Tests;

/// <summary>
/// Pan's step report of a driver install (#413) and the command line that announces a run: a report is tied to its run,
/// display only, and one Pan can't read makes Arcadia fall back to one step.
/// </summary>
public sealed class InstallReportTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ArcadiaPanTests", "report-" + Guid.NewGuid().ToString("N"));

    public InstallReportTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    [Trait("TestCase", "T413-12")]
    public void The_writer_s_lines_read_back_as_each_step_s_latest_state()
    {
        var id = InstallRun.NewId();
        var writer = new InstallReportWriter(_folder, id, _ => { });

        writer.Write(PanInstallStep.Usbip, PanStepState.Running);
        writer.Write(PanInstallStep.Usbip, PanStepState.Done);
        writer.Write(PanInstallStep.HidHide, PanStepState.Running);

        var report = InstallReport.Parse(id, File.ReadAllText(InstallReport.PathOf(_folder, id)));
        Assert.NotNull(report);
        Assert.Equal(PanStepState.Done, report[PanInstallStep.Usbip]);
        Assert.Equal(PanStepState.Running, report[PanInstallStep.HidHide]);
        Assert.False(report.ContainsKey(PanInstallStep.Pan));
    }

    [Fact]
    [Trait("TestCase", "T413-12")]
    public void Another_run_s_report_is_never_read_as_this_one_s()
    {
        var earlier = InstallRun.NewId();
        var now = InstallRun.NewId();
        var text = InstallReport.Header(earlier) + "\n" + InstallReport.Line(PanInstallStep.Pan, PanStepState.Done) + "\n";

        Assert.Null(InstallReport.Parse(now, text));
        Assert.NotNull(InstallReport.Parse(earlier, text));
    }

    [Fact]
    [Trait("TestCase", "T413-12")]
    public void A_new_run_removes_earlier_runs_reports_so_only_its_own_is_left()
    {
        var earlier = InstallRun.NewId();
        new InstallReportWriter(_folder, earlier, _ => { }).Write(PanInstallStep.Pan, PanStepState.Done);
        var now = InstallRun.NewId();

        _ = new InstallReportWriter(_folder, now, _ => { });

        Assert.Equal([InstallReport.FileName(now)], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Theory]
    [Trait("TestCase", "T413-14")]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("run {id}\nusbip exploded\n")]
    [InlineData("run {id}\nteleport done\n")]
    public void An_empty_or_garbled_report_reads_as_none(string text)
    {
        var id = InstallRun.NewId();

        Assert.Null(InstallReport.Parse(id, text.Replace("{id}", id, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("TestCase", "T413-14")]
    public void A_line_still_being_written_doesn_t_spoil_the_lines_before_it()
    {
        var id = InstallRun.NewId();
        var text = $"run {id}\nusbip done\nhidhide run";

        var report = InstallReport.Parse(id, text);

        Assert.NotNull(report);
        Assert.Equal(PanStepState.Done, report[PanInstallStep.Usbip]);
        Assert.False(report.ContainsKey(PanInstallStep.HidHide));
    }

    [Fact]
    [Trait("TestCase", "T413-14")]
    public void A_report_that_can_t_be_written_never_stops_the_install()
    {
        var logged = new List<string>();
        var missing = Path.Combine(_folder, "missing");

        var writer = new InstallReportWriter(missing, InstallRun.NewId(), logged.Add);
        writer.Write(PanInstallStep.Pan, PanStepState.Running);

        Assert.Single(logged);
    }

    [Fact]
    [Trait("TestCase", "T413-25")]
    public void A_run_s_command_line_reads_back_as_the_same_run()
    {
        var run = new InstallRun(InstallRun.NewId(), 4242, _folder, Usbip: true, HidHide: true, HidHideRepair: true);

        var read = InstallRun.Parse(["--install", "--expect-hash", new string('0', 64), .. run.ToArguments()]);

        Assert.Equal(run, read);
    }

    [Fact]
    public void Without_a_run_the_command_line_installs_Pan_alone()
    {
        Assert.Null(InstallRun.Parse(["--install", "--expect-hash", new string('0', 64)]));
    }

    [Theory]
    [Trait("TestCase", "T413-26")]
    [InlineData("--install", "--usbip")]
    [InlineData("--install", "--run", "not-hex", "--client", "1", "--installers", "/tmp")]
    [InlineData("--install", "--run", "0123456789abcdef0123456789abcdef", "--installers", "/tmp")]
    [InlineData("--install", "--run", "0123456789abcdef0123456789abcdef", "--client", "1", "--installers", "relative")]
    [InlineData("--install", "--run", "0123456789abcdef0123456789abcdef", "--client", "1", "--installers", "/tmp", "--hidhide-repair")]
    public void A_half_or_malformed_run_is_refused_before_anything_is_touched(params string[] args)
    {
        Assert.Throws<InvalidOperationException>(() => InstallRun.Parse(args));
    }

    [Fact]
    [Trait("TestCase", "T413-25")]
    public void A_run_id_has_128_random_bits()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => InstallRun.NewId()).ToHashSet();

        Assert.Equal(100, ids.Count);
        Assert.All(ids, id => Assert.True(InstallRun.IsValidId(id)));
    }
}
