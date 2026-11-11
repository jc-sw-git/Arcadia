namespace Arcadia.ControllerSupport;

/// <summary>Whether a driver change waits for the Windows restart, and whether Pan pauses until then (#382, D1).</summary>
public enum PendingRestart
{
    None,

    /// <summary>
    /// usbip-win2 was installed or updated since Windows started: it works already, the restart only finishes it, so Pan
    /// keeps running and the row only recommends the restart (S6).
    /// </summary>
    Recommended,

    /// <summary>
    /// HidHide or usbip-win2 was uninstalled and is still half there until the restart: Pan pauses and nothing can be
    /// installed, as a setup would meet the half removed driver (S3).
    /// </summary>
    Required,
}
