using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// The exit codes of <c>Pan.exe</c> (constants of <c>Program</c> in Pan) and the message Arcadia shows for each; the
/// list with what they mean is in <c>docs/controller-support/pan-setup.md</c>.
/// </summary>
public static class PanExitCodes
{
    /// <summary><c>--install</c> couldn't secure the data folder.</summary>
    public const int DataFolder = 4;

    /// <summary><c>--restore</c> or <c>--uninstall</c> could not undo everything.</summary>
    public const int RestoreIncomplete = 5;

    /// <summary><c>--install</c> set Pan up but not HidHide.</summary>
    public const int HidHideFailed = 6;

    /// <summary><c>--uninstall --remove-drivers</c> left a driver.</summary>
    public const int DriversNotRemoved = 7;

    /// <summary>Exit 9 that Arcadia didn't cause (#413): Pan waited in vain or the pipe was taken; nothing was changed.</summary>
    public const string HandOverEndedText =
        "Pan hat die Installation beendet, bevor Arcadia die Installer übergeben konnte. Es wurde nichts verändert – bitte noch einmal „Installieren“ wählen.";

    /// <summary>Today's message for each exit code other than 0.</summary>
    public static Exception ErrorFor(int exitCode, string command) => exitCode switch
    {
        // Pan could not log this: nothing of it may be written into a folder it doesn't own.
        DataFolder => new InvalidOperationException($"Pan konnte seinen Datenordner {PanPaths.DataDirectory} nicht sichern – bitte den Ordner prüfen oder entfernen und es noch einmal versuchen."),
        RestoreIncomplete => new InvalidOperationException($"Pan konnte nicht alles aufräumen – das Journal bleibt erhalten; Details stehen in {PanPaths.LogPath}. Ein Neustart von Pan oder \"Pan.exe --restore\" versucht es noch einmal."),
        HidHideFailed => new InvalidOperationException($"Pan ist eingerichtet, HidHide aber nicht – Details stehen in {PanPaths.LogPath}. HidHide lässt sich auch selbst installieren: {HidHideRelease.ManualInstallUrl}"),
        DriversNotRemoved => new InvalidOperationException($"Pan ist entfernt, aber nicht jeder Treiber – Details stehen in {PanPaths.LogPath}; der Rest lässt sich unter Windows-Einstellungen → Apps entfernen."),
        // When Arcadia aborted, it shows its own failed step's message instead.
        InstallHandOver.EndedExitCode => new InvalidOperationException(HandOverEndedText),
        _ => new InvalidOperationException($"Pan {command} meldete Fehler {exitCode} – Details stehen in {PanPaths.LogPath}."),
    };
}
