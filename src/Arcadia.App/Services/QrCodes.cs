using Avalonia;
using Avalonia.Media;
using QRCoder;

namespace Arcadia.App.Services;

/// <summary>QR codes drawn as shapes (#83), so the theme's colours fill them and they stay sharp at any size.</summary>
public static class QrCodes
{
    /// <summary>The side of every drawn code; the view scales it to its box.</summary>
    public const double Size = 100;

    /// <summary>
    /// The dark modules of <paramref name="text"/>'s QR code (error correction M), with the quiet zone of four modules
    /// around it, in a square of <see cref="Size"/>.
    /// </summary>
    public static Geometry Draw(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        var count = matrix.Count;
        var module = Size / count;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var y = 0; y < count; y++)
            {
                for (var x = 0; x < count; x++)
                {
                    if (!matrix[y][x])
                    {
                        continue;
                    }

                    // Each module a little larger, so neighbours touch without hairlines between them.
                    var rect = new Rect(x * module, y * module, module + 0.02, module + 0.02);
                    context.BeginFigure(rect.TopLeft, isFilled: true);
                    context.LineTo(rect.TopRight);
                    context.LineTo(rect.BottomRight);
                    context.LineTo(rect.BottomLeft);
                    context.EndFigure(isClosed: true);
                }
            }
        }

        return geometry;
    }
}
