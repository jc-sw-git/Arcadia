using System.Text;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>
/// Writes an <c>appinfo.vdf</c> v28 whose entries carry any VDF the test gives (#493): maps as nested dictionaries,
/// strings and ints as values. Made-up AppIDs and names only.
/// </summary>
internal static class AppInfoMetadataFile
{
    private const uint V28 = 0x07564428;

    public static byte[] Build(params (uint AppId, Dictionary<string, object> AppInfo)[] apps) => Build(apps, brokenApp: null);

    /// <param name="brokenApp">This app's entry is cut off inside its VDF (its frame still holds).</param>
    public static byte[] Build((uint AppId, Dictionary<string, object> AppInfo)[] apps, uint? brokenApp)
    {
        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);
        writer.Write(V28);
        writer.Write(1u);
        foreach (var (appId, appInfo) in apps)
        {
            var vdf = Vdf(new Dictionary<string, object> { ["appinfo"] = appInfo });
            if (appId == brokenApp)
            {
                vdf = vdf[..(vdf.Length / 2)];
            }

            writer.Write(appId);
            writer.Write((uint)(60 + vdf.Length));
            writer.Write(new byte[60]);
            writer.Write(vdf);
        }

        writer.Write(0u);
        writer.Flush();
        return file.ToArray();
    }

    private static byte[] Vdf(Dictionary<string, object> root)
    {
        using var stream = new MemoryStream();
        Write(stream, root);
        stream.WriteByte(0x08);
        return stream.ToArray();
    }

    private static void Write(Stream stream, Dictionary<string, object> map)
    {
        foreach (var (key, value) in map)
        {
            switch (value)
            {
                case Dictionary<string, object> inner:
                    stream.WriteByte(0x00);
                    Text(stream, key);
                    Write(stream, inner);
                    stream.WriteByte(0x08);
                    break;
                case int number:
                    stream.WriteByte(0x02);
                    Text(stream, key);
                    stream.Write(BitConverter.GetBytes(number));
                    break;
                default:
                    stream.WriteByte(0x01);
                    Text(stream, key);
                    Text(stream, value.ToString()!);
                    break;
            }
        }
    }

    private static void Text(Stream stream, string text)
    {
        stream.Write(Encoding.UTF8.GetBytes(text));
        stream.WriteByte(0);
    }

    /// <summary>A list as Steam writes one: a map with the keys 0, 1, 2, ….</summary>
    public static Dictionary<string, object> List(params object[] values) =>
        values.Select((value, i) => (i, value)).ToDictionary(e => e.i.ToString(System.Globalization.CultureInfo.InvariantCulture), e => e.value);
}
