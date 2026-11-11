using System.Buffers.Binary;

namespace Arcadia.Core.Artwork;

/// <summary>The size of an image Arcadia can show, read from its file.</summary>
public readonly record struct ImageSize(int Width, int Height);

/// <summary>
/// Whether a file is a whole PNG, JPEG or WebP image, and its size, without decoding it (#487): its header names the size,
/// its end shows it isn't cut off. Cheap enough to check every cached image once per session; a download is checked the same
/// way before it is kept. What the check can't see (damage inside the image data) shows when the image is decoded.
/// </summary>
public static class ImageCheck
{
    /// <summary>The largest image Arcadia takes, as for own files (#13).</summary>
    public const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>The largest side Arcadia takes; larger is no cover or background but a mistake or an attack.</summary>
    private const int MaxSide = 16384;

    /// <summary>
    /// The most pixels Arcadia takes: a small file can name a huge image, and a PNG is decoded whole before it is scaled
    /// (16384² px would be 1 GB). 40 MP is eight times a 4K background.
    /// </summary>
    private const long MaxPixels = 40_000_000;

    // Enough for the size in a PNG's or WebP's header; a JPEG's segments are read where they lie.
    private const int HeadBytes = 32;

    // A JPEG may carry some padding after its end marker.
    private const int TailBytes = 256;

    /// <summary>The image's size; null if the file is missing, unreadable, too large, cut off or no image.</summary>
    public static ImageSize? OfFile(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
            return Of(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The image's size; null if <paramref name="image"/> is too large, cut off or no image. Needs a seekable stream.</summary>
    public static ImageSize? Of(Stream image)
    {
        var length = image.Length;
        if (length is < 16 or > MaxBytes)
        {
            return null;
        }

        Span<byte> head = stackalloc byte[(int)Math.Min(HeadBytes, length)];
        image.Position = 0;
        image.ReadExactly(head);
        Span<byte> tail = stackalloc byte[(int)Math.Min(TailBytes, length)];
        image.Position = length - tail.Length;
        image.ReadExactly(tail);

        ImageSize? size = head switch
        {
            [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => Png(head, tail),
            [0xFF, 0xD8, ..] => Jpeg(image, tail),
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => WebP(head, length),
            _ => null,
        };
        return size is { Width: > 0 and <= MaxSide, Height: > 0 and <= MaxSide } shown && (long)shown.Width * shown.Height <= MaxPixels
            ? size
            : null;
    }

    private static ImageSize? Png(ReadOnlySpan<byte> head, ReadOnlySpan<byte> tail)
    {
        // The IHDR chunk comes first; a whole file ends with the IEND chunk.
        if (head.Length < 24 || !head.Slice(12, 4).SequenceEqual("IHDR"u8) || tail.LastIndexOf("IEND"u8) < 0)
        {
            return null;
        }

        return new ImageSize(BinaryPrimitives.ReadInt32BigEndian(head[16..]), BinaryPrimitives.ReadInt32BigEndian(head[20..]));
    }

    private static ImageSize? Jpeg(Stream image, ReadOnlySpan<byte> tail)
    {
        // A whole file ends with EOI (FF D9), maybe followed by padding; FF D9 can't occur in the image data before it.
        if (tail.LastIndexOf([(byte)0xFF, (byte)0xD9]) < 0)
        {
            return null;
        }

        // The segments before the image data name their lengths; the frame header (SOFn) holds the size.
        var position = 2L;
        Span<byte> segment = stackalloc byte[9];
        while (position + 4 <= image.Length)
        {
            image.Position = position;
            if (image.ReadByte() != 0xFF)
            {
                return null;
            }

            var marker = image.ReadByte();
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }

            image.Position = position + 2;
            var read = image.Read(segment);
            if (read < 2)
            {
                return null;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(segment);
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                return read < 7 ? null : new ImageSize(BinaryPrimitives.ReadUInt16BigEndian(segment[5..]), BinaryPrimitives.ReadUInt16BigEndian(segment[3..]));
            }

            if (marker is 0xD9 or 0xDA || segmentLength < 2)
            {
                return null;
            }

            position += 2 + segmentLength;
        }

        return null;
    }

    private static ImageSize? WebP(ReadOnlySpan<byte> head, long length)
    {
        // The RIFF header names the file's length; a shorter file is cut off.
        if (head.Length < 30 || BinaryPrimitives.ReadUInt32LittleEndian(head[4..]) + 8L > length)
        {
            return null;
        }

        var chunk = head.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8) && head[23] == 0x9D && head[24] == 0x01 && head[25] == 0x2A)
        {
            return new ImageSize(BinaryPrimitives.ReadUInt16LittleEndian(head[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(head[28..]) & 0x3FFF);
        }

        if (chunk.SequenceEqual("VP8L"u8) && head[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(head[21..]);
            return new ImageSize((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            return new ImageSize(Read24(head[24..]) + 1, Read24(head[27..]) + 1);
        }

        return null;
    }

    private static int Read24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
}
