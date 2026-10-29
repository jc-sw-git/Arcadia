using System.Buffers.Binary;
using System.Globalization;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Reads which apps a package (a license) holds from Steam's binary package cache <c>appcache/packageinfo.vdf</c> (#83).
/// Steam's library counts free tools and soundtracks that come with a package but are in no owned-app list of the web
/// API or the store (owner's account, 2026-10-27: 169 tools, 120 of them from package 0, which every account has).
/// </summary>
/// <remarks>
/// Format (checked against the owner's file, 2026-10-27): magic and universe; then per package its id, SHA-1, change
/// number, in v40 a PICS token, then the package as binary VDF with string keys (<c>"&lt;id&gt;"/appids</c>); there is no
/// entry size, so every entry is parsed. Package id <c>0xFFFFFFFF</c> ends the entries. The file holds only the packages
/// Steam has seen on this PC, a few MB at most, so it is read whole.
/// </remarks>
public static class SteamPackageInfo
{
    // Header magics: version byte + "UV\x06", little endian.
    private const uint V39 = 0x06565527;
    private const uint V40 = 0x06565528;

    private const uint EndOfEntries = 0xFFFFFFFF;

    // SHA-1 and change number; v40 adds the PICS token.
    private const int EntryHeaderV39 = 20 + 4;
    private const int EntryHeaderV40 = EntryHeaderV39 + 8;

    // Far above any real file; a larger one is not read into memory.
    private const long MaxFileSize = 64 * 1024 * 1024;

    /// <summary>The apps of every package of <paramref name="packageIds"/> found in the file.</summary>
    /// <exception cref="FormatException">An unknown version, a broken or too large file.</exception>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read.</exception>
    public static IReadOnlyDictionary<uint, IReadOnlyList<uint>> ReadApps(string path, IReadOnlySet<uint> packageIds)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxFileSize)
        {
            throw new FormatException("packageinfo.vdf too large.");
        }

        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return ReadApps(data, packageIds);
    }

    /// <inheritdoc cref="ReadApps(string, IReadOnlySet{uint})"/>
    public static IReadOnlyDictionary<uint, IReadOnlyList<uint>> ReadApps(ReadOnlySpan<byte> data, IReadOnlySet<uint> packageIds)
    {
        var reader = new SteamAppInfo.Reader(data, strings: null);
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(reader.Take(4));
        if (magic is not (V39 or V40))
        {
            throw new FormatException($"Unknown packageinfo.vdf version 0x{magic:X8}.");
        }

        reader.Take(4); // universe
        var entryHeader = magic == V39 ? EntryHeaderV39 : EntryHeaderV40;
        var packages = new Dictionary<uint, IReadOnlyList<uint>>();
        while (packages.Count < packageIds.Count && reader.Position < data.Length)
        {
            var packageId = BinaryPrimitives.ReadUInt32LittleEndian(reader.Take(4));
            if (packageId == EndOfEntries)
            {
                break;
            }

            reader.Take(entryHeader);
            var entry = reader.Map();
            if (packageIds.Contains(packageId))
            {
                var appIds = entry.GetMap(packageId.ToString(CultureInfo.InvariantCulture))?.GetMap("appids");
                packages[packageId] = appIds is null ? [] : [.. appIds.Select(app => app.Value).OfType<int>().Select(appId => (uint)appId)];
            }
        }

        return packages;
    }
}
