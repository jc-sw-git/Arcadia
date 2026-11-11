using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Settings;

/// <summary>
/// What a launcher's row in the list shows (#136): its state or status message, its tab, and the info panel with the
/// library, where it was found and what Arcadia watches.
/// </summary>
internal sealed class LauncherRowStates(ILauncherCategory category)
{
    public void Show(LauncherRowViewModel row, IReadOnlyList<LauncherRowViewModel> tabs)
    {
        var host = category.Host;
        var launchers = host.Launchers;
        var id = row.Launcher.Id;
        var name = row.Launcher.DisplayName;
        var place = tabs.ToList().IndexOf(row);
        var isActive = place >= 0;
        var isFound = launchers.IsFound(id);
        var library = isActive ? host.LibraryOf(id) : null;
        var keyword = $"{SearchQuery.PrefixMarker}{row.Launcher.SearchKeyword}";

        // The status message: short in the row instead of the state, the whole text atop the info panel.
        (string Text, HealthSeverity Severity)? notice = null;
        string? note = null;
        if (isActive && !isFound)
        {
            notice = (AppTexts.NotFound, HealthSeverity.Warning);
            note = AppTexts.LauncherNotFoundAnymoreSwitchOff(name);
        }
        else if (library is { Games.Count: 0 })
        {
            // Nothing is broken and nothing needs doing: information, not a warning (owner, 2026-10-24).
            notice = (AppTexts.NoGamesFound, HealthSeverity.Info);
            note = AppTexts.LauncherNoGamesInfo(name);
        }

        var status = (isActive, isFound, library) switch
        {
            (true, _, { } shown) => $"{LauncherTexts.GameCount(shown.Games.Count)} · {keyword}",
            (true, _, null) => AppTexts.Scanning,
            (false, true, _) => AppTexts.Installed,
            _ => AppTexts.NotFound,
        };

        var tabNumber = isActive ? place + 1 : (int?)null;
        var integration = launchers.Registered(id);
        var location = launchers.LocationOf(id);
        InfoBlock[] preview = [new InfoTabBar([.. tabs.Select(tab => new InfoTab(tab.Launcher.DisplayName, tab.Badge, tab == row))])];

        // Whether it is active, its tab, key and keyword are in the preview and the row already (owner, 2026-10-22).
        List<InfoBlock> info = [new InfoLauncher(name, row.Badge, isActive ? AppTexts.LauncherActiveTab(tabNumber ?? 0) : isFound ? AppTexts.NotActive : AppTexts.NotInstalled, isActive)];
        if (notice is { } message && note is not null)
        {
            info.Add(new InfoNote(note, message.Severity));
        }

        var scannedAt = host.ScannedAt(id);
        info.Add(new InfoFacts(AppTexts.LibraryGroup,
        [
            library is null
                ? new InfoFact(AppTexts.FactGames, isActive ? AppTexts.FactScanning : AppTexts.FactNotScanned, IsMuted: true)
                : library.Games.Count == 0 ? new InfoFact(AppTexts.FactGames, LauncherTexts.GameCount(0))
                : CountsFact(TypeCountLine.Of(library.Games)),
            new InfoFact(AppTexts.FactLastScanned, LauncherTexts.Ago(category.Time, scannedAt), IsMuted: scannedAt is null),
        ]));
        info.Add(new InfoFacts(AppTexts.FactsLocation,
        [
            new InfoFact(AppTexts.FoundAt, location ?? AppTexts.NotFound, IsMuted: location is null),
            new InfoFact(AppTexts.FactInstalls, (isActive, integration?.InstallMonitor) switch
            {
                (false, _) => AppTexts.FactNotWatched,
                (true, null) => AppTexts.FactOnlyStartedFromArcadia,
                _ => AppTexts.FactWatched,
            }, IsMuted: !isActive, IsGood: isActive && integration?.InstallMonitor is not null),
            new InfoFact(AppTexts.FactRunningGames, (isActive, integration?.RunningGames) switch
            {
                (false, _) => AppTexts.FactNotWatched,
                (true, null) => AppTexts.FactDetectedByProcesses,
                _ => AppTexts.FactReportedByLauncher,
            }, IsMuted: !isActive, IsGood: isActive),
        ]));

        row.Show(isActive, isFound, tabNumber, status, notice, preview, info);
    }

    // The same count line as beside the tab's name (#141); the words are the tooltip's.
    private static InfoFact CountsFact(TypeCountLine counts) => new(AppTexts.FactEntries, counts.Tip, Counts: counts);
}
