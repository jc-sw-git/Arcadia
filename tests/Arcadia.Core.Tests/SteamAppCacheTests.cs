using System.Text;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class SteamAppCacheTests
{
    [Fact]
    public void Reads_the_app_ids_of_all_packages_except_the_base_package()
    {
        var file = SteamCacheFiles.PackageInfo(
            (0, [7, 480]),
            (56, [211, 215]),
            (1213, [367520]));

        Assert.Equal([211u, 215u, 367520u], SteamAppCache.ReadOwnedAppIds(file).Order());
    }

    [Fact]
    public void Reads_name_and_type_of_the_wanted_apps_only()
    {
        var file = SteamCacheFiles.AppInfo(
            (367520, "Hollow Knight ", "Game"),
            (228980, "Steamworks Common Redistributables", "Tool"),
            (1030301, "Silksong Soundtrack", "Music"));

        var apps = SteamAppCache.ReadAppInfo(file, new HashSet<uint> { 367520, 1030301, 999 });

        Assert.Equal(2, apps.Count);
        Assert.Equal(new SteamAppInfo("Hollow Knight", "game"), apps[367520]);
        Assert.Equal("music", apps[1030301].Type);
    }

    [Fact]
    public void Rejects_unknown_versions_and_truncated_files()
    {
        var packages = SteamCacheFiles.PackageInfo((56, [211]));
        var apps = SteamCacheFiles.AppInfo((367520, "Hollow Knight", "Game"));
        var wanted = new HashSet<uint> { 367520 };

        Assert.Throws<FormatException>(() => SteamAppCache.ReadOwnedAppIds(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }));
        Assert.Throws<FormatException>(() => SteamAppCache.ReadOwnedAppIds(packages.AsSpan(0, packages.Length - 6)));
        Assert.Throws<FormatException>(() => SteamAppCache.ReadAppInfo(apps.AsSpan(0, 40), wanted));
        Assert.Throws<FormatException>(() => SteamAppCache.ReadAppInfo([], wanted));
    }
}

/// <summary>Builds Steam's binary caches the way Steam writes them (packageinfo v40, appinfo v29).</summary>
internal static class SteamCacheFiles
{
    public static byte[] PackageInfo(params (uint Id, int[] Apps)[] packages)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x06565528u);
        writer.Write(1u);
        foreach (var (id, apps) in packages)
        {
            writer.Write(id);
            writer.Write(new byte[20 + 4 + 8]);
            writer.Write((byte)0);
            CString(writer, id.ToString());
            Int(writer, "packageid", (int)id);
            writer.Write((byte)0);
            CString(writer, "appids");
            for (var i = 0; i < apps.Length; i++)
            {
                Int(writer, i.ToString(), apps[i]);
            }

            writer.Write((byte)8);
            writer.Write((byte)8);
            writer.Write((byte)8);
        }

        writer.Write(uint.MaxValue);
        return stream.ToArray();
    }

    public static byte[] AppInfo(params (uint Id, string Name, string Type)[] apps)
    {
        string[] keys = ["appinfo", "appid", "common", "name", "type", "gameid"];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x07564429u);
        writer.Write(1u);
        var tableOffsetAt = stream.Position;
        writer.Write(0L);
        foreach (var (id, name, type) in apps)
        {
            using var entry = new MemoryStream();
            using (var body = new BinaryWriter(entry, Encoding.UTF8, leaveOpen: true))
            {
                body.Write(new byte[4 + 4 + 8 + 20 + 4 + 20]);
                body.Write((byte)0); body.Write(0u); // appinfo
                body.Write((byte)2); body.Write(1u); body.Write((int)id); // appid
                body.Write((byte)0); body.Write(2u); // common
                body.Write((byte)1); body.Write(3u); CString(body, name);
                body.Write((byte)1); body.Write(4u); CString(body, type);
                body.Write((byte)7); body.Write(5u); body.Write((ulong)id); // gameid, a uint64
                body.Write((byte)8);
                body.Write((byte)8);
                body.Write((byte)8);
            }

            writer.Write(id);
            writer.Write((uint)entry.Length);
            writer.Write(entry.ToArray());
        }

        writer.Write(0u);
        var tableOffset = stream.Position;
        writer.Write((uint)keys.Length);
        foreach (var key in keys)
        {
            CString(writer, key);
        }

        stream.Position = tableOffsetAt;
        writer.Write(tableOffset);
        return stream.ToArray();
    }

    private static void Int(BinaryWriter writer, string key, int value)
    {
        writer.Write((byte)2);
        CString(writer, key);
        writer.Write(value);
    }

    private static void CString(BinaryWriter writer, string text)
    {
        writer.Write(Encoding.UTF8.GetBytes(text));
        writer.Write((byte)0);
    }
}
