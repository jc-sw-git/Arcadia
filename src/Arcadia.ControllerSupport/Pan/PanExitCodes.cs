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

    /// <summary>Exit 9 of a driver install that Arcadia didn't cause (#413): Pan waited in vain or the pipe was taken; nothing was changed.</summary>
    public static string HandOverEndedText =>
        ControllerSupportTexts.PanHandOverEnded;

    /// <summary>Exit 9 of a restore that Arcadia didn't cause (#471): Pan waited in vain for the go; nothing was restored.</summary>
    public static string RestoreHandOverEndedText =>
        ControllerSupportTexts.PanRestoreHandOverEnded;

    /// <summary>Today's message for each exit code other than 0.</summary>
    public static Exception ErrorFor(int exitCode, string command) => exitCode switch
    {
        // Pan could not log this: nothing of it may be written into a folder it doesn't own.
        DataFolder => new InvalidOperationException(ControllerSupportTexts.PanDataFolderNotSaved(PanPaths.DataDirectory)),
        RestoreIncomplete => new InvalidOperationException(ControllerSupportTexts.PanCleanupIncomplete(PanPaths.LogPath)),
        HidHideFailed => new InvalidOperationException(ControllerSupportTexts.PanHidHideNotInstalled(logPath: PanPaths.LogPath, url: HidHideRelease.ManualInstallUrl)),
        DriversNotRemoved => new InvalidOperationException(ControllerSupportTexts.PanRemovedDriverLeft(PanPaths.LogPath)),
        // When Arcadia aborted, it shows its own failed step's message instead.
        InstallHandOver.EndedExitCode => new InvalidOperationException(command == "--restore" ? RestoreHandOverEndedText : HandOverEndedText),
        _ => new InvalidOperationException(ControllerSupportTexts.PanCommandFailed(command: command, exitCode: exitCode, logPath: PanPaths.LogPath)),
    };
}
