using System.Security.Cryptography;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>Whether the installed Pan is the one this Arcadia brings (#297).</summary>
public enum PanCompatibility
{
    /// <summary>No Pan on this PC (neither service nor exe): Arcadia starts as usual.</summary>
    NotInstalled,

    /// <summary>The installed exe is byte for byte the bundled one.</summary>
    Compatible,

    /// <summary>Another Pan, older or newer, or one whose exe can't be read: replaced before the library shows.</summary>
    UpdateRequired,

    /// <summary>This Arcadia brings no Pan (a Debug build that skipped it): nothing to compare or update with.</summary>
    NotBundled,
}

/// <summary>
/// Compatible means exactly the bundled Pan (owner, 2026-10-28): no version range, so any other installed exe, newer or
/// older, is replaced. The comparison is by SHA-256, the hash <c>--install --expect-hash</c> checks too; file versions
/// alone miss a rebuilt Pan with the same version.
/// </summary>
public static class PanCompatibilityCheck
{
    /// <param name="installed">Pan's service or its exe in Program Files exists.</param>
    /// <param name="bundledHash">The bundled exe's SHA-256, or null if this Arcadia brings none.</param>
    /// <param name="installedHash">The installed exe's SHA-256, or null if it is missing or can't be read.</param>
    public static PanCompatibility Judge(bool installed, string? bundledHash, Func<string?> installedHash)
    {
        if (!installed)
        {
            return PanCompatibility.NotInstalled;
        }

        if (bundledHash is null)
        {
            return PanCompatibility.NotBundled;
        }

        // An unreadable exe or a service without one is an unknown state, which counts as incompatible.
        return string.Equals(installedHash(), bundledHash, StringComparison.OrdinalIgnoreCase)
            ? PanCompatibility.Compatible
            : PanCompatibility.UpdateRequired;
    }

    /// <summary>The file's SHA-256 as hex, or null if it is missing or can't be read.</summary>
    public static string? HashOf(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
