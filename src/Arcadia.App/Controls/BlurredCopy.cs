using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Arcadia.App.Controls;

/// <summary>
/// The blurred copy of a background (#565): the image shrunk to <see cref="Width"/> pixels and blurred there with a few box
/// passes (close to a Gaussian), so drawn scaled up over a whole frame it is soft without blocks. Made once per decoded image
/// off the UI thread; drawing it costs no more than any small bitmap.
/// </summary>
internal static class BlurredCopy
{
    /// <summary>How wide the copy is; on the full-screen page one of its pixels covers 30 screen pixels.</summary>
    public const int Width = 64;

    private const int Radius = 2;
    private const int Passes = 3;

    /// <summary>The blurred copy of <paramref name="image"/>; any thread.</summary>
    public static Bitmap Of(Bitmap image)
    {
        var height = Math.Max(1, (int)Math.Round(Width * image.Size.Height / image.Size.Width));
        var small = image.CreateScaledBitmap(new PixelSize(Width, height), BitmapInterpolationMode.HighQuality);
        if (small.Format is not { BitsPerPixel: 32 } format)
        {
            // Not four bytes a pixel: the shrunk image alone is still soft enough.
            return small;
        }

        using (small)
        {
            var stride = Width * 4;
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                small.CopyPixels(new PixelRect(0, 0, Width, height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }

            Blur(pixels, Width, height);
            var blurred = new WriteableBitmap(new PixelSize(Width, height), new Vector(96, 96), format, small.AlphaFormat ?? AlphaFormat.Premul);
            using (var target = blurred.Lock())
            {
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(pixels, y * stride, target.Address + (y * target.RowBytes), stride);
                }
            }

            return blurred;
        }
    }

    /// <summary>Box blur in both directions, <see cref="Passes"/> times, every channel alike; edges repeat their last pixel.</summary>
    internal static void Blur(byte[] pixels, int width, int height)
    {
        var buffer = new byte[pixels.Length];
        for (var pass = 0; pass < Passes; pass++)
        {
            BoxPass(pixels, buffer, step: 4, lineStep: width * 4, length: width, lines: height);
            BoxPass(buffer, pixels, step: width * 4, lineStep: 4, length: height, lines: width);
        }
    }

    // One direction: each pixel becomes the mean of the 2 × Radius + 1 pixels around it along the line.
    private static void BoxPass(byte[] from, byte[] to, int step, int lineStep, int length, int lines)
    {
        var count = (2 * Radius) + 1;
        for (var line = 0; line < lines; line++)
        {
            var start = line * lineStep;
            for (var channel = 0; channel < 4; channel++)
            {
                var sum = 0;
                for (var i = -Radius; i <= Radius; i++)
                {
                    sum += from[start + (Math.Clamp(i, 0, length - 1) * step) + channel];
                }

                for (var i = 0; i < length; i++)
                {
                    to[start + (i * step) + channel] = (byte)(sum / count);
                    sum += from[start + (Math.Min(i + Radius + 1, length - 1) * step) + channel];
                    sum -= from[start + (Math.Max(i - Radius, 0) * step) + channel];
                }
            }
        }
    }
}
