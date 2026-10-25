using System.Globalization;
using System.Text;
using System.Text.Json;
using Arcadia.Launchers.Epic;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// A game in Epic's files: its app name (Epic's id), the catalog ids and its title. Made up; the format of the files is in
/// <c>docs/launchers/epic.md</c>.
/// </summary>
internal sealed record EpicApp(string AppName, string Title)
{
    public string Namespace => $"ns{AppName}";

    public string CatalogItemId => $"item{AppName}";

    /// <summary>The id Epic's log names the app by.</summary>
    public string LogId => $"{Namespace}:{CatalogItemId}:{AppName}";

    public string Version { get; init; } = "1.0.0";

    public long InstallSize { get; init; } = 1000;
}

/// <summary>
/// Epic's files as on the owner's PC, built from nothing: the data folder (<c>Manifests\*.item</c>, <c>Manifests\Pending</c>,
/// <c>Catalog\catcache.bin</c>, <c>DownloadManager\DownloadState_*.json</c>), the launcher's logs, the install folders, and
/// Arcadia's own file of last starts. Nothing exists until <see cref="Install"/>.
/// </summary>
internal sealed class EpicFolder(string root)
{
    private int _logClock;

    public EpicFolders Folders { get; } = new(
        Path.Combine(root, "ProgramData", "Data"),
        Path.Combine(root, "LocalAppData", "Logs"),
        Path.Combine(root, "Arcadia", "epic-last-played.json"));

    private string Manifests => Folders.ManifestDirectory;

    /// <summary>The Epic Games Launcher is installed: its data folder and an empty log exist.</summary>
    public EpicFolder Install()
    {
        Directory.CreateDirectory(Manifests);
        Directory.CreateDirectory(Folders.Logs);
        File.WriteAllText(Folders.CurrentLogPath, string.Empty);
        return this;
    }

    public string InstallLocation(EpicApp app) => Path.Combine(root, "Games", app.AppName);

    /// <summary>The game is installed: its manifest and a full install folder.</summary>
    public void WriteInstalled(EpicApp app)
    {
        WriteGameBytes(app, app.InstallSize);
        File.WriteAllText(Path.Combine(Manifests, $"{app.AppName}.item"), Manifest(app));
    }

    /// <summary>A DLC's manifest names its game; Epic installs it into the game's folder.</summary>
    public void WriteInstalledDlc(EpicApp dlc, EpicApp game) =>
        File.WriteAllText(Path.Combine(Manifests, $"{dlc.AppName}.item"), Manifest(dlc, mainGame: game.AppName, location: InstallLocation(game)));

    /// <summary>Uninstalled in Epic: the manifest and the folder are gone.</summary>
    public void Uninstall(EpicApp app)
    {
        File.Delete(Path.Combine(Manifests, $"{app.AppName}.item"));
        Directory.Delete(InstallLocation(app), recursive: true);
    }

    /// <summary>
    /// The launcher's catalog cache: Base64 of a JSON array, one item per game, DLC, add-on … of the library.
    /// <paramref name="categories"/> are the item's category paths; <paramref name="mainGame"/> is set for DLC and season passes.
    /// </summary>
    public void WriteCatalog(params (EpicApp App, string[] Categories, EpicApp? MainGame)[] items)
    {
        var json = JsonSerializer.Serialize(items.Select(item => new Dictionary<string, object?>
        {
            ["id"] = item.App.CatalogItemId,
            ["namespace"] = item.App.Namespace,
            ["title"] = item.App.Title,
            ["categories"] = item.Categories.Select(path => new { path }).ToArray(),
            ["mainGameItem"] = item.MainGame is { } main ? new { id = main.CatalogItemId, @namespace = main.Namespace } : null,
            ["releaseInfo"] = new[] { new { appId = item.App.AppName, platform = new[] { "Windows" } } },
        }));
        Directory.CreateDirectory(Path.GetDirectoryName(Folders.CatalogPath)!);
        File.WriteAllText(Folders.CatalogPath, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Epic's download queue, in order (<c>[]</c> when nothing is queued).</summary>
    public void SetQueue(params EpicApp[] apps)
    {
        var directory = Path.Combine(Folders.Data, "DownloadManager");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "DownloadState_account1.json"),
            JsonSerializer.Serialize(apps.Select(app => new { AppId = new { app.Namespace, app.CatalogItemId, app.AppName } })));
    }

    /// <summary>The manifest of an install in progress; it stays behind after a cancel.</summary>
    public void WritePending(EpicApp app)
    {
        var directory = Path.Combine(Manifests, "Pending");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{app.AppName}.item"), Manifest(app));
    }

    /// <summary>What has arrived in the install folder (Epic downloads into <c>.egstore</c> inside it).</summary>
    public void WriteGameBytes(EpicApp app, long bytes)
    {
        var folder = Path.Combine(InstallLocation(app), ".egstore");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "chunk.bin"), new byte[bytes]);
    }

    /// <summary>Lines the launcher adds to <c>EpicGamesLauncher.log</c>, each with the next second of the log's clock.</summary>
    public void Log(params Func<string, string>[] lines) =>
        File.AppendAllLines(Folders.CurrentLogPath, lines.Select(line => line($"[2026.10.24-19.{_logClock / 60:00}.{_logClock++ % 60:00}:000][ 42]")));

    /// <summary>Epic starts a new log at its next start; the old one becomes a backup, and old backups go.</summary>
    public void StartNewLog(bool deleteBackups = false)
    {
        if (deleteBackups)
        {
            foreach (var backup in Directory.GetFiles(Folders.Logs, "EpicGamesLauncher-backup-*.log"))
            {
                File.Delete(backup);
            }
        }

        var stamp = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        File.Move(Folders.CurrentLogPath, Path.Combine(Folders.Logs, $"EpicGamesLauncher-backup-{stamp}.log"));
        File.WriteAllText(Folders.CurrentLogPath, string.Empty);
    }

    public static Func<string, string> Queued(EpicApp app) => time => $"{time}LogDownloadManager: Queued install for the {app.LogId}, Queue length is 1, Are we restarting followers 0";

    public static Func<string, string> Started(EpicApp app) => time => $"{time}LogEoshPatcher: StartTask: BinaryId [{app.LogId}:{app.Version}] — using default components: []";

    public static Func<string, string> Paused(EpicApp app) => time => $"{time}LogDownloadManager: Pause is requested for the {app.LogId}";

    public static Func<string, string> Resumed(EpicApp app) => time => $"{time}LogDownloadManager: Resume of active installation is requested for the {app.LogId}";

    public static Func<string, string> Cancelled(EpicApp app) => time => $"{time}LogDownloadManager: Cancel of active installation is requested for the {app.LogId}";

    public static Func<string, string> Completed(EpicApp app) => time => $"{time}LogDownloadManager: HandleTaskComplete: AppId [{app.LogId}] AlertCode=[ok] QueueLen=0 IncompleteInstall=0 AutoResume=1";

    public static Func<string, string> PlaytimeStarted(EpicApp app, DateTimeOffset at) => _ => $"{PlayTime(at)}LogPortalPublishTime: Started tracking playtime for {app.LogId}";

    public static Func<string, string> PlaytimeStopped(EpicApp app, DateTimeOffset at) => _ => $"{PlayTime(at)}LogPortalPublishTime: Stopped tracking playtime for {app.LogId}";

    /// <summary>The launcher's report at its start: each installed app's version and whether Epic has an update for it.</summary>
    public static Func<string, string> InstalledApps(params (EpicApp App, string Version, bool HasUpdate)[] apps) => time =>
        $"{time}LogEOSNotificationBridge: Sending response: "
        + JsonSerializer.Serialize(new
        {
            feature = "productinfo",
            name = "get-installed-apps-response",
            payload = new
            {
                products = apps.Select(a => new { @namespace = a.App.Namespace, appName = a.App.AppName, isInstalled = true, installedVersion = a.Version, hasUpdate = a.HasUpdate }),
            },
        });

    private static string PlayTime(DateTimeOffset at) => $"[{at.UtcDateTime.ToString("yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture)}:000][ 42]";

    private string Manifest(EpicApp app, string? mainGame = null, string? location = null) => JsonSerializer.Serialize(new
    {
        FormatVersion = 0,
        bIsIncompleteInstall = false,
        DisplayName = app.Title,
        AppName = app.AppName,
        MainGameAppName = mainGame ?? app.AppName,
        CatalogNamespace = app.Namespace,
        CatalogItemId = app.CatalogItemId,
        AppVersionString = app.Version,
        InstallLocation = location ?? InstallLocation(app),
        LaunchExecutable = "Game.exe",
        InstallSize = app.InstallSize,
        bRequiresAuth = true,
    });
}
