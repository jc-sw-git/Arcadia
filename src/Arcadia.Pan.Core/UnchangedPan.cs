namespace Arcadia.Pan;

/// <summary>
/// Whether <c>--install</c> keeps the installed Pan as it is (#382, S2): a driver install over a Pan that already is
/// the one Arcadia brings would otherwise copy it again and recreate its service, which ends a running emulation and
/// restarts the puck once more. The service's configuration (description, recovery, pre-shutdown, DACL) is re-applied
/// in place then, so a kept service never differs from a new one.
/// </summary>
public static class UnchangedPan
{
    /// <summary>
    /// True when the service exists and the installed exe's SHA-256 equals the source's and, when Arcadia named one, the
    /// expected hash; anything else installs as before. A missing hash (no installed exe) never matches.
    /// </summary>
    public static bool IsKept(string? installedHash, string sourceHash, string? expectedHash, bool serviceExists) =>
        serviceExists
        && installedHash is not null
        && string.Equals(installedHash, sourceHash, StringComparison.OrdinalIgnoreCase)
        && (expectedHash is null || string.Equals(installedHash, expectedHash, StringComparison.OrdinalIgnoreCase));
}
