using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>Replays the manifest states seen on the owner's PC on 2026-10-12 (Age of Empires II, System Shock 2).</summary>
public sealed class SteamInstallMonitorTests : IDisposable
{
    private const string AgeOfEmpires = "221380";
    private const string SystemShock = "238210";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-steammon-" + Guid.NewGuid().ToString("N"));
    private readonly string _steamApps;
    private readonly FakeTimeProvider _time = new();
    private readonly SteamInstallMonitor _monitor;

    public SteamInstallMonitorTests()
    {
        _steamApps = Path.Combine(_root, "steamapps");
        Directory.CreateDirectory(_steamApps);
        _monitor = new SteamInstallMonitor(() => new SteamInstallation(_root, null), _time);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string appId, int flags, long downloaded = 0, long staged = 0, long buildId = 0, string name = "Game", long toDownload = 2571409488, long toStage = 4956889523) =>
        File.WriteAllText(Path.Combine(_steamApps, $"appmanifest_{appId}.acf"), $$"""
            "AppState"
            {
            	"appid"		"{{appId}}"
            	"name"		"{{name}}"
            	"StateFlags"		"{{flags}}"
            	"BytesToDownload"		"{{toDownload}}"
            	"BytesDownloaded"		"{{downloaded}}"
            	"BytesToStage"		"{{toStage}}"
            	"BytesStaged"		"{{staged}}"
            	"buildid"		"{{buildId}}"
            }
            """);

    private string ContentLogPath => Path.Combine(_root, "logs", "content_log.txt");

    private void AppendContentLog(params string[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ContentLogPath)!);
        File.AppendAllLines(ContentLogPath, lines);
    }

    private void Delete(string appId) => File.Delete(Path.Combine(_steamApps, $"appmanifest_{appId}.acf"));

    private InstallSnapshot Poll()
    {
        _time.Advance(TimeSpan.FromSeconds(2));
        return _monitor.Poll();
    }

    [Fact]
    public void Follows_a_new_install_from_queue_to_completion()
    {
        Poll();
        Write(AgeOfEmpires, flags: 2);
        Assert.Equal(InstallPhase.Queued, Poll().Active[AgeOfEmpires].Phase);

        Write(AgeOfEmpires, flags: 1026);
        Assert.Equal(InstallPhase.Queued, Poll().Active[AgeOfEmpires].Phase);

        // Steam writes the counters only on a state change, so a running download shows no (stale) number.
        Write(AgeOfEmpires, flags: 1026, downloaded: 418549120, staged: 672606034);
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active[AgeOfEmpires]);

        // Paused: the counters are fresh; download bytes only, as Steam's download page counts them.
        Write(AgeOfEmpires, flags: 1538, downloaded: 989891104, staged: 1482440292);
        var paused = Poll().Active[AgeOfEmpires];
        Assert.Equal(InstallPhase.Paused, paused.Phase);
        Assert.Equal(989891104d / 2571409488, paused.Fraction!.Value, 6);

        Write(AgeOfEmpires, flags: 1026, downloaded: 2571409488, staged: 4956889523);
        Assert.Equal(InstallPhase.Finishing, Poll().Active[AgeOfEmpires].Phase);

        Write(AgeOfEmpires, flags: 4, buildId: 3062235);
        var done = Poll();
        Assert.Empty(done.Active);
        Assert.Equal(InstallOutcome.Completed, done.Finished[AgeOfEmpires]);
    }

    [Fact]
    public void Only_the_app_whose_bytes_moved_last_is_downloading()
    {
        Poll();
        Write(AgeOfEmpires, flags: 1026, downloaded: 100);
        Write(SystemShock, flags: 1026);
        Poll();
        Write(AgeOfEmpires, flags: 1026, downloaded: 200);
        Assert.Equal(InstallPhase.Downloading, Poll().Active[AgeOfEmpires].Phase);

        Write(SystemShock, flags: 1026, downloaded: 50);
        var snapshot = Poll();
        Assert.Equal(InstallPhase.Downloading, snapshot.Active[SystemShock].Phase);
        Assert.Equal(InstallPhase.Queued, snapshot.Active[AgeOfEmpires].Phase);
    }

    [Fact]
    public void Cancelling_a_new_install_deletes_its_manifest()
    {
        Poll();
        Write(SystemShock, flags: 1026, downloaded: 43036528);
        Poll();
        Delete(SystemShock);

        Assert.Equal(InstallOutcome.Cancelled, Poll().Finished[SystemShock]);
    }

    [Fact]
    public void Uninstalling_an_installed_game_is_reported()
    {
        Write(AgeOfEmpires, flags: 4, buildId: 3062235);
        Poll();
        Write(AgeOfEmpires, flags: 38, buildId: 3062235);
        Assert.Empty(Poll().Finished);

        Delete(AgeOfEmpires);
        Assert.Equal(InstallOutcome.Uninstalled, Poll().Finished[AgeOfEmpires]);
    }

    [Fact]
    public void Pending_updates_tools_and_known_state_at_start_are_no_news()
    {
        Write("960090", flags: 6, buildId: 24829026);
        Write(AgeOfEmpires, flags: 4, buildId: 3062235);
        Write("228980", flags: 1030, buildId: 24098430, name: "Steamworks Common Redistributables");

        var first = _monitor.Poll();

        Assert.Empty(first.Active);
        Assert.Empty(first.Finished);
        Write("228980", flags: 4, buildId: 24098430, name: "Steamworks Common Redistributables");
        Assert.Empty(Poll().Finished);
    }

    [Fact]
    public void A_running_update_of_an_installed_game_counts_as_install()
    {
        Write(AgeOfEmpires, flags: 4, buildId: 3062235);
        Poll();
        Write(AgeOfEmpires, flags: 1030, buildId: 3062235);
        Assert.Contains(AgeOfEmpires, Poll().Active.Keys);
        Write(AgeOfEmpires, flags: 4, buildId: 3062236);
        Assert.Equal(InstallOutcome.Completed, Poll().Finished[AgeOfEmpires]);
    }

    [Fact]
    public void The_content_log_names_the_downloading_app_before_its_bytes_move()
    {
        // Bloons TD 6 on 2026-10-12: started through Arcadia, Arcadia showed it waiting while it downloaded.
        Write("960090", flags: 6, buildId: 24829026);
        Poll();
        Write("960090", flags: 1030, buildId: 24829026);
        AppendContentLog(
            "[2026-10-12 20:43:01] AppID 960090 state changed : Update Required,Fully Installed,Update Queued, (Update delayed for 639109 secs)",
            "[2026-10-12 20:43:01] AppID 960090 state changed : Update Required,Fully Installed,Update Queued,Update Running,",
            "[2026-10-12 20:43:03] AppID 960090 state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            "[2026-10-12 20:43:03] AppID 960090 App update changed : Running Update,Downloading,Staging,");
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active["960090"]);

        AppendContentLog("[2026-10-12 20:45:02] AppID 960090 App update changed : Running Update,Committing,");
        Assert.Equal(InstallPhase.Finishing, Poll().Active["960090"].Phase);

        Write("960090", flags: 4, buildId: 25664134);
        AppendContentLog("[2026-10-12 20:45:03] AppID 960090 state changed : Fully Installed,");
        Assert.Equal(InstallOutcome.Completed, Poll().Finished["960090"]);
    }

    [Fact]
    public void A_queued_app_without_update_running_waits_even_when_the_log_exists()
    {
        Poll();
        Write(AgeOfEmpires, flags: 1026);
        Write(SystemShock, flags: 1026);
        AppendContentLog(
            "[2026-10-12 17:50:00] AppID 221380 state changed : Update Required,Update Queued,Update Started,",
            "[2026-10-12 17:50:00] AppID 238210 state changed : Update Required,Update Queued,Update Running,Update Started,");
        var active = Poll().Active;
        Assert.Equal(InstallPhase.Queued, active[AgeOfEmpires].Phase);
        Assert.Equal(InstallPhase.Downloading, active[SystemShock].Phase);
    }

    [Fact]
    public void The_content_log_survives_steam_starting_a_new_file()
    {
        var log = new SteamContentLog(ContentLogPath);
        AppendContentLog("[2026-10-12 20:43:01] AppID 960090 state changed : Update Required,Fully Installed,Update Queued,Update Running,");
        Assert.True(log.Update());
        Assert.Equal(new SteamContentState(SteamDownloadPhase.Preparing), log.StateOf(960090));

        File.WriteAllText(ContentLogPath, "[2026-10-12 20:50:00] AppID 1 state changed : Fully Installed,\n");
        Assert.True(log.Update());
        Assert.True(log.StateOf(960090)!.IsRunning);
        Assert.Null(log.StateOf(2));

        AppendContentLog("[2026-10-12 20:50:01] AppID 960090 update canceled : Disabled (Suspended)");
        Assert.True(log.Update());
        Assert.Equal(SteamDownloadPhase.Paused, log.StateOf(960090)!.Phase);
    }

    // #278: the owner's content log of 2026-10-27, three new installs (253230, 792100, 1098080) and an account switch.
    private const string Big = "253230";
    private const string Small = "792100";
    private const long BigDownload = 5899237456;
    private const long BigStage = 7846833278;

    private void WriteBig(int flags = 1026, long downloaded = 0) =>
        Write(Big, flags, downloaded, toDownload: BigDownload, toStage: BigStage);

    private void WriteSmall(int flags = 1026, long downloaded = 0) =>
        Write(Small, flags, downloaded, toDownload: 228935584, toStage: 237717350);

    [Fact]
    public void A_start_shows_at_once_as_downloading_without_a_number_while_steam_preallocates()
    {
        Write(AgeOfEmpires, flags: 6, buildId: 3062235);
        Poll();
        AppendContentLog(
            "[2026-10-27 17:38:53] AppID 221380 state changed : Update Required,Fully Installed,Update Queued,Update Running,",
            "[2026-10-27 17:38:53] AppID 221380 App update changed : Running Update,Preallocating,");

        // The manifest still says "update pending"; the log already knows Steam works on it.
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active[AgeOfEmpires]);

        AppendContentLog(
            "[2026-10-27 17:38:53] AppID 221380 update started : download 0/2571409488, store 0/0, reuse 0/0, delta 0/0, stage 0/4956889523 ",
            "[2026-10-27 17:38:53] AppID 221380 state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            "[2026-10-27 17:38:53] AppID 221380 App update changed : Running Update,Downloading,Staging,");
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active[AgeOfEmpires]);
    }

    [Fact]
    public void A_pause_in_steam_shows_as_paused_with_steams_number_and_a_resume_as_running()
    {
        WriteBig();
        AppendContentLog(
            "[2026-10-27 17:44:44] AppID 253230 update started : download 107329968/5899237456, store 0/0, reuse 0/0, delta 0/0, stage 198841577/7846833278 ",
            "[2026-10-27 17:44:44] AppID 253230 App update changed : Running Update,Downloading,Staging,");
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active[Big]);

        AppendContentLog(
            "[2026-10-27 17:44:52] AppID 253230 update canceled : Disabled (Suspended)",
            "[2026-10-27 17:44:52] AppID 253230 App update changed : Running Update,Downloading,Staging,Stopping,",
            "[2026-10-27 17:44:52] AppID 253230 App update changed : None",
            "[2026-10-27 17:44:52] AppID 253230 state changed : Update Required,Update Queued,Update Started, (Suspended)");
        var paused = Poll().Active[Big];
        Assert.Equal(InstallPhase.Paused, paused.Phase);
        Assert.Equal(107329968d / BigDownload, paused.Fraction!.Value, 6);

        // Steam's pause also rewrites the manifest: its counters are newer than the log's start.
        WriteBig(downloaded: 277958000);
        Assert.Equal(277958000d / BigDownload, Poll().Active[Big].Fraction!.Value, 6);

        AppendContentLog(
            "[2026-10-27 17:47:00] AppID 253230 state changed : Update Required,Update Queued,Update Running,Update Started,",
            "[2026-10-27 17:47:00] AppID 253230 App update changed : Running Update,Preallocating,");
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), Poll().Active[Big]);
    }

    [Fact]
    public void A_download_another_one_took_over_waits_in_the_queue()
    {
        WriteBig();
        WriteSmall();
        AppendContentLog(
            "[2026-10-27 17:38:53] AppID 253230 update started : download 0/5899237456, store 0/0, reuse 0/0, delta 0/0, stage 0/7846833278 ",
            "[2026-10-27 17:38:53] AppID 253230 state changed : Update Required,Update Queued,Update Running,Update Started,",
            "[2026-10-27 17:39:00] AppID 792100 state changed : Update Required,Update Queued,",
            "[2026-10-27 17:39:00] AppID 253230 update canceled : Priority (Suspended)",
            "[2026-10-27 17:39:00] AppID 253230 state changed : Update Required,Update Queued,Update Started, (Suspended)",
            "[2026-10-27 17:39:00] AppID 792100 state changed : Update Required,Update Queued,Update Running,",
            "[2026-10-27 17:39:03] AppID 792100 App update changed : Running Update,Preallocating,");
        WriteBig(downloaded: 105898736);
        var active = Poll().Active;
        Assert.Equal(InstallPhase.Queued, active[Big].Phase);
        Assert.Equal(105898736d / BigDownload, active[Big].Fraction!.Value, 6);
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), active[Small]);

        AppendContentLog(
            "[2026-10-27 17:39:11] AppID 792100 update canceled : Priority (Suspended)",
            "[2026-10-27 17:39:11] AppID 792100 state changed : Update Required,Update Queued,Update Started, (Suspended)",
            "[2026-10-27 17:39:11] AppID 253230 state changed : Update Required,Update Queued,Update Running,Update Started,",
            "[2026-10-27 17:39:11] AppID 253230 update started : download 105898736/5899237456, store 0/0, reuse 0/0, delta 0/0, stage 196124119/7846833278 ");
        active = Poll().Active;
        Assert.Equal(InstallPhase.Queued, active[Small].Phase);
        Assert.Equal(new InstallProgress(InstallPhase.Downloading), active[Big]);
    }

    [Fact]
    public void After_an_account_switch_the_downloads_of_the_previous_account_are_paused()
    {
        WriteBig();
        WriteSmall();
        AppendContentLog(
            "[2026-10-27 17:44:39] AppID 792100 state changed : Update Required,Update Queued,Update Started,",
            "[2026-10-27 17:44:44] AppID 253230 update started : download 107329968/5899237456, store 0/0, reuse 0/0, delta 0/0, stage 198841577/7846833278 ",
            "[2026-10-27 17:44:44] AppID 253230 App update changed : Running Update,Downloading,Staging,");
        Poll();

        // The logoff suspends the running download in the same second; the other account's session then says nothing about them.
        AppendContentLog(
            "[2026-10-27 17:44:52] AppID 253230 update canceled : Disabled (Suspended)",
            "[2026-10-27 17:44:52] AppID 253230 state changed : Update Required,Update Queued,Update Started, (Suspended)",
            "[2026-10-27 17:44:54] Client version: 1793056502",
            "[2026-10-27 17:44:54] Loaded 18 apps from install folder \"G:\\SteamLibrary\\steamapps\\appmanifest_*.acf\".");
        var active = Poll().Active;
        Assert.Equal(InstallPhase.Paused, active[Big].Phase);
        Assert.Equal(InstallPhase.Paused, active[Small].Phase);

        // Back on the owner's account Steam resumes them.
        AppendContentLog(
            "[2026-10-27 17:46:59] Client version: 1793056502",
            "[2026-10-27 17:47:00] AppID 792100 state changed : Update Required,Update Queued,Update Started,",
            "[2026-10-27 17:47:00] AppID 253230 state changed : Update Required,Update Queued,Update Running,Update Started,",
            "[2026-10-27 17:47:00] AppID 253230 App update changed : Running Update,Preallocating,");
        active = Poll().Active;
        Assert.Equal(InstallPhase.Downloading, active[Big].Phase);
        Assert.Equal(InstallPhase.Queued, active[Small].Phase);
    }

    [Fact]
    public void Committing_shows_as_finishing_and_the_written_manifest_as_completed()
    {
        WriteSmall();
        AppendContentLog(
            "[2026-10-27 21:00:42] AppID 792100 update started : download 141347840/228935584, store 0/0, reuse 0/0, delta 0/0, stage 147613394/237717350 ",
            "[2026-10-27 21:00:49] AppID 792100 App update changed : Running Update,Committing,",
            "[2026-10-27 21:00:49] AppID 792100 state changed : Fully Installed,Update Queued,Update Running,");
        Assert.Equal(InstallPhase.Finishing, Poll().Active[Small].Phase);

        AppendContentLog(
            "[2026-10-27 21:00:49] AppID 792100 finished update, 2 mounted depots (BuildID 8016557) : 792101 (5052600457178655633),",
            "[2026-10-27 21:00:49] AppID 792100 state changed : Fully Installed,Update Queued,");
        Assert.Equal(InstallPhase.Finishing, Poll().Active[Small].Phase);

        Write(Small, flags: 4, buildId: 8016557);
        Assert.Equal(InstallOutcome.Completed, Poll().Finished[Small]);
    }

    [Fact]
    public void The_check_run_pause_matches_steam_and_the_uninstall_ends_it()
    {
        // The #278 check run (owner's PC, 2026-10-27): Steam showed 2 % while paused; the manifest got the bytes at the pause.
        const string App = "1097150";
        Write(App, flags: 1026, toDownload: 6972784416, toStage: 7888336863);
        AppendContentLog(
            "[2026-10-27 23:30:20] AppID 1097150 App update changed : Running Update,Preallocating,",
            "[2026-10-27 23:30:20] AppID 1097150 update started : download 0/6972784416, store 0/0, reuse 0/0, delta 0/0, stage 0/7888336863 ",
            "[2026-10-27 23:30:20] AppID 1097150 App update changed : Running Update,Downloading,Staging,",
            "[2026-10-27 23:32:16] AppID 1097150 update canceled : Disabled (Suspended)",
            "[2026-10-27 23:32:16] AppID 1097150 App update changed : Running Update,Downloading,Staging,Stopping,",
            "[2026-10-27 23:32:16] AppID 1097150 App update changed : None",
            "[2026-10-27 23:32:16] AppID 1097150 state changed : Update Required,Update Queued,Update Started, (Suspended)");
        Write(App, flags: 1026, downloaded: 139259776, staged: 183162788, toDownload: 6972784416, toStage: 7888336863);
        var paused = Poll().Active[App];
        Assert.Equal(InstallPhase.Paused, paused.Phase);
        Assert.Equal(0.02, paused.Fraction!.Value, 2);

        AppendContentLog(
            "[2026-10-27 23:34:39] AppID 1097150 state changed : Update Required,Update Queued,Uninstalling,",
            "[2026-10-27 23:34:39] AppID 1097150 state changed : Uninstalled,",
            "[2026-10-27 23:34:39] AppID 1097150 finished uninstall (No Error)");
        Delete(App);
        Assert.Equal(InstallOutcome.Cancelled, Poll().Finished[App]);
    }

    [Fact]
    public void The_next_update_does_not_show_the_bytes_of_the_last_one()
    {
        var log = new SteamContentLog(ContentLogPath);
        log.Apply([
            "[2026-10-27 21:00:42] AppID 792100 update started : download 141347840/228935584, store 0/0, reuse 0/0, delta 0/0, stage 147613394/237717350 ",
            "[2026-10-27 21:00:49] AppID 792100 finished update, 2 mounted depots (BuildID 8016557) : 792101 (5052600457178655633),",
            "[2026-10-28 18:00:00] AppID 792100 state changed : Update Required,Fully Installed,Update Queued,Update Started,"]);

        Assert.Equal(new SteamContentState(SteamDownloadPhase.Queued, Started: true), log.StateOf(792100));
    }

    [Fact]
    public void A_paused_update_removed_from_steams_queue_is_no_installation_any_more()
    {
        Write(AgeOfEmpires, flags: 1030, buildId: 3062235);
        AppendContentLog(
            "[2026-10-27 17:44:44] AppID 221380 update started : download 100/2571409488, store 0/0, reuse 0/0, delta 0/0, stage 100/4956889523 ",
            "[2026-10-27 17:44:52] AppID 221380 update canceled : Disabled (Suspended)",
            "[2026-10-27 17:44:52] AppID 221380 state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)");
        Assert.Equal(InstallPhase.Paused, Poll().Active[AgeOfEmpires].Phase);

        Write(AgeOfEmpires, flags: 6, buildId: 3062235);
        AppendContentLog("[2026-10-27 17:45:10] AppID 221380 state changed : Update Required,Fully Installed,");
        var snapshot = Poll();
        Assert.Empty(snapshot.Active);
        Assert.Equal(InstallOutcome.Cancelled, snapshot.Finished[AgeOfEmpires]);
    }

    [Fact]
    public void A_scheduled_update_nobody_started_is_no_installation()
    {
        Write("960090", flags: 6, buildId: 24829026);
        AppendContentLog(
            "[2026-10-12 20:43:01] AppID 960090 state changed : Update Required,Fully Installed,Update Queued, (Update delayed for 639109 secs)",
            "[2026-10-27 17:42:50] AppID 960090 update prefetch finished : 0 bytes to download, 0 bytes to stage");

        Assert.Empty(Poll().Active);
    }

    [Fact]
    public void A_dropped_download_is_over_even_before_steam_deletes_the_manifest()
    {
        Write(AgeOfEmpires, flags: 6, buildId: 3062235);
        Poll();
        AppendContentLog(
            "[2026-10-27 17:39:11] AppID 221380 update started : download 0/2571409488, store 0/0, reuse 0/0, delta 0/0, stage 0/4956889523 ",
            "[2026-10-27 17:39:12] AppID 221380 update canceled : Uninstall (Canceled)");

        Assert.Empty(Poll().Active);
    }

    [Fact]
    public void Broken_manifests_and_missing_steam_are_ignored()
    {
        File.WriteAllText(Path.Combine(_steamApps, "appmanifest_1.acf"), "\"AppState\" {");
        Assert.Empty(Poll().Active);
        Assert.Empty(new SteamInstallMonitor(() => null, _time).Poll().Active);
        Assert.Null(SteamInstallMonitor.ParseState("\"AppState\" { \"appid\" \"x\" }"));
    }
}
