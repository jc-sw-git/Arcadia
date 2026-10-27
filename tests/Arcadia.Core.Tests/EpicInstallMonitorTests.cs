using Arcadia.Launchers.Abstractions;
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

    private const string OtherId = $"a940fa38f001486a9884640924119576:f3ac42b603f546ccbd0f0240ba120143:{Other}";
    private const string OtherStarted = $"[2026.10.12-17.27.27:692][853]LogEoshPatcher: StartTask: BinaryId [{OtherId}:1.3] — using default components: []";

    // Installing the Fortnite editor (UEFN) before Fortnite, owner's log 2026-10-27: Epic queues the editor as a follower
    // of Fortnite and, once Fortnite is done, downloads it in one task with Fortnite unchanged.
    private const string Fortnite = "fn:4fe75bbc5a674f4f9b356b5c90567da5:Fortnite";
    private const string Editor = "fn:1e8bda5cfbb641b9a9aea8bd62285f73:Fortnite_Studio";
    private const string EditorQueued = $"[2026.10.27-10.23.47:191][259]LogDownloadManager: Queued follower install for the {Editor}, Main App Id is {Fortnite} , Queue length is 2, Are we uninstalling 0";
    private const string FortniteStarted = $"[2026.10.27-11.55.21:533][175]LogEoshPatcher: StartTask: BinaryId [{Fortnite}:++Fortnite+Release-42.40-CL-59343437-Windows] — using validated components: [A17316661F902216B15E8DAB34ECCD71]";
    private const string FortniteCompleted = $"[2026.10.27-12.10.02:316][113]LogDownloadManager: HandleTaskComplete: AppId [{Fortnite}] AlertCode=[ok] QueueLen=4 IncompleteInstall=0 AutoResume=1";
    private const string EditorLeftQueueFile = $"[2026.10.27-12.10.02:345][113]LogDownloadManager: Not persisting follower [{Editor}], its main [{Fortnite}] is no longer queued.";
    private const string FortniteInEditorTask = $"[2026.10.27-12.10.02:928][121]LogEoshPatcher: StartTask: BinaryId [{Fortnite}:++Fortnite+Release-42.40-CL-59343437-Windows] — using validated components: [A17316661F902216B15E8DAB34ECCD71]";
    private const string EditorStarted = $"[2026.10.27-12.10.02:928][121]LogEoshPatcher: StartTask: BinaryId [{Editor}:++Fortnite+Release-42.40-CL-59343437-Windows] — using default components: []";
    private const string FortniteUnchanged = $"[2026.10.27-12.10.02:987][122]LogDownloadManager: HandlePatcherTaskBegin: Skipping OnInstallBegin broadcast for unchanged action {Fortnite}:++Fortnite+Release-42.40-CL-59343437-Windows";

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

    [Theory]
    [InlineData(EditorQueued, "Fortnite_Studio", EpicInstallEvent.Queued)]
    [InlineData(EditorLeftQueueFile, "Fortnite_Studio", EpicInstallEvent.LeftQueueFile)]
    [InlineData(FortniteUnchanged, "Fortnite", EpicInstallEvent.Unchanged)]
    public void Parses_the_lines_of_a_follower(string line, string app, EpicInstallEvent expected) =>
        Assert.Equal((app, expected), EpicInstallMonitor.ParseLogLine(line));

    [Fact]
    public void A_follower_downloading_after_its_main_app_is_open_without_the_queue_file_and_its_main_app_is_done()
    {
        // Epic's queue file was empty while the editor downloaded; Arcadia, restarted then, showed nothing (owner, 2026-10-27).
        SetQueue();
        WritePending("Fortnite_Studio", installSize: 1000, mainGame: "Fortnite");
        WriteGameBytes(5000);
        AppendLog(EditorQueued, FortniteStarted, FortniteCompleted, EditorLeftQueueFile, FortniteInEditorTask, EditorStarted, FortniteUnchanged);

        var active = Monitor().Poll().Active;

        // In its game's folder, which is full already: no fraction, so not "Fast fertig".
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Assert.Single(active, entry => entry.Key == "Fortnite_Studio").Value);
        Assert.DoesNotContain("Fortnite", active.Keys);
    }

    [Fact]
    public void A_follower_seen_live_downloads_without_a_fraction_and_its_main_app_is_done()
    {
        SetQueue("Fortnite", "Fortnite_Studio");
        var monitor = Monitor();
        monitor.Poll();
        WritePending("Fortnite_Studio", installSize: 1000, mainGame: "Fortnite");
        WriteGameBytes(5000);
        AppendLog(EditorQueued, FortniteStarted, FortniteCompleted);
        SetQueue();
        AppendLog(EditorLeftQueueFile, FortniteInEditorTask, EditorStarted, FortniteUnchanged);

        var snapshot = monitor.Poll();

        Assert.Equal(new InstallProgress(InstallPhase.Downloading), snapshot.Active["Fortnite_Studio"]);
        Assert.DoesNotContain("Fortnite", snapshot.Active.Keys);
        Assert.Equal(InstallOutcome.Completed, snapshot.Finished["Fortnite"]);
    }

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

    [Theory]
    [InlineData(App)]
    [InlineData("")]
    public void A_game_naming_itself_or_nothing_as_main_game_has_a_fraction(string mainGame)
    {
        // Both seen in real manifests: older ones name the game itself, the owner's of 2026-10-27 nothing.
        SetQueue(App);
        WritePending(App, installSize: 1000, mainGame);
        WriteGameBytes(250);
        AppendLog(Queued, Started);

        Assert.Equal(0.25, Monitor().Poll().Active[App].Fraction!.Value, precision: 3);
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
    public void An_update_downloads_without_a_fraction()
    {
        // Terraforming Mars on 2026-10-12: the directory was full from the start and showed "almost done".
        File.WriteAllText(Path.Combine(_data, "Manifests", "installed.item"), $$"""{ "AppName": "{{App}}", "bIsIncompleteInstall": false }""");
        SetQueue(App);
        WritePending(App, installSize: 100);
        WriteGameBytes(100);
        AppendLog(Queued, Started);

        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Monitor().Poll().Active[App]);
    }

    [Fact]
    public void An_incomplete_install_manifest_is_no_update()
    {
        File.WriteAllText(Path.Combine(_data, "Manifests", "installed.item"), $$"""{ "AppName": "{{App}}", "bIsIncompleteInstall": true }""");
        SetQueue(App);
        WritePending(App, installSize: 1000);
        WriteGameBytes(250);
        AppendLog(Queued, Started);

        Assert.Equal(0.25, Monitor().Poll().Active[App].Fraction!.Value, precision: 3);
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
    public void Resuming_another_app_in_epic_pauses_the_running_one()
    {
        // Owner's log, 2026-10-12: resuming Other while App downloaded made Epic cancel App's
        // operation and start Other, without a pause line for App.
        SetQueue(App, Other);
        WritePending(App, installSize: 1000);
        WriteGameBytes(400);
        var otherResumed = $"[2026.10.12-17.27.27:012][812]LogDownloadManager: Resume of active installation is requested for the {OtherId}";
        AppendLog(Queued, Started, OtherQueued, otherResumed, OtherStarted);
        var monitor = Monitor();

        var active = monitor.Poll().Active;
        Assert.Equal(InstallPhase.Downloading, active[Other].Phase);
        Assert.Equal(InstallPhase.Paused, active[App].Phase);
        Assert.Equal(0.4, active[App].Fraction!.Value, precision: 3);

        // Pausing Other leaves nothing running; Epic doesn't fall back to App on its own.
        AppendLog($"[2026.10.12-17.28.04:318][100]LogDownloadManager: Pause is requested for the {OtherId}");
        active = monitor.Poll().Active;
        Assert.Equal(InstallPhase.Paused, active[Other].Phase);
        Assert.Equal(InstallPhase.Paused, active[App].Phase);
    }

    [Fact]
    public void An_uninstall_does_not_pause_the_running_download()
    {
        SetQueue(App, Other);
        var otherUninstalled = $"[2026.10.12-17.25.11:723][274]LogCommunityPortalAppManager: FCommunityPortalAppManagerImpl: Uninstall {OtherId}";
        AppendLog(Queued, Started, otherUninstalled, OtherQueued, OtherStarted);

        var active = Monitor().Poll().Active;

        Assert.Equal(InstallPhase.Downloading, active[App].Phase);
        Assert.Equal(InstallPhase.Uninstalling, active[Other].Phase);
    }

    [Fact]
    public void A_finished_download_still_moving_files_is_not_paused_by_the_next_start()
    {
        SetQueue(App, Other);
        WritePending(App, installSize: 100);
        WriteGameBytes(100);
        AppendLog(Queued, Started, OtherQueued, OtherStarted);

        Assert.Equal(InstallPhase.Finishing, Monitor().Poll().Active[App].Phase);
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

    private void WritePending(string app, long installSize, string? mainGame = null) => File.WriteAllText(
        Path.Combine(_data, "Manifests", "Pending", app + ".item"),
        $$"""{ "AppName": "{{app}}", "InstallLocation": {{System.Text.Json.JsonSerializer.Serialize(_installDir)}}, "InstallSize": {{installSize}}, "bIsIncompleteInstall": true, "MainGameAppName": "{{mainGame ?? app}}" }""");

    private void WriteGameBytes(int count)
    {
        var staging = Path.Combine(_installDir, ".egstore", "bps");
        Directory.CreateDirectory(staging);
        File.WriteAllBytes(Path.Combine(staging, "chunk"), new byte[count]);
    }

    private void AppendLog(params string[] lines) => File.AppendAllLines(_log, lines);
}
