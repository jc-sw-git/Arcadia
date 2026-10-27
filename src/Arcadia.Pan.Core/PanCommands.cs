namespace Arcadia.Pan;

/// <summary>Pan.exe's command line, as far as it is free of Windows.</summary>
public static class PanCommands
{
    /// <summary>
    /// The commands that change the machine or run the guardian outside the service: they stop at once without
    /// administrator rights. The service itself (no command) runs as SYSTEM, <c>--version</c> only prints.
    /// </summary>
    public static bool NeedsAdministrator(string command) => command is "--install" or "--uninstall" or "--restore" or "--console";
}
