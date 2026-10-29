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
            return tabs.Items.Count == 0 ? "Weiterhin ist kein Launcher aktiv." : "Alle Launcher sind neu eingelesen.";
        }

        var name = launcher.DisplayName;
        return tabs.Find(launcher.Id) switch
        {
            null => $"{name} ist nicht aktiv.",
            { IsFound: false } => $"{name} ist weiterhin nicht zu finden.",
            { Count: 0 } => $"Weiterhin keine Spiele in {name}.",
            { Count: var count } => count == 1 ? $"1 Spiel in {name} gefunden." : $"{count} Spiele in {name} gefunden.",
        };
    }
}
