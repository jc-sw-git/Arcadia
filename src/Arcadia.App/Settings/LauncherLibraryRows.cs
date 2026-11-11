using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "Bibliothek" on a launcher's page: its folder if Arcadia can't find it (#138), the switches for software,
/// tools and soundtracks the launcher offers (#141), and "Neu einlesen".
/// </summary>
internal sealed class LauncherLibraryRows
{
    /// <summary>The group's title; "Alle neu einlesen" on the list stands in it too.</summary>
    public static string LibraryGroup => AppTexts.LibraryGroup;

    private readonly ILauncherCategory _category;
    private readonly LauncherOrder _order;
    private readonly ActionRowViewModel _folderRow;
    private readonly ActionRowViewModel _rescanRow;

    // A launcher's switches for software, tools and soundtracks, made when its page first shows them.
    private readonly Dictionary<string, SwitchRowViewModel> _optionRows = new(StringComparer.Ordinal);

    /// <param name="order">A folder chosen for an inactive launcher switches it on.</param>
    public LauncherLibraryRows(ILauncherCategory category, LauncherOrder order)
    {
        _category = category;
        _order = order;
        _folderRow = new ActionRowViewModel("launcher-folder", "Ordner", () => PickFolderAsync().Forget(AppTexts.ErrorContextPickFolder))
        {
            Group = LibraryGroup,
            ActionText = AppTexts.PickFolder,
        };
        _rescanRow = new ActionRowViewModel("launcher-rescan", AppTexts.Rescan, () => category.Host.RescanLauncher(category.PageLauncherId!))
        {
            Group = LibraryGroup,
            ActionText = AppTexts.ScanNow,
        };
    }

    /// <summary>The rows for the page of launcher <paramref name="id"/>; <paramref name="library"/> is null while it is inactive or not read yet.</summary>
    public IReadOnlyList<SettingsRowViewModel> Show(string id, string name, bool isActive, bool isFound, LauncherLibrary? library)
    {
        var host = _category.Host;
        var launchers = host.Launchers;
        var preferences = _category.Changes.Current.PreferencesOf(id);
        var folderChoice = launchers.Registered(id)?.FolderChoice;

        // Only a fallback: shown while Arcadia can't find the launcher by itself, or a folder was chosen (owner, 2026-10-23).
        var showsFolder = folderChoice is not null && (!isFound || preferences.Folder is not null);
        if (folderChoice is not null)
        {
            _folderRow.Subtitle = preferences.Folder ?? AppTexts.LauncherFolderSubtitle(name);
            _folderRow.Severity = isFound ? null : HealthSeverity.Warning;
            _folderRow.Info =
            [
                new InfoText(AppTexts.LauncherFolderInfo(name)),
                new InfoText(AppTexts.LauncherFolderCheckInfo(folderChoice.Expected)),
                new InfoFacts("ORT",
                [
                    new InfoFact(AppTexts.Chosen, preferences.Folder ?? "–", IsMuted: preferences.Folder is null),
                    new InfoFact(AppTexts.FoundAt, launchers.LocationOf(id) ?? AppTexts.NotFound, IsMuted: !isFound),
                ]),
            ];
        }

        var scannedAt = host.ScannedAt(id);
        var counts = library is { Games.Count: > 0 } && isFound ? TypeCountLine.Of(library.Games) : null;
        _rescanRow.IsEnabled = isActive && isFound;
        _rescanRow.Counts = counts;
        _rescanRow.Subtitle = (isActive, isFound, library) switch
        {
            (false, _, _) => AppTexts.OnlyForActiveLauncher,
            (true, false, _) => AppTexts.NotFound,
            (true, true, null) => AppTexts.Scanning,
            (true, true, { Games.Count: 0 }) when scannedAt is null => LauncherTexts.GameCount(0),
            (true, true, { Games.Count: 0 }) => AppTexts.GamesScannedAgo(games: LauncherTexts.GameCount(0), ago: Ago(scannedAt)),
            (true, true, _) when scannedAt is null => null,
            // After the count line (#141).
            (true, true, _) => AppTexts.ScannedAgo(Ago(scannedAt)),
        };
        _rescanRow.Info =
        [
            new InfoText(AppTexts.LauncherRescanInfo(name)),
        ];

        var optionRows = launchers.LibraryOptions(id).Select(option => OptionRow(id, option));
        return [.. showsFolder ? [_folderRow] : Array.Empty<SettingsRowViewModel>(), .. optionRows, _rescanRow];
    }

    private string Ago(DateTimeOffset? at) => LauncherTexts.Ago(_category.Time, at);

    /// <summary>
    /// A switch that adds software, tools or soundtracks to the launcher's library, as the launcher offers it. Acts at once
    /// and is kept in the settings; the tab shows the entries without reading the launcher again.
    /// </summary>
    private SwitchRowViewModel OptionRow(string launcherId, Arcadia.Launchers.Abstractions.LibraryTypeOption option)
    {
        var key = $"library-option:{launcherId}:{option.Id}";
        if (!_optionRows.TryGetValue(key, out var row))
        {
            row = new SwitchRowViewModel(key, option.Title, () => _category.Host.Launchers.IsOptionOn(launcherId, option.Id), on => SwitchOption(launcherId, option, on))
            {
                Group = LibraryGroup,
                Subtitle = option.Subtitle,
                Info =
                [
                    new InfoText(option.Info),
                    .. option.Examples.Select(example => new InfoBullet(example)),
                    new InfoFootnote(AppTexts.OptionDefaultOffImmediate),
                ],
            };
            _optionRows[key] = row;
        }

        row.Refresh();
        return row;
    }

    private void SwitchOption(string launcherId, Arcadia.Launchers.Abstractions.LibraryTypeOption option, bool on)
    {
        _category.Changes.Apply(settings => settings.WithLauncherPreferences(launcherId, p => p.WithLibraryOption(option.Id, on)), option.Title);
        AppLog.Write($"{launcherId}: „{option.Title}“ {(on ? "an" : "aus")}");
        _category.Host.LibraryTypesChanged();
        _category.Refresh();
    }

    /// <summary>
    /// "Ordner wählen …" (#138): the folder dialog; Arcadia takes the folder only if the launcher really lies there, then
    /// switches the launcher on if it isn't yet and reads it, so its games appear.
    /// </summary>
    private async Task PickFolderAsync()
    {
        var host = _category.Host;
        if (_category.PageLauncherId is not { } id || host.Launchers.Registered(id)?.FolderChoice is not { } choice || host.Dialogs is not { } dialogs || _category.IsPicking)
        {
            return;
        }

        _category.IsPicking = true;
        try
        {
            var name = host.Launchers.Info(id).DisplayName;
            if (await dialogs.PickFolderAsync(AppTexts.LauncherPickFolder(name)) is not { } folder)
            {
                return;
            }

            if (await Task.Run(() => choice.Find(folder)) is not { } root)
            {
                host.Warn(AppTexts.LauncherFolderWrong(launcher: name, expected: choice.Expected));
                return;
            }

            _category.Changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { Folder = root }), "Launcher-Ordner");
            AppLog.Write($"Ordner für {id} gewählt");
            var active = host.Launchers.ActiveIds;
            if (active.Contains(id))
            {
                host.RescanLauncher(id);
                _category.Refresh();
            }
            else
            {
                _order.Choose(LauncherSelection.Placed(active, id, active.Count), null);
            }
        }
        finally
        {
            _category.IsPicking = false;
        }
    }
}
