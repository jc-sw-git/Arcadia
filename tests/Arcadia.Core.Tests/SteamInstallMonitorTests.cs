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

        Write(AgeOfEmpires, flags: 1026, downloaded: 418549120, staged: 672606034);
        var downloading = Poll().Active[AgeOfEmpires];
        Assert.Equal(InstallPhase.Downloading, downloading.Phase);
        Assert.Equal((418549120d + 672606034) / (2571409488d + 4956889523), downloading.Fraction!.Value, 6);

        Write(AgeOfEmpires, flags: 1538, downloaded: 989891104, staged: 1482440292);
        Assert.Equal(InstallPhase.Paused, Poll().Active[AgeOfEmpires].Phase);

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
        Assert.Equal(new SteamContentState(true, false), log.StateOf(960090));

        File.WriteAllText(ContentLogPath, "[2026-10-12 20:50:00] AppID 1 state changed : Fully Installed,\n");
        Assert.True(log.Update());
        Assert.True(log.StateOf(960090)!.IsRunning);
        Assert.Null(log.StateOf(2));
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
