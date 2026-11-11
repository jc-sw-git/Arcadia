using System.Buffers.Binary;
using System.IO.Compression;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>
/// Made-up covers and backgrounds for the demo libraries of <c>--demo-games</c> (#487), so measuring a large library against
/// the budgets in <c>CLAUDE.md</c> decodes real images: a few colour bands as PNG files in a folder of their own, shared
/// round the games, which Arcadia copies into its cache like a launcher's.
/// </summary>
public sealed class DemoArtwork(string folder) : ILauncherArtwork
{
    private const int Variants = 12;

    private readonly Lock _gate = new();

    public IArtworkLookup Look()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(folder);
            for (var i = 0; i < Variants; i++)
            {
                var epic = InEpicsFormats(i);
                Write(PathOf(ArtworkKind.Cover, i), epic ? 1200 : 600, epic ? 1600 : 900, i);
                Write(PathOf(ArtworkKind.Hero, i), epic ? 2560 : 1920, epic ? 1440 : 620, i);
            }
        }

        return new Lookup(this);
    }

    /// <summary>Every fourth in Epic's catalog formats (#488), 3 : 4 and 16 : 9, so the crop to 2 : 3 and 3.1 : 1 can be looked at.</summary>
    private static bool InEpicsFormats(int variant) => variant % 4 == 3;

    private string PathOf(ArtworkKind kind, int variant) =>
        Path.Combine(folder, $"{(kind == ArtworkKind.Cover ? "cover" : "hero")}-{variant}{(InEpicsFormats(variant) ? "-epic" : string.Empty)}.png");

    private static void Write(string path, int width, int height, int variant)
    {
        if (!File.Exists(path))
        {
            var hue = variant * 360.0 / Variants;
            File.WriteAllBytes(path, Png(width, height, x => Colour(hue + (x * 40.0 / width), 0.55, 0.35 + (0.2 * x / width))));
        }
    }

    /// <summary>
    /// A PNG of <paramref name="width"/> × <paramref name="height"/>, every row alike, coloured per column by
    /// <paramref name="column"/>; also the tests' way to make a real image.
    /// </summary>
    public static byte[] Png(int width, int height, Func<int, (byte R, byte G, byte B)> column)
    {
        var row = new byte[1 + (width * 3)];
        for (var x = 0; x < width; x++)
        {
            (row[1 + (x * 3)], row[2 + (x * 3)], row[3 + (x * 3)]) = column(x);
        }

        using var pixels = new MemoryStream();
        using (var zlib = new ZLibStream(pixels, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.Write(row);
            }
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR"u8, header);
        Chunk(png, "IDAT"u8, pixels.ToArray());
        Chunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        var typed = new byte[4 + data.Length];
        type.CopyTo(typed);
        data.CopyTo(typed.AsSpan(4));
        png.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typed));
        png.Write(number);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private static (byte, byte, byte) Colour(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var h = (hue % 360) / 60;
        var x = c * (1 - Math.Abs((h % 2) - 1));
        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        var m = lightness - (c / 2);
        return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private sealed class Lookup(DemoArtwork artwork) : IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind)
        {
            var variant = (int)((uint)StableHash(game.Id) % Variants);
            return [new ArtworkCandidate("demo", File: artwork.PathOf(kind, variant))];
        }

        // string.GetHashCode changes per process; the same game keeps its image across starts.
        private static int StableHash(string text)
        {
            var hash = 17;
            foreach (var c in text)
            {
                hash = unchecked((hash * 31) + c);
            }

            return hash;
        }
    }
}
