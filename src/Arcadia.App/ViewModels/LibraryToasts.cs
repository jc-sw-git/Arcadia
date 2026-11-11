using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>The words of the toasts for what <see cref="LibrarySession"/> notices (#228); the session itself shows nothing.</summary>
public static class LibraryToasts
{
    /// <summary>The toast's level and text, and whether it stays until the next one (a rescan running).</summary>
    public static (HealthSeverity Level, string Text, bool Sticky) For(LibraryNotice notice, LauncherTabsViewModel tabs) => notice switch
    {
        LibraryNotice.PendingInstallsFinished { Games: var games } => (HealthSeverity.Info, games.Count == 1 ? AppTexts.ToastGameInstalled(games[0].Title) : AppTexts.ToastGamesInstalled(games.Count), false),
        LibraryNotice.InstallEnded ended => (ended.Outcome is InstallOutcome.Completed or InstallOutcome.Cancelled or InstallOutcome.Uninstalled ? HealthSeverity.Info : HealthSeverity.Error, InstallEnded(ended), false),
        LibraryNotice.GameNotStarted notStarted => (HealthSeverity.Warning, GameNotStarted(notStarted), false),
        LibraryNotice.RescanStarted { Launcher: var launcher } => (HealthSeverity.Info, launcher is null ? AppTexts.ToastRescanningAll : AppTexts.ToastRescanning(launcher.DisplayName), true),
        LibraryNotice.RescanFinished { Launcher: var launcher } => RescanFinished(launcher, tabs),
        LibraryNotice.ScanFailed { Launchers: var launchers } => (HealthSeverity.Warning, ScanFailed([.. launchers.Select(l => l.DisplayName)], keptGames: true), false),
        _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, null),
    };

    private static string InstallEnded(LibraryNotice.InstallEnded ended)
    {
        var title = ended.Title ?? AppTexts.GameTypeGame;
        return ended.Outcome switch
        {
            InstallOutcome.Uninstalled => AppTexts.ToastGameUninstalled(title),
            InstallOutcome.Completed when ended.Completed > 1 => AppTexts.ToastGamesInstalled(ended.Completed),
            InstallOutcome.Completed => AppTexts.ToastGameInstalled(title),
            InstallOutcome.Cancelled => AppTexts.ToastInstallCancelled(title),
            _ => AppTexts.ToastInstallFailed(title),
        };
    }

    private static string GameNotStarted(LibraryNotice.GameNotStarted notStarted)
    {
        // A game that left the library meanwhile has no title to quote.
        var name = notStarted.Title is { } title ? AppTexts.QuotedTitle(title) : AppTexts.ToastTheGame;
        return notStarted.Refusal switch
        {
            null => AppTexts.ToastNotStarted(name),
            { Reason: { } reason } => $"{name}: {reason}",
            _ => AppTexts.ToastStartCancelledByLauncher(name, notStarted.Launcher.DisplayName),
        };
    }

    private static (HealthSeverity Level, string Text, bool Sticky) RescanFinished(LauncherInfo? launcher, LauncherTabsViewModel tabs)
    {
        if (launcher is null)
        {
            // A failed launcher is what the player needs to hear of, not that the others were read (#369).
            var failed = tabs.Items.Where(tab => tab.ScanFailed).ToList();
            return tabs.Items.Count == 0 ? (HealthSeverity.Info, AppTexts.ToastStillNoLauncher, false)
                : failed.Count > 0 ? (HealthSeverity.Warning, ScanFailed([.. failed.Select(tab => tab.Name)], keptGames: failed.Any(tab => tab.Count > 0)), false)
                : (HealthSeverity.Info, AppTexts.ToastAllRescanned, false);
        }

        var name = launcher.DisplayName;
        return tabs.Find(launcher.Id) switch
        {
            null => (HealthSeverity.Info, AppTexts.ToastLauncherNotActive(name), false),
            { IsFound: false } => (HealthSeverity.Warning, AppTexts.ToastLauncherStillNotFound(name), false),
            { ScanFailed: true, Count: var kept } => (HealthSeverity.Warning, ScanFailed([name], keptGames: kept > 0), false),
            { Count: 0 } => (HealthSeverity.Info, AppTexts.ToastStillNoGames(name), false),
            { Count: var count } => (HealthSeverity.Info, count == 1 ? AppTexts.ToastFoundOneGame(name) : AppTexts.ToastFoundGames(count, name), false),
        };
    }

    /// <summary>
    /// What a failed scan of <paramref name="names"/> means for the player: whether the games read before stay
    /// (<paramref name="keptGames"/>) or the tab says itself that it couldn't be read (#369).
    /// </summary>
    private static string ScanFailed(IReadOnlyList<string> names, bool keptGames)
    {
        var who = names.Count == 1 ? AppTexts.LauncherCould(names[0]) : AppTexts.LaunchersCould(string.Join(AppTexts.JoinAnd, names));
        return keptGames ? AppTexts.ToastRescanFailedKeep(who) : AppTexts.ToastScanFailed(who);
    }
}
