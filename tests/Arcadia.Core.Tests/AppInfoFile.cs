using System.Buffers.Binary;
using System.Text;

namespace Arcadia.Core.Tests;

/// <summary>
/// Writes Steam's <c>appcache/appinfo.vdf</c> for tests (#141), in the layout <see cref="Arcadia.Launchers.Steam.SteamAppInfo"/>
/// reads: header, one entry per app with name and type under <c>appinfo/common</c>, app id 0, and in v29 the string table.
/// </summary>
internal static class AppInfoFile
{
    public const uint V27 = 0x07564427;
    public const uint V28 = 0x07564428;
    public const uint V29 = 0x07564429;

    public static byte[] Build(uint version, IEnumerable<(uint AppId, string Name, string Type)> apps)
    {
        var strings = new List<string>();
        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);
        writer.Write(version);
        writer.Write(1u); // universe
        var tableOffsetAt = file.Position;
        if (version == V29)
        {
            writer.Write(0L);
        }

        foreach (var (appId, name, type) in apps)
        {
            var vdf = Vdf(appId, name, type, version == V29 ? strings : null);
            var header = version == V27 ? 40 : 60;
            writer.Write(appId);
            writer.Write((uint)(header + vdf.Length));
            writer.Write(new byte[header]);
            writer.Write(vdf);
        }

        writer.Write(0u);
        if (version == V29)
        {
            var tableOffset = file.Position;
            writer.Write((uint)strings.Count);
            foreach (var text in strings)
            {
                writer.Write(Encoding.UTF8.GetBytes(text));
                writer.Write((byte)0);
            }

            file.Position = tableOffsetAt;
            writer.Write(tableOffset);
        }

        writer.Flush();
        return file.ToArray();
    }

    private static byte[] Vdf(uint appId, string name, string type, List<string>? strings)
    {
        using var stream = new MemoryStream();

        void Key(string key)
        {
            if (strings is null)
            {
                stream.Write(Encoding.UTF8.GetBytes(key));
                stream.WriteByte(0);
                return;
            }

            var index = strings.IndexOf(key);
            if (index < 0)
            {
                strings.Add(key);
                index = strings.Count - 1;
            }

            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)index);
            stream.Write(bytes);
        }

        void Text(string key, string value)
        {
            stream.WriteByte(0x01);
            Key(key);
            stream.Write(Encoding.UTF8.GetBytes(value));
            stream.WriteByte(0);
        }

        stream.WriteByte(0x00);
        Key("appinfo");
        stream.WriteByte(0x02);
        Key("appid");
        Span<byte> id = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(id, appId);
        stream.Write(id);
        stream.WriteByte(0x07);
        Key("change");
        stream.Write(new byte[8]);
        stream.WriteByte(0x00);
        Key("common");
        Text("name", name);
        Text("type", type);
        stream.WriteByte(0x08);
        stream.WriteByte(0x08);
        stream.WriteByte(0x08);
        return stream.ToArray();
    }
}
