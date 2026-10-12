using System.Globalization;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Follows Steam installations, updates and uninstalls through the app manifests, which Steam
/// rewrites every few seconds while it works (verified on the owner's PC, 2026-10-12):
/// a new install appears with <c>StateFlags</c> 2 and <c>buildid</c> 0, then 1026 (update required
/// + started) while it downloads or waits, 1538 (+ paused) when paused, and 4 once done.
/// <c>BytesDownloaded/BytesToDownload</c> and <c>BytesStaged/BytesToStage</c> give the progress.
/// Uninstalling an installed game (38 = files missing, for a moment) and cancelling a new install
/// both delete the manifest.
/// Steam downloads one app at a time but marks every waiting app the same way, so the one whose
/// bytes moved last is the one downloading.
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
    private bool _primed;

    public string LauncherId => KnownLaunchers.Steam.Id;

    public InstallSnapshot Poll()
    {
        var manifests = ReadManifests();
        var now = _time.GetUtcNow();
        var finished = new Dictionary<string, InstallOutcome>(StringComparer.Ordinal);

        foreach (var (appId, tracked) in _active.ToList())
        {
            if (manifests.TryGetValue(appId, out var manifest) && IsActive(manifest))
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
            if (!IsActive(manifest))
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

        var downloading = _active
            .Where(a => a.Value.MovedAt is not null && (manifests[a.Key].Flags & UpdatePaused) == 0)
            .OrderByDescending(a => a.Value.MovedAt)
            .Select(a => a.Key)
            .FirstOrDefault();

        var active = new Dictionary<string, InstallProgress>(StringComparer.Ordinal);
        foreach (var (appId, tracked) in _active.ToList())
        {
            var progress = ProgressOf(manifests[appId], appId == downloading);
            _active[appId] = tracked with { LastPhase = progress.Phase };
            active[appId] = progress;
        }

        _previous = manifests;
        _primed = true;
        return new InstallSnapshot(active, finished);
    }

    /// <summary>The state of one manifest, as Arcadia reads it.</summary>
    /// <param name="IsDownloading">Whether this app's bytes moved most recently of all waiting apps.</param>
    public static InstallProgress ProgressOf(SteamManifestState manifest, bool isDownloading)
    {
        var fraction = manifest.Fraction;
        if ((manifest.Flags & Uninstalling) != 0)
        {
            return new InstallProgress(InstallPhase.Uninstalling);
        }

        if ((manifest.Flags & UpdatePaused) != 0)
        {
            return new InstallProgress(InstallPhase.Paused, fraction);
        }

        if (manifest.BytesToDownload > 0 && manifest.BytesDownloaded >= manifest.BytesToDownload)
        {
            return new InstallProgress(InstallPhase.Finishing, fraction);
        }

        return new InstallProgress(isDownloading ? InstallPhase.Downloading : InstallPhase.Queued, fraction);
    }

    /// <summary>
    /// True while Steam works on the app, and for a new install Steam has accepted but not started
    /// (<c>StateFlags</c> 2 without a build). A pending update Steam hasn't started is no installation.
    /// </summary>
    public static bool IsActive(SteamManifestState manifest) =>
        (manifest.Flags & Busy) != 0
        || ((manifest.Flags & UpdateRequired) != 0 && (manifest.Flags & FullyInstalled) == 0 && manifest.BuildId == 0);

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

    private Dictionary<string, SteamManifestState> ReadManifests()
    {
        var manifests = new Dictionary<string, SteamManifestState>(StringComparer.Ordinal);
        if (_locate() is not { } steam)
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

    /// <summary>Download and staging (writing the files) together, 0…1; null before Steam knows the size.</summary>
    public double? Fraction => BytesToDownload + BytesToStage is > 0 and var total
        ? Math.Clamp((double)(BytesDownloaded + BytesStaged) / total, 0, 1)
        : null;
}
