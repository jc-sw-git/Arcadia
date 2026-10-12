using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests;

public sealed class EpicLauncherLogTests : IDisposable
{
    // Lines in the shape of the owner's Epic log (2026-10-09 and 2026-10-12, Loop Hero and Chess Ultra).
    private const string LoopHero = "f7a0ebb44f93430fb1c4388a395eba96";
    private const string Chess = "fc0b13b7b17b4a46933756fb2786cdc4";
    private const string LoopHeroStarted = $"[2026.10.09-15.57.42:000][813]LogPortalPublishTime: Started tracking playtime for ff50f85ed609454e80ac46d9496da34d:9c7c10e8e1a648f8a9e35f28a1d45900:{LoopHero}";
    private const string LoopHeroStopped = $"[2026.10.09-15.58.02:910][225]LogPortalPublishTime: Stopped tracking playtime for ff50f85ed609454e80ac46d9496da34d:9c7c10e8e1a648f8a9e35f28a1d45900:{LoopHero}";
    private const string ChessStarted = $"[2026.10.12-17.40.48:340][256]LogPortalPublishTime: Started tracking playtime for 0e9ae0e756524ed49a27435c9c3c3e1c:9a84503f4c494b91b565ba67f00c4fb1:{Chess}";

    private static string InstalledApps(string app, string version, bool hasUpdate) =>
        "[2026.10.12-17.43.09:188][ 12]LogEOSNotificationBridge: Sending response: "
        + "{\"feature\":\"productinfo\",\"name\":\"get-installed-apps-response\",\"payload\":{\"products\":[{\"namespace\":\"ns\",\"appName\":\""
        + app + "\",\"isInstalled\":true,\"installedVersion\":\"" + version + "\",\"hasUpdate\":" + (hasUpdate ? "true" : "false")
        + ",\"installLocation\":\"G:\\\\EpicGames\\\\TerraformingMars\"}]}}";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-epiclog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Reads_play_sessions_in_utc()
    {
        var facts = EpicLauncherLog.Parse([LoopHeroStarted, "noise", LoopHeroStopped, ChessStarted]);

        Assert.Equal(
            [
                new EpicPlaySession(LoopHero, new DateTimeOffset(2026, 10, 9, 15, 57, 42, TimeSpan.Zero), new DateTimeOffset(2026, 10, 9, 15, 58, 2, TimeSpan.Zero)),
                new EpicPlaySession(Chess, new DateTimeOffset(2026, 10, 12, 17, 40, 48, TimeSpan.Zero), null),
            ],
            facts.Sessions);
        Assert.Null(facts.InstalledApps);
    }

    [Fact]
    public void Keeps_the_last_installed_apps_report()
    {
        var facts = EpicLauncherLog.Parse([InstalledApps("tm", "1.0", hasUpdate: false), InstalledApps("tm", "2.5", hasUpdate: true), "x Sending response: {\"name\":\"get-installed-apps-response\", broken"]);

        Assert.Equal(new EpicInstalledAppState("2.5", true), facts.InstalledApps!["tm"]);
    }

    [Theory]
    [InlineData("Windows_2.5.1", true, "Windows_2.5.1", true)]
    [InlineData("Windows_2.5.1", false, "Windows_2.5.1", false)]
    [InlineData("Windows_2.5.1", true, "Windows_2.6.0", false)]
    [InlineData(null, true, "Windows_2.5.1", false)]
    public void An_update_counts_only_for_the_reported_version(string? reportedVersion, bool hasUpdate, string installedVersion, bool expected)
    {
        var report = reportedVersion is null
            ? null
            : new Dictionary<string, EpicInstalledAppState> { ["tm"] = new(reportedVersion, hasUpdate) };

        Assert.Equal(expected, EpicScanner.HasUpdate(report, "tm", installedVersion));
    }

    [Fact]
    public void Remembers_last_played_beyond_the_logs()
    {
        var path = Path.Combine(_root, "last-played.json");
        var start = new DateTimeOffset(2026, 10, 9, 15, 57, 42, TimeSpan.Zero);
        new EpicLastPlayedStore(path).Merge([new EpicPlaySession(LoopHero, start, null), new EpicPlaySession(LoopHero, start.AddDays(-1), null)]);

        var later = new EpicLastPlayedStore(path).Merge([]);

        Assert.Equal(start, later[LoopHero]);
    }

    [Fact]
    public void A_broken_last_played_file_starts_over()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "last-played.json");
        File.WriteAllText(path, "{ not json");

        Assert.Empty(new EpicLastPlayedStore(path).Merge([]));
    }

    [Fact]
    public async Task Scanner_takes_last_played_and_updates_from_the_logs()
    {
        var manifests = Path.Combine(_root, "Manifests");
        var logs = Path.Combine(_root, "Logs");
        Directory.CreateDirectory(manifests);
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(manifests, "a.item"), $$"""
            { "DisplayName": "Loop Hero", "AppName": "{{LoopHero}}", "AppVersionString": "11054", "CatalogNamespace": "ns", "CatalogItemId": "item", "InstallLocation": "G:\\EpicGames\\LoopHero" }
            """);
        var backup = Path.Combine(logs, "EpicGamesLauncher-backup-2026.10.09-22.18.28.log");
        File.WriteAllLines(backup, [LoopHeroStarted, LoopHeroStopped]);
        File.SetLastWriteTimeUtc(backup, new DateTime(2026, 10, 9, 22, 18, 28, DateTimeKind.Utc));
        File.WriteAllLines(Path.Combine(logs, "EpicGamesLauncher.log"), [InstalledApps(LoopHero, "11054", hasUpdate: true)]);

        var scanner = new EpicScanner(manifests, Path.Combine(_root, "catcache.bin"), logs, new EpicLastPlayedStore(Path.Combine(_root, "last-played.json")));
        var game = Assert.Single(await scanner.ScanAsync());

        Assert.True(game.UpdateAvailable);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 15, 57, 42, TimeSpan.Zero), game.LastPlayedAt);
        Assert.Null(game.Playtime);
    }
}
