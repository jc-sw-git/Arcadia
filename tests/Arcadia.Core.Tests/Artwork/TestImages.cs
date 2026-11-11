using System.Buffers.Binary;
using Arcadia.Launchers.Demo;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>Made-up images for the artwork tests (#487): real PNGs, and JPEGs whose structure (not their pixels) is whole.</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height) => DemoArtwork.Png(width, height, x => ((byte)x, 80, 160));

    /// <summary>Start, a frame header with the size, end: what <c>ImageCheck</c> reads of a JPEG.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        var frame = new byte[19];
        frame[0] = 0xFF;
        frame[1] = 0xC0;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 17);
        frame[4] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(5), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(7), (ushort)width);
        frame[9] = 3;
        bytes.AddRange(frame);
        bytes.AddRange(new byte[200]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    /// <summary>A JPEG as <see cref="Jpeg"/>, padded inside to exactly <paramref name="length"/> bytes (the 20 MB limit, #490).</summary>
    public static byte[] JpegOfLength(int width, int height, long length)
    {
        var jpeg = Jpeg(width, height);
        var content = new byte[length];
        jpeg.AsSpan(0, jpeg.Length - 2).CopyTo(content);
        content[^2] = 0xFF;
        content[^1] = 0xD9;
        return content;
    }

    /// <summary>The header of an animated WebP (VP8X with the animation flag); Arcadia takes no animated images (#13).</summary>
    public static byte[] AnimatedWebP(int width, int height)
    {
        var content = new byte[64];
        "RIFF"u8.CopyTo(content);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(4), (uint)content.Length - 8);
        "WEBPVP8X"u8.CopyTo(content.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(16), 10);
        content[20] = 0x02;
        Write24(content.AsSpan(24), width - 1);
        Write24(content.AsSpan(27), height - 1);
        return content;
    }

    private static void Write24(Span<byte> target, int value)
    {
        target[0] = (byte)value;
        target[1] = (byte)(value >> 8);
        target[2] = (byte)(value >> 16);
    }

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}
