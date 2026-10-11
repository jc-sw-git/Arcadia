using System.Reflection;
using Arcadia.ControllerSupport;
using Arcadia.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Arcadia.App.Services;

/// <summary>
/// Arcadia's own updates: checks GitHub Releases through Velopack (every few hours and on
/// request), collects the changelog since the running version, downloads on request and restarts
/// into the new version. Only an installed Arcadia updates; from bin/ this stays idle.
/// </summary>
public sealed class ArcadiaUpdateService(HttpClient http, TimeProvider time)
{
    public const string RepositoryUrl = "https://github.com/jc-sw-git/Arcadia";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    // Stable releases only; pre-release tags (v0.2.0-beta.1) are for testers who install them by hand.
    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private UpdateInfo? _update;

    /// <summary>Raised on any state change, possibly off the UI thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when Arcadia should close so the downloaded update can install and restart it.</summary>
    public event EventHandler? RestartRequested;

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion => _manager.CurrentVersion?.ToString()
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? "?";

    /// <summary>The newer version on GitHub, if any.</summary>
    public string? AvailableVersion => _update?.TargetFullRelease.Version.ToString();

    /// <summary>What changed between the running and the available version, newest first.</summary>
    public IReadOnlyList<ChangelogEntry> Changelog { get; private set; } = [];

    public bool IsChecking { get; private set; }

    public DateTimeOffset? LastChecked { get; private set; }

    /// <summary>Download progress in percent while an update downloads.</summary>
    public int? Progress { get; private set; }

    /// <summary>The update is downloaded and installs when Arcadia closes (a game started meanwhile).</summary>
    public bool InstallsOnExit { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>The background check; asks GitHub at most every few hours. Call it regularly.</summary>
    public Task CheckIfDueAsync() =>
        LastChecked is { } last && time.GetUtcNow() - last < CheckInterval ? Task.CompletedTask : CheckAsync(manual: false);

    /// <summary>"Nach Updates suchen": checks right away and reports a failure to the user.</summary>
    public Task CheckNowAsync() => CheckAsync(manual: true);

    private async Task CheckAsync(bool manual)
    {
        if (!IsInstalled || IsChecking || Progress is not null || InstallsOnExit)
        {
            return;
        }

        IsChecking = true;
        LastError = null;
        OnChanged();
        try
        {
            _update = await _manager.CheckForUpdatesAsync();
            Changelog = _update is null ? [] : await LoadChangelogAsync(_update);
            LastChecked = time.GetUtcNow();
            Log?.Invoke(_update is null ? "Arcadia-Update: aktuell" : $"Arcadia-Update verfügbar: {AvailableVersion}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // In the background (offline, GitHub unreachable) try again later without bothering the user.
            LastError = manual ? $"Die Suche nach Updates ist fehlgeschlagen: {ex.Message}" : null;
            Log?.Invoke($"Arcadia-Update-Prüfung fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            IsChecking = false;
            OnChanged();
        }
    }

    /// <summary>
    /// The notes of every release since the running version. If GitHub's release list can't be
    /// read, at least the notes Velopack shipped with the newest package.
    /// </summary>
    private async Task<IReadOnlyList<ChangelogEntry>> LoadChangelogAsync(UpdateInfo update)
    {
        var latest = Core.Updates.Changelog.ParseVersion(update.TargetFullRelease.Version.ToString()) ?? new Version();
        var current = Core.Updates.Changelog.ParseVersion(CurrentVersion) ?? new Version();
        try
        {
            var releases = await new GitHubReleaseClient(http).GetReleasesAsync("jc-sw-git", "Arcadia");
            var entries = Core.Updates.Changelog.Between(releases, current, latest);
            if (entries.Count > 0)
            {
                return entries;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Log?.Invoke($"Changelog nicht geladen: {ex.Message}");
        }

        return [new ChangelogEntry(latest, null, Core.Updates.Changelog.ParseChanges(update.TargetFullRelease.NotesMarkdown ?? string.Empty))];
    }

    /// <summary>
    /// Downloads the available update and restarts Arcadia into it. If a game started while the
    /// update downloaded, it installs when Arcadia closes instead, so the game keeps its controller.
    /// </summary>
    public async Task InstallAsync(Func<bool> gameRunning)
    {
        if (_update is not { } update || Progress is not null || InstallsOnExit)
        {
            return;
        }

        LastError = null;
        Progress = 0;
        OnChanged();
        try
        {
            await _manager.DownloadUpdatesAsync(update, percent =>
            {
                Progress = percent;
                OnChanged();
            });

            // Velopack's updater waits until Arcadia has exited, so closing normally (which also
            // stops SISR cleanly) is enough; it then installs and, if wanted, restarts Arcadia.
            var restart = !gameRunning();
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: restart);
            InstallsOnExit = true;
            Log?.Invoke($"Arcadia-Update {AvailableVersion} geladen, {(restart ? "Neustart" : "wird beim Beenden installiert")}");
            if (restart)
            {
                RestartRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = $"Das Update konnte nicht geladen werden: {ex.Message}";
            Log?.Invoke($"Arcadia-Update fehlgeschlagen: {ex}");
        }
        finally
        {
            Progress = null;
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
