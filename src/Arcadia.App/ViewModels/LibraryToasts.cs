using Arcadia.App.Services;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>The words of the toasts for what <see cref="LibrarySession"/> notices (#228); the session itself shows nothing.</summary>
public static class LibraryToasts
{
    /// <summary>The toast's text, and whether it stays until the next one (a rescan running).</summary>
    public static (string Text, bool Sticky) For(LibraryNotice notice, LauncherTabsViewModel tabs) => notice switch
    {
        LibraryNotice.PendingInstallsFinished { Games: var games } => (games.Count == 1 ? $"„{games[0].Title}“ ist jetzt installiert." : $"{games.Count} Spiele sind jetzt installiert.", false),
        LibraryNotice.InstallEnded ended => (InstallEnded(ended), false),
        LibraryNotice.GameNotStarted notStarted => (GameNotStarted(notStarted), false),
        LibraryNotice.RescanStarted { Launcher: var launcher } => (launcher is null ? "Alle Launcher werden neu eingelesen …" : $"{launcher.DisplayName} wird neu eingelesen …", true),
        LibraryNotice.RescanFinished { Launcher: var launcher } => (RescanFinished(launcher, tabs), false),
        LibraryNotice.ScanFailed { Launchers: var launchers } => (ScanFailed([.. launchers.Select(l => l.DisplayName)], keptGames: true), false),
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

    private static string RescanFinished(LauncherInfo? launcher, LauncherTabsViewModel tabs)
    {
        if (launcher is null)
        {
            // A failed launcher is what the player needs to hear of, not that the others were read (#369).
            var failed = tabs.Items.Where(tab => tab.ScanFailed).ToList();
            return tabs.Items.Count == 0 ? "Weiterhin ist kein Launcher aktiv."
                : failed.Count > 0 ? ScanFailed([.. failed.Select(tab => tab.Name)], keptGames: failed.Any(tab => tab.Count > 0))
                : "Alle Launcher sind neu eingelesen.";
        }

        var name = launcher.DisplayName;
        return tabs.Find(launcher.Id) switch
        {
            null => $"{name} ist nicht aktiv.",
            { IsFound: false } => $"{name} ist weiterhin nicht zu finden.",
            { ScanFailed: true, Count: var kept } => ScanFailed([name], keptGames: kept > 0),
            { Count: 0 } => $"Weiterhin keine Spiele in {name}.",
            { Count: var count } => count == 1 ? $"1 Spiel in {name} gefunden." : $"{count} Spiele in {name} gefunden.",
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
