using System.Reflection;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The <c>viiper.exe</c> Arcadia ships (#242 part B): the build of its fork pinned in <c>Viiper.targets</c>, compiled
/// in as assembly metadata so the build and "Installieren" can never fetch different files.
/// </summary>
public static class ViiperRelease
{
    public const string ExecutableName = "viiper.exe";

    /// <summary>The fork's commit the build is made from; empty while no build is pinned.</summary>
    public static string Commit { get; } = Metadata("ViiperCommit");

    /// <summary>Lower-case hex; empty while no build is pinned.</summary>
    public static string Sha256 { get; } = Metadata("ViiperSha256").ToLowerInvariant();

    public static long Size { get; } = long.TryParse(Metadata("ViiperSize"), out var size) ? size : 0;

    public static Uri DownloadUrl { get; } = new(Metadata("ViiperUrl"));

    /// <summary>A fork build is pinned: Arcadia ships it, and only it knows the copy's device type.</summary>
    public static bool IsPinned => Sha256.Length == 64;

    public static ReleaseAsset Asset => new(ExecutableName, DownloadUrl, Size, IsPinned ? Sha256 : null);

    private static string Metadata(string key) =>
        typeof(ViiperRelease).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value ?? string.Empty;
}
