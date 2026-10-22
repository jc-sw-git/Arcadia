using Arcadia.App.Services;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>The category "Über Arcadia" (#15): the version, the search for updates, the update itself and "Was ist neu".</summary>
public sealed class AboutSettingsViewModel : SettingsCategoryViewModel
{
    private const string Group = "VERSION";

    private readonly ArcadiaUpdateService _updates;
    private readonly InfoRowViewModel _version;
    private readonly ActionRowViewModel _check;
    private readonly ActionRowViewModel _install;
    private readonly InfoRowViewModel _changelog;
    private bool _disposed;

    public AboutSettingsViewModel(ArcadiaUpdateService updates, Func<Task> installUpdate)
        : base("Über Arcadia", "M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0z M12 11v6 M12 7.5v.01")
    {
        _updates = updates;
        _version = new InfoRowViewModel("arcadia-version", $"Arcadia {updates.CurrentVersion}") { Group = Group };
        _check = new ActionRowViewModel("arcadia-check", "Nach Updates suchen", () => updates.CheckNowAsync().Forget("Update-Prüfung"))
        {
            Group = Group,
            Subtitle = "Arcadia sucht auch von selbst alle 6 Stunden",
        };
        _install = new ActionRowViewModel("arcadia-install", "Jetzt aktualisieren", () => installUpdate().Forget("Arcadia aktualisieren"))
        {
            Group = Group,
            Subtitle = "Arcadia schließt sich, installiert das Update und startet neu",
            ActionText = "Jetzt aktualisieren",
            IsPrimary = true,
        };
        _changelog = new InfoRowViewModel("arcadia-changelog", "Was ist neu") { Group = Group };
        _updates.Changed += OnUpdatesChanged;
        Refresh();
    }

    public override void Dispose()
    {
        _disposed = true;
        _updates.Changed -= OnUpdatesChanged;
    }

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
            { IsInstalled: false } => "Entwicklungs-Build – Updates gibt es nur für das installierte Arcadia.",
            { IsChecking: true } => "Suche nach Updates …",
            { AvailableVersion: { } next, IsRestartingForUpdate: true } => $"Version {next} ist geladen – Arcadia startet neu …",
            { AvailableVersion: { } next, Progress: { } percent } => $"Version {next} wird geladen … {percent} %",
            { AvailableVersion: { } next } => $"Version {next} ist verfügbar.",
            { LastChecked: { } checkedAt } => $"Arcadia ist auf dem neuesten Stand. Zuletzt gesucht: {checkedAt.ToLocalTime():HH:mm} Uhr.",
            _ => "Arcadia ist auf dem neuesten Stand.",
        };
        var source = $"Freie Software unter der GPL-3.0 · Quelltext: {ArcadiaUpdateService.RepositoryUrl.Replace("https://", string.Empty, StringComparison.Ordinal)}";

        _version.Subtitle = updateText;
        _version.Info =
        [
            new InfoText(updateText),
            .. updates.LastError is { } error ? [new InfoError(error)] : Array.Empty<InfoBlock>(),
            new InfoFootnote(source),
        ];

        var idle = !updates.IsChecking && updates.Progress is null && !updates.IsRestartingForUpdate;
        _check.ActionText = updates.IsChecking ? "Suche läuft …" : "Suchen";
        _check.IsEnabled = idle;
        _check.Info = [new InfoText(updateText), .. updates.LastError is { } checkError ? [new InfoError(checkError)] : Array.Empty<InfoBlock>()];

        var hasUpdate = updates.AvailableVersion is not null && updates.Progress is null && !updates.IsRestartingForUpdate;
        _install.Info = [new InfoText(updateText), new InfoText("Arcadia schließt sich, Velopack installiert das Update ohne Rückfrage und startet Arcadia neu. Ein laufendes Spiel läuft dabei weiter.")];

        // Only the versions between the running and the newest one.
        var hasChangelog = updates.AvailableVersion is not null && updates.Changelog.Count > 0;
        _changelog.Subtitle = updates.AvailableVersion is { } version ? $"Neu bis Version {version}" : null;
        _changelog.Info = [.. updates.Changelog.Select(InfoRelease.Of)];

        SetRows(
        [
            _version,
            .. updates.IsInstalled ? [_check] : Array.Empty<SettingsRowViewModel>(),
            .. hasUpdate ? [_install] : Array.Empty<SettingsRowViewModel>(),
            .. hasChangelog ? [_changelog] : Array.Empty<SettingsRowViewModel>(),
        ]);
    }
}
