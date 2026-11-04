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
        LibraryNotice.PendingInstallsFinished { Games: var games } => (HealthSeverity.Info, games.Count == 1 ? $"„{games[0].Title}“ ist jetzt installiert." : $"{games.Count} Spiele sind jetzt installiert.", false),
        LibraryNotice.InstallEnded ended => (ended.Outcome is InstallOutcome.Completed or InstallOutcome.Cancelled or InstallOutcome.Uninstalled ? HealthSeverity.Info : HealthSeverity.Error, InstallEnded(ended), false),
        LibraryNotice.GameNotStarted notStarted => (HealthSeverity.Warning, GameNotStarted(notStarted), false),
        LibraryNotice.RescanStarted { Launcher: var launcher } => (HealthSeverity.Info, launcher is null ? "Alle Launcher werden neu eingelesen …" : $"{launcher.DisplayName} wird neu eingelesen …", true),
        LibraryNotice.RescanFinished { Launcher: var launcher } => RescanFinished(launcher, tabs),
        LibraryNotice.ScanFailed { Launchers: var launchers } => (HealthSeverity.Warning, ScanFailed([.. launchers.Select(l => l.DisplayName)], keptGames: true), false),
        _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, null),
    };

    private static string InstallEnded(LibraryNotice.InstallEnded ended)
    {
        var title = ended.Title ?? "Spiel";
        return ended.Outcome switch
        {
            InstallOutcome.Uninstalled => $"„{title}“ wurde deinstalliert.",
            InstallOutcome.Completed when ended.Completed > 1 => $"{ended.Completed} Spiele sind jetzt installiert.",
            InstallOutcome.Completed => $"„{title}“ ist jetzt installiert.",
            InstallOutcome.Cancelled => $"Installation von „{title}“ abgebrochen.",
            _ => $"Installation von „{title}“ ist fehlgeschlagen – bitte im Launcher nachsehen.",
        };
    }

    private static string GameNotStarted(LibraryNotice.GameNotStarted notStarted)
    {
        // A game that left the library meanwhile has no title to quote.
        var name = notStarted.Title is { } title ? $"„{title}“" : "Das Spiel";
        return notStarted.Refusal switch
        {
            null => $"{name} wurde nicht gestartet.",
            { Reason: { } reason } => $"{name}: {reason}",
            _ => $"{name}: {notStarted.Launcher.DisplayName} hat den Start abgebrochen.",
        };
    }

    private static (HealthSeverity Level, string Text, bool Sticky) RescanFinished(LauncherInfo? launcher, LauncherTabsViewModel tabs)
    {
        if (launcher is null)
        {
            // A failed launcher is what the player needs to hear of, not that the others were read (#369).
            var failed = tabs.Items.Where(tab => tab.ScanFailed).ToList();
            return tabs.Items.Count == 0 ? (HealthSeverity.Info, "Weiterhin ist kein Launcher aktiv.", false)
                : failed.Count > 0 ? (HealthSeverity.Warning, ScanFailed([.. failed.Select(tab => tab.Name)], keptGames: failed.Any(tab => tab.Count > 0)), false)
                : (HealthSeverity.Info, "Alle Launcher sind neu eingelesen.", false);
        }

        var name = launcher.DisplayName;
        return tabs.Find(launcher.Id) switch
        {
            null => (HealthSeverity.Info, $"{name} ist nicht aktiv.", false),
            { IsFound: false } => (HealthSeverity.Warning, $"{name} ist weiterhin nicht zu finden.", false),
            { ScanFailed: true, Count: var kept } => (HealthSeverity.Warning, ScanFailed([name], keptGames: kept > 0), false),
            { Count: 0 } => (HealthSeverity.Info, $"Weiterhin keine Spiele in {name}.", false),
            { Count: var count } => (HealthSeverity.Info, count == 1 ? $"1 Spiel in {name} gefunden." : $"{count} Spiele in {name} gefunden.", false),
        };
    }

    /// <summary>
    /// What a failed scan of <paramref name="names"/> means for the player: whether the games read before stay
    /// (<paramref name="keptGames"/>) or the tab says itself that it couldn't be read (#369).
    /// </summary>
    private static string ScanFailed(IReadOnlyList<string> names, bool keptGames)
    {
        var who = $"{string.Join(" und ", names)} konnte{(names.Count == 1 ? string.Empty : "n")}";
        return keptGames ? $"{who} nicht neu eingelesen werden; die zuletzt gelesenen Spiele bleiben." : $"{who} nicht eingelesen werden.";
    }
}
