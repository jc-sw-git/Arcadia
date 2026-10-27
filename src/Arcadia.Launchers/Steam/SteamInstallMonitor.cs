using System.Globalization;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Follows Steam installations, updates and uninstalls through the app manifests and Steam's content log
/// (verified on the owner's PC, 2026-10-12 and 2026-10-27): a new install's manifest appears with <c>StateFlags</c> 2
/// and <c>buildid</c> 0, then 1026 (update required + started) while it downloads or waits, and 4 once done.
/// Uninstalling an installed game (38 = files missing, for a moment) and cancelling a new install both delete the manifest.
/// The manifests flag a waiting and a running download alike and get their byte counters only on a state change, so
/// the content log (<see cref="SteamContentLog"/>), written the moment anything changes, decides what a download does.
/// A percentage is shown only when it is exact (#278): while a download is stopped (paused, queued) Steam's last bytes
/// are; while it runs they go stale at once, so a running download has none. Without the log, the manifests decide,
/// and the app whose bytes moved last counts as downloading.
/// </summary>
public sealed class SteamInstallMonitor(Func<SteamInstallation?>? locate = null, TimeProvider? time = null) : IInstallMonitor
{
    private const int UpdateRequired = 2;
    private const int FullyInstalled = 4;
    private const int UpdateRunning = 256;
    private const int UpdatePaused = 512;
    private const int UpdateStarted = 1024;
    private const int Uninstalling = 2048;
    private const int Validating = 131072;
    private const int Downloading = 1048576;
    private const int Staging = 2097152;
    private const int Committing = 4194304;
    private const int Busy = UpdateRunning | UpdatePaused | UpdateStarted | Uninstalling | Validating | Downloading | Staging | Committing;

    private readonly Func<SteamInstallation?> _locate = locate ?? SteamInstallation.Find;
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Dictionary<string, Tracked> _active = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, SteamManifestState> _previous = new Dictionary<string, SteamManifestState>();
    private SteamContentLog? _contentLog;
    private string? _contentLogRoot;
    private bool _primed;

    public string LauncherId => KnownLaunchers.Steam.Id;

    public InstallSnapshot Poll()
    {
        var steam = _locate();
        var manifests = ReadManifests(steam);
        var contentLog = ReadContentLog(steam);
        var now = _time.GetUtcNow();
        var finished = new Dictionary<string, InstallOutcome>(StringComparer.Ordinal);

        foreach (var (appId, tracked) in _active.ToList())
        {
            if (manifests.TryGetValue(appId, out var manifest) && IsActive(manifest, contentLog?.StateOf(manifest.AppId)))
            {
                continue;
            }

            _active.Remove(appId);
            if (_primed)
            {
                finished[appId] = OutcomeOf(tracked.LastPhase, _previous.GetValueOrDefault(appId), manifest);
            }
        }

        // An installed game removed between two polls: Steam uninstalls too fast to show it as active.
        foreach (var (appId, before) in _previous)
        {
            if (_primed && !manifests.ContainsKey(appId) && !finished.ContainsKey(appId) && (before.Flags & FullyInstalled) != 0)
            {
                finished[appId] = InstallOutcome.Uninstalled;
            }
        }

        foreach (var (appId, manifest) in manifests)
        {
            if (!IsActive(manifest, contentLog?.StateOf(manifest.AppId)))
            {
                continue;
            }

            if (!_active.TryGetValue(appId, out var tracked))
            {
                tracked = new Tracked(manifest.Bytes, null, InstallPhase.Queued);
            }
            else if (manifest.Bytes != tracked.Bytes)
            {
                tracked = tracked with { Bytes = manifest.Bytes, MovedAt = now };
            }

            _active[appId] = tracked;
        }

        var logNamesRunningApp = contentLog is not null && _active.Keys.Any(a => contentLog.StateOf(manifests[a].AppId)?.IsRunning == true);
        var downloading = logNamesRunningApp ? null : AppWhoseBytesMovedLast(manifests);
        var active = new Dictionary<string, InstallProgress>(StringComparer.Ordinal);
        foreach (var (appId, tracked) in _active.ToList())
        {
            var manifest = manifests[appId];
            var progress = contentLog?.StateOf(manifest.AppId) is { } logged
                ? ProgressOf(manifest, logged)
                : ProgressOf(manifest, isDownloading: appId == downloading);
            _active[appId] = tracked with { LastPhase = progress.Phase };
            active[appId] = progress;
        }

        _previous = manifests;
        _primed = true;
        return new InstallSnapshot(active, finished);
    }

    // Without the content log only the manifests' byte counters hint which app Steam works on.
    private string? AppWhoseBytesMovedLast(Dictionary<string, SteamManifestState> manifests) => _active
        .Where(a => (manifests[a.Key].Flags & UpdatePaused) == 0 && a.Value.MovedAt is not null)
        .OrderByDescending(a => a.Value.MovedAt)
        .Select(a => a.Key)
        .FirstOrDefault();

    /// <summary>The state of one installation as the content log tells it; the manifest adds uninstalling and bytes.</summary>
    public static InstallProgress ProgressOf(SteamManifestState manifest, SteamContentState logged)
    {
        if ((manifest.Flags & Uninstalling) != 0)
        {
            return new InstallProgress(InstallPhase.Uninstalling);
        }

        // Steam restarted or switched accounts and hasn't picked the download up again (#278 decision 4).
        if (logged.FromEarlierSession && logged.Phase != SteamDownloadPhase.Done)
        {
            return new InstallProgress(InstallPhase.Paused, StoppedFraction(manifest, logged));
        }

        return logged.Phase switch
        {
            SteamDownloadPhase.Paused => new InstallProgress(InstallPhase.Paused, StoppedFraction(manifest, logged)),
            SteamDownloadPhase.Queued => new InstallProgress(InstallPhase.Queued, StoppedFraction(manifest, logged)),
            SteamDownloadPhase.Preparing or SteamDownloadPhase.Downloading => new InstallProgress(InstallPhase.Downloading),
            _ => new InstallProgress(InstallPhase.Finishing),
        };
    }

    /// <summary>The state of one manifest when the content log has nothing on the app.</summary>
    /// <param name="isDownloading">Whether Steam works on this app rather than letting it wait.</param>
    public static InstallProgress ProgressOf(SteamManifestState manifest, bool isDownloading)
    {
        if ((manifest.Flags & Uninstalling) != 0)
        {
            return new InstallProgress(InstallPhase.Uninstalling);
        }

        if ((manifest.Flags & UpdatePaused) != 0)
        {
            return new InstallProgress(InstallPhase.Paused, manifest.Fraction);
        }

        if (manifest.BytesToDownload > 0 && manifest.BytesDownloaded >= manifest.BytesToDownload)
        {
            return new InstallProgress(InstallPhase.Finishing);
        }

        return isDownloading ? new InstallProgress(InstallPhase.Downloading) : new InstallProgress(InstallPhase.Queued, manifest.Fraction);
    }

    /// <summary>
    /// The download fraction of a stopped download, as Steam's download page shows it: Steam writes the manifest's
    /// counters when the download stops and the log's at the last start; both only grow, so the larger is the current
    /// one. The manifest's count only if it belongs to the same update (the same total).
    /// </summary>
    private static double? StoppedFraction(SteamManifestState manifest, SteamContentState logged)
    {
        if (logged.DownloadTotal <= 0)
        {
            return manifest.Fraction;
        }

        var downloaded = manifest.BytesToDownload == logged.DownloadTotal
            ? Math.Max(manifest.BytesDownloaded, logged.DownloadedAtStart)
            : logged.DownloadedAtStart;
        return Math.Clamp((double)downloaded / logged.DownloadTotal, 0, 1);
    }

    /// <summary>
    /// True while Steam works on the app, and for a new install Steam has accepted but not started
    /// (<c>StateFlags</c> 2 without a build). A pending update Steam hasn't started is no installation.
    /// The content log knows of a start or resume before the manifest shows it; a stopped download is busy in its manifest.
    /// </summary>
    public static bool IsActive(SteamManifestState manifest, SteamContentState? logged = null) =>
        (manifest.Flags & Busy) != 0
        || ((manifest.Flags & UpdateRequired) != 0 && (manifest.Flags & FullyInstalled) == 0 && manifest.BuildId == 0)
        || logged is { IsRunning: true };

    /// <summary>Parses the fields of an <c>appmanifest_*.acf</c> the monitor needs; null for a broken file.</summary>
    public static SteamManifestState? ParseState(string? acf)
    {
        VdfMap? app;
        try
        {
            app = string.IsNullOrEmpty(acf) ? null : TextVdf.Parse(acf).GetMap("AppState");
        }
        catch (FormatException)
        {
            return null;
        }

        if (app is null || !uint.TryParse(app.GetString("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            return null;
        }

        long Number(string key) => long.TryParse(app.GetString(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
        return new SteamManifestState(
            appId,
            app.GetString("name") ?? string.Empty,
            (int)Number("StateFlags"),
            Number("buildid"),
            Number("BytesToDownload"),
            Number("BytesDownloaded"),
            Number("BytesToStage"),
            Number("BytesStaged"));
    }

    // Gone after an uninstall, or after cancelling a new install; an installed game's build survives in its last manifest.
    private static InstallOutcome OutcomeOf(InstallPhase lastPhase, SteamManifestState? before, SteamManifestState? manifest) => manifest switch
    {
        null => lastPhase == InstallPhase.Uninstalling || before?.BuildId > 0 ? InstallOutcome.Uninstalled : InstallOutcome.Cancelled,
        _ when (manifest.Flags & FullyInstalled) != 0 && (manifest.Flags & UpdateRequired) == 0 => InstallOutcome.Completed,
        _ => InstallOutcome.Cancelled,
    };

    private SteamContentLog? ReadContentLog(SteamInstallation? steam)
    {
        if (steam is null)
        {
            return null;
        }

        if (_contentLog is null || !string.Equals(_contentLogRoot, steam.RootPath, StringComparison.OrdinalIgnoreCase))
        {
            _contentLog = new SteamContentLog(Path.Combine(steam.RootPath, "logs", "content_log.txt"));
            _contentLogRoot = steam.RootPath;
        }

        return _contentLog.Update() ? _contentLog : null;
    }

    private static Dictionary<string, SteamManifestState> ReadManifests(SteamInstallation? steam)
    {
        var manifests = new Dictionary<string, SteamManifestState>(StringComparer.Ordinal);
        if (steam is null)
        {
            return manifests;
        }

        foreach (var library in SteamScanner.LibraryPaths(steam.RootPath, SteamScanner.ReadText(Path.Combine(steam.RootPath, "steamapps", "libraryfolders.vdf"))))
        {
            foreach (var file in SteamScanner.ManifestFiles(library))
            {
                // Steam updates its runtimes and redistributables on its own; they are no games to report.
                if (ParseState(SteamScanner.ReadText(file)) is { } manifest && !SteamScanner.IsTool(manifest.AppId, manifest.Name))
                {
                    manifests[manifest.AppId.ToString(CultureInfo.InvariantCulture)] = manifest;
                }
            }
        }

        return manifests;
    }

    private sealed record Tracked((long Downloaded, long Staged) Bytes, DateTimeOffset? MovedAt, InstallPhase LastPhase);
}

/// <summary>What one app manifest says about an installation in progress.</summary>
public sealed record SteamManifestState(uint AppId, string Name, int Flags, long BuildId, long BytesToDownload, long BytesDownloaded, long BytesToStage, long BytesStaged)
{
    public (long Downloaded, long Staged) Bytes => (BytesDownloaded, BytesStaged);

    /// <summary>
    /// The download alone, 0…1, as Steam's download page counts it (#278 decision 3); null before Steam knows the size.
    /// Steam writes the counters only on a state change, so this is exact only while the download is stopped.
    /// </summary>
    public double? Fraction => BytesToDownload > 0 ? Math.Clamp((double)BytesDownloaded / BytesToDownload, 0, 1) : null;
}
