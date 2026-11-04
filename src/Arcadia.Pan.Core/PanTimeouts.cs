namespace Arcadia.Pan;

/// <summary>
/// How long each elevated step of Pan may take, and from those how long Arcadia waits for a whole command, so Arcadia
/// never reports a timeout while Pan is still within its steps' budgets (overall review of #243, finding 3).
/// </summary>
public static class PanTimeouts
{
    /// <summary>usbip-win2's installer; after it the setup is left running, never killed.</summary>
    public static readonly TimeSpan UsbipInstaller = TimeSpan.FromMinutes(5);

    /// <summary>HidHide's installer, whose last step hangs on some PCs; see <see cref="HidHideSetup"/>.</summary>
    public static readonly TimeSpan HidHideInstaller = TimeSpan.FromMinutes(2);

    /// <summary>Restarting a connected Steam Controller after HidHide's setup.</summary>
    public static readonly TimeSpan PuckRestart = TimeSpan.FromSeconds(10);

    /// <summary>Each driver's uninstaller at <c>--uninstall --remove-drivers</c>.</summary>
    public static readonly TimeSpan DriverUninstaller = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Pan's own part: stopping the service with a full teardown (up to 90 s), the restore, replacing, configuring and
    /// starting the service; with room to spare.
    /// </summary>
    public static readonly TimeSpan ServiceSteps = TimeSpan.FromMinutes(3);

    /// <summary>The last removal of stale device entries at <c>--uninstall</c> (#347): it stops between two entries after this.</summary>
    public static readonly TimeSpan StaleDevices = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long elevated Pan waits for Arcadia's hand-over after the prompt (#413): Arcadia's steps up to it (VIIPER, the
    /// checks, closing Steam, the downloads) each have their own timeout, and a test in Arcadia pins that their sum plus a
    /// margin stays below this, so Pan never gives up while Arcadia still works within its limits. Fixed here, as Pan
    /// can't see Arcadia's timeouts.
    /// </summary>
    public static readonly TimeSpan HandOver = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long elevated Pan waits for Arcadia's <c>go</c> after the restore's prompt (#471): Arcadia only connects and
    /// lets go of Pan in between, each with its own limit; a test in Arcadia pins that they stay below this.
    /// </summary>
    public static readonly TimeSpan RestoreHandOver = TimeSpan.FromMinutes(5);

    public static TimeSpan Install => UsbipInstaller + HidHideInstaller + PuckRestart + ServiceSteps;

    public static TimeSpan Uninstall => ServiceSteps + StaleDevices + DriverUninstaller + DriverUninstaller;

    public static TimeSpan Restore => ServiceSteps;
}
