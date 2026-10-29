using System.Text;

namespace Arcadia.Core.Tests;

/// <summary>
/// Writes Steam's <c>appcache/packageinfo.vdf</c> for tests (#83), in the layout <see cref="Arcadia.Launchers.Steam.SteamPackageInfo"/>
/// reads: header, one entry per package with its apps under <c>"&lt;id&gt;"/appids</c>, package id <c>0xFFFFFFFF</c>.
/// </summary>
internal static class PackageInfoFile
{
    public const uint V39 = 0x06565527;
    public const uint V40 = 0x06565528;

    public static byte[] Build(uint version, IEnumerable<(uint PackageId, uint[] AppIds)> packages)
    {
        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);
        writer.Write(version);
        writer.Write(1u); // universe
        foreach (var (packageId, appIds) in packages)
        {
            writer.Write(packageId);
            writer.Write(new byte[version == V39 ? 24 : 32]); // SHA-1, change number, v40's PICS token
            writer.Write((byte)0x00);
            Key(writer, packageId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.Write((byte)0x02);
            Key(writer, "packageid");
            writer.Write(packageId);
            writer.Write((byte)0x00);
            Key(writer, "appids");
            for (var i = 0; i < appIds.Length; i++)
            {
                writer.Write((byte)0x02);
                Key(writer, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.Write(appIds[i]);
            }

            writer.Write((byte)0x08); // appids
            writer.Write((byte)0x08); // the package
            writer.Write((byte)0x08); // the entry
        }

        writer.Write(0xFFFFFFFFu);
        writer.Flush();
        return file.ToArray();
    }

    private static void Key(BinaryWriter writer, string key)
    {
        writer.Write(Encoding.UTF8.GetBytes(key));
        writer.Write((byte)0);
    }
}
