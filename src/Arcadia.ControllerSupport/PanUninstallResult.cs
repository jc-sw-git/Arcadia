namespace Arcadia.ControllerSupport;

/// <summary>How Pan's elevated uninstall went at Arcadia's uninstall.</summary>
public enum PanUninstallResult
{
    /// <summary>Nothing to remove, no Pan in the package, or Windows couldn't start it elevated.</summary>
    NotRun,

    /// <summary>The UAC prompt was declined: Pan and the drivers stay.</summary>
    Declined,

    /// <summary>Pan runs elevated; it may still be at work (hand-off, timeout) or have failed partway.</summary>
    Started,
}
