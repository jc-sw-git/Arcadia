using Arcadia.Core.Launchers;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests;

public sealed class EpicInstallMonitorTests : IDisposable
{
    // Lines captured from the owner's Epic log on 2026-10-10 (Breathedge, Astrea).
    private const string Ns = "08ae29e4f70a4b62aa055e383381aa82:1addbe9a36af44b2868133287a3673c0";
    private const string App = "0a20ccd3f1b3464da750a4dbf8c80d7c";
    private const string Other = "f9685170f7b142909c661a080bd9e60c";

    private const string Queued = $"[2026.10.10-07.13.51:175][659]LogDownloadManager: Queued install for the {Ns}:{App}, Queue length is 1, Are we restarting followers 0";
    private const string Started = $"[2026.10.10-07.13.51:188][660]LogEoshPatcher: StartTask: BinaryId [{Ns}:{App}:1.1.0.3] — using default components: []";
    private const string Paused = $"[2026.10.10-07.17.40:316][ 20]LogDownloadManager: Pause is requested for the {Ns}:{App}";
    private const string Resumed = $"[2026.10.10-07.19.59:808][259]LogDownloadManager: Resume of active installation is requested for the {Ns}:{App}";
    private const string Completed = $"[2026.10.10-07.20.47:339][  7]LogDownloadManager: HandleTaskComplete: AppId [{Ns}:{App}] AlertCode=[ok] QueueLen=2 IncompleteInstall=0 AutoResume=1";
    private const string OtherQueued = $"[2026.10.10-07.20.10:570][807]LogDownloadManager: Queued install for the a940fa38f001486a9884640924119576:f3ac42b603f546ccbd0f0240ba120143:{Other}, Queue length is 2, Are we restarting followers 0";
    private const string Uninstalled = $"[2026.10.10-07.21.48:919][256]LogCommunityPortalAppManager: FCommunityPortalAppManagerImpl: Uninstall {Ns}:{App}";
    private const string OtherCancelled = $"[2026.10.10-07.20.57:737][405]LogDownloadManager: Cancel of active installation is requested for the a940fa38f001486a9884640924119576:f3ac42b603f546ccbd0f0240ba120143:{Other}";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-epic-" + Guid.NewGuid().ToString("N"));
    private readonly string _data;
    private readonly string _log;
    private readonly string _installDir;

    public EpicInstallMonitorTests()
    {
        _data = Path.Combine(_root, "Data");
        _log = Path.Combine(_root, "EpicGamesLauncher.log");
        _installDir = Path.Combine(_root, "Games", "Breathedge");
        Directory.CreateDirectory(Path.Combine(_data, "DownloadManager"));
        Directory.CreateDirectory(Path.Combine(_data, "Manifests", "Pending"));
        File.WriteAllText(_log, string.Empty);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(Queued, EpicInstallEvent.Queued)]
    [InlineData(Started, EpicInstallEvent.Started)]
    [InlineData(Resumed, EpicInstallEvent.Started)]
    [InlineData(Paused, EpicInstallEvent.Paused)]
    [InlineData(Completed, EpicInstallEvent.Completed)]
    [InlineData(Uninstalled, EpicInstallEvent.Uninstalled)]
    public void Parses_log_lines(string line, EpicInstallEvent expected) =>
        Assert.Equal((App, expected), EpicInstallMonitor.ParseLogLine(line));

    [Fact]
    public void Parses_cancel_and_failure()
    {
        Assert.Equal((Other, EpicInstallEvent.Cancelled), EpicInstallMonitor.ParseLogLine(OtherCancelled));
        Assert.Equal((App, EpicInstallEvent.Failed), EpicInstallMonitor.ParseLogLine(Completed.Replace("AlertCode=[ok]", "AlertCode=[error]", StringComparison.Ordinal)));
    }

    [Fact]
    public void Ignores_unrelated_lines() =>
        Assert.Null(EpicInstallMonitor.ParseLogLine("[2026.10.10-07.15.55:890][402]LogSelfUpdateService: ExecuteEpicGamesUpdater: queryinstallation"));

    [Fact]
    public void Reports_download_progress_from_the_install_directory()
    {
        SetQueue(App);
        WritePending(App, installSize: 1000);
        WriteGameBytes(250);
        AppendLog(Queued, Started);

        var progress = Monitor().Poll().Active[App];

        Assert.Equal(InstallPhase.Downloading, progress.Phase);
        Assert.Equal(0.25, progress.Fraction!.Value, precision: 3);
    }

    [Fact]
    public void Reports_paused_and_queued_installations()
    {
        SetQueue(App, Other);
        WritePending(App, installSize: 1000);
        WriteGameBytes(500);
        AppendLog(Queued, Started, OtherQueued, Paused);

        var active = Monitor().Poll().Active;

        Assert.Equal(InstallPhase.Paused, active[App].Phase);
        Assert.Equal(0.5, active[App].Fraction!.Value, precision: 3);
        Assert.Equal(new InstallProgress(InstallPhase.Queued, QueuePosition: 1), active[Other]);
    }

    [Fact]
    public void Full_size_without_completion_is_finishing()
    {
        SetQueue(App);
        WritePending(App, installSize: 100);
        WriteGameBytes(100);
        AppendLog(Queued, Started);

        Assert.Equal(InstallPhase.Finishing, Monitor().Poll().Active[App].Phase);
    }

    [Fact]
    public void Reports_outcomes_only_for_events_after_the_first_poll()
    {
        SetQueue(App, Other);
        AppendLog(Completed);
        var monitor = Monitor();

        Assert.Empty(monitor.Poll().Finished);

        AppendLog(OtherCancelled);
        SetQueue();
        var snapshot = monitor.Poll();

        Assert.Equal(InstallOutcome.Cancelled, snapshot.Finished[Other]);
        Assert.Empty(snapshot.Active);
    }

    [Fact]
    public void An_uninstall_is_not_reported_as_an_installation()
    {
        // Epic runs an uninstall through its download queue: same queue, start and completion lines.
        SetQueue();
        var monitor = Monitor();
        monitor.Poll();

        SetQueue(App);
        AppendLog(Uninstalled, Queued, Started);
        Assert.Equal(new InstallProgress(InstallPhase.Uninstalling), monitor.Poll().Active[App]);

        SetQueue();
        AppendLog(Completed);
        Assert.Equal(InstallOutcome.Uninstalled, monitor.Poll().Finished[App]);

        // A later install of the same game is an install again.
        SetQueue(App);
        AppendLog(Queued, Started);
        Assert.Equal(InstallPhase.Downloading, monitor.Poll().Active[App].Phase);
    }

    [Fact]
    public void The_log_wins_over_a_stale_queue_file()
    {
        // Owner's test, 2026-10-10: Other was moved to the front, App paused and then cancelled
        // while Other kept downloading. Epic's queue file can lag behind such a cancel.
        SetQueue();
        var monitor = Monitor();
        monitor.Poll();

        var otherStarted = $"[2026.10.10-07.40.02:620][243]LogEoshPatcher: StartTask: BinaryId [a940fa38f001486a9884640924119576:f3ac42b603f546ccbd0f0240ba120143:{Other}:1.3] — using default components: []";
        var appCancelled = $"[2026.10.10-07.40.16:135][ 57]LogDownloadManager: Cancel of active installation is requested for the {Ns}:{App}";
        AppendLog(Queued, Started, OtherQueued, Paused, otherStarted, appCancelled);
        SetQueue(App);

        var active = monitor.Poll().Active;

        Assert.Equal(InstallPhase.Downloading, active[Other].Phase);
        Assert.False(active.ContainsKey(App));
    }

    [Fact]
    public void A_leftover_pending_manifest_is_not_an_active_installation()
    {
        // After a cancel Epic keeps the pending manifest but drops the app from its queue.
        SetQueue();
        WritePending(App, installSize: 1000);
        AppendLog(Queued, Started);

        Assert.Empty(Monitor().Poll().Active);
    }

    [Fact]
    public void Missing_files_yield_an_empty_snapshot()
    {
        var missing = Path.Combine(_root, "nothing");

        var snapshot = new EpicInstallMonitor(missing, Path.Combine(missing, "x.log")).Poll();

        Assert.Empty(snapshot.Active);
        Assert.Empty(snapshot.Finished);
    }

    private EpicInstallMonitor Monitor() => new(_data, _log);

    private void SetQueue(params string[] apps) => File.WriteAllText(
        Path.Combine(_data, "DownloadManager", "DownloadState_account.json"),
        "[" + string.Join(",", apps.Select(a => $$"""{ "AppId": { "NamespaceId": "ns", "ItemId": "id", "AppName": "{{a}}" } }""")) + "]");

    private void WritePending(string app, long installSize) => File.WriteAllText(
        Path.Combine(_data, "Manifests", "Pending", app + ".item"),
        $$"""{ "AppName": "{{app}}", "InstallLocation": {{System.Text.Json.JsonSerializer.Serialize(_installDir)}}, "InstallSize": {{installSize}}, "bIsIncompleteInstall": true }""");

    private void WriteGameBytes(int count)
    {
        var staging = Path.Combine(_installDir, ".egstore", "bps");
        Directory.CreateDirectory(staging);
        File.WriteAllBytes(Path.Combine(staging, "chunk"), new byte[count]);
    }

    private void AppendLog(params string[] lines) => File.AppendAllLines(_log, lines);
}
