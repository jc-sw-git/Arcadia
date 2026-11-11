using Arcadia.Core.Artwork;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#487: an image is kept only when it is whole and Arcadia can show it (T487-6, T487-13).</summary>
public sealed class ImageCheckTests
{
    private static ImageSize? Check(byte[] content) => ImageCheck.Of(new MemoryStream(content));

    [Fact]
    public void Reads_the_size_of_a_png_and_a_jpeg()
    {
        Assert.Equal(new ImageSize(60, 90), Check(TestImages.Png(60, 90)));
        Assert.Equal(new ImageSize(600, 900), Check(TestImages.Jpeg(600, 900)));
    }

    [Fact]
    public void Reads_the_size_of_a_webp()
    {
        // A lossy WebP's frame header: RIFF, its length, WEBP, the VP8 chunk with the key frame's start code and size.
        var webp = new byte[64];
        "RIFF"u8.CopyTo(webp);
        BitConverter.GetBytes(webp.Length - 8).CopyTo(webp, 4);
        "WEBPVP8 "u8.CopyTo(webp.AsSpan(8));
        webp[23] = 0x9D;
        webp[24] = 0x01;
        webp[25] = 0x2A;
        BitConverter.GetBytes((ushort)1920).CopyTo(webp, 26);
        BitConverter.GetBytes((ushort)620).CopyTo(webp, 28);

        Assert.Equal(new ImageSize(1920, 620), Check(webp));
        Assert.Null(Check(webp[..40]));
    }

    [Theory]
    [InlineData(6000, 6000, true)]
    [InlineData(7000, 7000, false)]
    [InlineData(16384, 16384, false)]
    [InlineData(20000, 100, false)]
    public void An_image_with_more_than_40_megapixels_is_none(int width, int height, bool taken)
    {
        // A small file whose header names a huge image: decoding it would take gigabytes.
        var png = TestImages.Png(60, 90);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), height);

        Assert.Equal(taken ? new ImageSize(width, height) : null, Check(png));
    }

    [Fact]
    public void A_cut_off_image_is_none()
    {
        var png = TestImages.Png(60, 90);
        var jpeg = TestImages.Jpeg(600, 900);

        Assert.Null(Check(png[..(png.Length - 20)]));
        Assert.Null(Check(jpeg[..(jpeg.Length - 2)]));
    }

    [Fact]
    public void An_error_page_or_an_empty_answer_is_none()
    {
        Assert.Null(Check("<html><body>404 Not Found</body></html>"u8.ToArray()));
        Assert.Null(Check([]));
    }

    [Fact]
    public void An_image_larger_than_20_MB_is_none()
    {
        var jpeg = TestImages.Jpeg(600, 900);
        var large = new byte[ImageCheck.MaxBytes + 1];
        jpeg.AsSpan(0, jpeg.Length - 2).CopyTo(large);
        large[^2] = 0xFF;
        large[^1] = 0xD9;

        Assert.Null(Check(large));
    }

    [Fact]
    public void A_missing_file_is_none() =>
        Assert.Null(ImageCheck.OfFile(Path.Combine(Path.GetTempPath(), "arcadia-no-such-image-" + Guid.NewGuid().ToString("N") + ".png")));
}
