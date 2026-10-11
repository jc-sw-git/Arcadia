using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Arcadia.App.Services;

/// <summary>
/// Arcadia's own updates: checks GitHub Releases through Velopack, downloads on request and
/// restarts into the new version. Only an installed Arcadia updates; from bin/ this stays idle.
/// </summary>
public sealed class ArcadiaUpdateService(TimeProvider time)
{
    public const string RepositoryUrl = "https://github.com/jc-sw-git/Arcadia";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    // Stable releases only; pre-release tags (v0.2.0-beta.1) are for testers who install them by hand.
    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private DateTimeOffset? _lastCheck;
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

    /// <summary>Download progress in percent while an update downloads.</summary>
    public int? Progress { get; private set; }

    /// <summary>The update is downloaded and installs when Arcadia closes (a game was running).</summary>
    public bool InstallsOnExit { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Checks GitHub at most every few hours; call it regularly.</summary>
    public async Task CheckIfDueAsync()
    {
        if (!IsInstalled || Progress is not null || InstallsOnExit
            || (_lastCheck is { } last && time.GetUtcNow() - last < CheckInterval))
        {
            return;
        }

        _lastCheck = time.GetUtcNow();
        try
        {
            _update = await _manager.CheckForUpdatesAsync();
            Log?.Invoke(_update is null ? "Arcadia-Update: aktuell" : $"Arcadia-Update verfügbar: {AvailableVersion}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline or GitHub unreachable: try again at the next interval, without bothering the user.
            Log?.Invoke($"Arcadia-Update-Prüfung fehlgeschlagen: {ex.Message}");
        }

        OnChanged();
    }

    /// <summary>
    /// Downloads the available update. Then Arcadia restarts into it, unless
    /// <paramref name="gameRunning"/> says a game runs: then it installs when Arcadia closes.
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
