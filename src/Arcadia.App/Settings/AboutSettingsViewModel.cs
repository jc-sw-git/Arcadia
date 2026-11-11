using Arcadia.App.Messages;
using Arcadia.App.Services;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Über Arcadia" (#15): the version, the search for updates, the update itself and "Was ist neu"; below them
/// the group "LOGGING" (#433).
/// </summary>
public sealed class AboutSettingsViewModel : SettingsCategoryViewModel
{
    private const string Group = "VERSION";

    /// <summary>"Jetzt aktualisieren"; the status modal's update entry leads there (#166).</summary>
    public const string InstallKey = "arcadia-install";

    /// <summary>The download's progress, here and on the status modal's update entry.</summary>
    public static string LoadingText(string version, int percent) => AppTexts.AboutLoading(version, percent);

    private readonly ArcadiaUpdateService _updates;
    private readonly InfoRowViewModel _version;
    private readonly ActionRowViewModel _check;
    private readonly ActionRowViewModel _install;
    private readonly InfoRowViewModel _changelog;
    private readonly RecentMessages _messages;
    private readonly LoggingRows _logging;
    private bool _disposed;

    /// <summary>How often "×N" is read again while the category is shown (#433): repeats come without an event.</summary>
    public static readonly TimeSpan RepeatsInterval = TimeSpan.FromSeconds(1);

    // Kept so it isn't collected; it ticks until the category is disposed.
    private readonly ITimer _repeats;

    public AboutSettingsViewModel(ArcadiaUpdateService updates, Func<Task> installUpdate, RecentMessages messages, SettingsChanges changes, ILogFiles logs,
        TimeProvider time, Action<LogKind> openLog)
        : base(AppTexts.AboutTitle, "M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0z M12 11v6 M12 7.5v.01")
    {
        _updates = updates;
        _messages = messages;
        _logging = new LoggingRows(messages, changes, logs, openLog);
        _version = new InfoRowViewModel("arcadia-version", $"Arcadia {updates.CurrentVersion}") { Group = Group };
        _check = new ActionRowViewModel("arcadia-check", AppTexts.AboutCheckForUpdates, () => updates.CheckNowAsync().Forget(AppTexts.ErrorContextUpdateCheck))
        {
            Group = Group,
            Subtitle = AppTexts.AboutChecksByItself,
        };
        _install = new ActionRowViewModel(InstallKey, AppTexts.UpdateNow, () => installUpdate().Forget(AppTexts.ErrorContextUpdateArcadia))
        {
            Group = Group,
            Subtitle = AppTexts.AboutInstallSubtitle,
            ActionText = AppTexts.UpdateNow,
            IsPrimary = true,
        };
        _changelog = new InfoRowViewModel("arcadia-changelog", AppTexts.AboutWhatsNew) { Group = Group };
        _updates.Changed += OnUpdatesChanged;
        _messages.Changed += OnMessagesChanged;
        _repeats = time.CreateTimer(_ => Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
            {
                _logging.RefreshIfRepeated();
            }
        }, DispatcherPriority.Background), null, RepeatsInterval, RepeatsInterval);
        Refresh();
    }

    /// <summary>Chosen again: a log may have appeared meanwhile, e.g. pan.log after installing Pan.</summary>
    public override void ShowTopLevel()
    {
        _logging.Refresh();
        base.ShowTopLevel();
    }

    public override void Dispose()
    {
        _disposed = true;
        _repeats.Dispose();
        _updates.Changed -= OnUpdatesChanged;
        _messages.Changed -= OnMessagesChanged;
    }

    /// <summary>A toast or status change from any thread: the list follows on the UI thread while it is on screen.</summary>
    private void OnMessagesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            _logging.Refresh();
        }
    });

    private void OnUpdatesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            Refresh();
        }
    });

    private void Refresh()
    {
        var updates = _updates;
        var updateText = updates switch
        {
            { IsInstalled: false } => AppTexts.AboutDevelopmentBuild,
            { IsChecking: true } => AppTexts.AboutChecking,
            { AvailableVersion: { } next, IsRestartingForUpdate: true } => AppTexts.AboutLoadedRestarting(next),
            { AvailableVersion: { } next, Progress: { } percent } => LoadingText(next, percent),
            { AvailableVersion: { } next } => AppTexts.AboutAvailable(next),
            { LastChecked: { } checkedAt } => AppTexts.AboutUpToDateCheckedAt(checkedAt.ToLocalTime()),
            _ => AppTexts.AboutUpToDate,
        };
        var source = AppTexts.AboutLicense(ArcadiaUpdateService.RepositoryUrl.Replace("https://", string.Empty, StringComparison.Ordinal));

        _version.Subtitle = updateText;
        _version.Info =
        [
            new InfoText(updateText),
            .. updates.LastError is { } error ? [new InfoError(error)] : Array.Empty<InfoBlock>(),
            new InfoFootnote(source),
        ];

        var idle = !updates.IsChecking && updates.Progress is null && !updates.IsRestartingForUpdate;
        _check.ActionText = updates.IsChecking ? AppTexts.AboutCheckRunning : AppTexts.AboutCheck;
        _check.IsEnabled = idle;
        _check.Info = [new InfoText(updateText), .. updates.LastError is { } checkError ? [new InfoError(checkError)] : Array.Empty<InfoBlock>()];

        var hasUpdate = updates.AvailableVersion is not null && updates.Progress is null && !updates.IsRestartingForUpdate;
        _install.Info = [new InfoText(updateText), new InfoText(AppTexts.AboutInstallInfo)];

        // Only the versions between the running and the newest one.
        var hasChangelog = updates.AvailableVersion is not null && updates.Changelog.Count > 0;
        _changelog.Subtitle = updates.AvailableVersion is { } version ? AppTexts.AboutNewUpToVersion(version) : null;
        _changelog.Info = [.. updates.Changelog.Select(InfoRelease.Of)];

        SetRows(
        [
            _version,
            .. updates.IsInstalled ? [_check] : Array.Empty<SettingsRowViewModel>(),
            .. hasUpdate ? [_install] : Array.Empty<SettingsRowViewModel>(),
            .. hasChangelog ? [_changelog] : Array.Empty<SettingsRowViewModel>(),
            .. _logging.Rows,
        ]);
    }
}
