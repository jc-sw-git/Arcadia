using System.Globalization;
using System.Security.Cryptography;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace Arcadia.App.Services;

/// <summary>A logo the user picked, copied into Arcadia's logo folder, or why it wasn't.</summary>
/// <param name="FileName">The copy's name in the logo folder.</param>
public sealed record LogoImport(string? FileName, string? Problem);

/// <summary>
/// The launchers' logos (#138): the launcher's own one, or a picture the user picked. A picked picture (PNG, SVG, ICO or
/// WebP) is checked (<see cref="LogoFile"/>), drawn at most <see cref="PictureSize"/> pixels wide and high and kept as a PNG
/// in Arcadia's logo folder, so Arcadia never reads the user's file again and every logo it shows is a plain picture.
/// </summary>
public sealed class LauncherLogos(SettingsService settings, string folder)
{
    /// <summary>The copy's longest side: sharp at the 64 px of the launcher page on a 4K screen.</summary>
    public const int PictureSize = 256;

    /// <summary>A picture with sides beyond this would take hundreds of megabytes to decode.</summary>
    private const int MaxSide = 4096;

    private const int MinSide = 16;

    private static readonly Dictionary<string, Geometry> Shapes = new(StringComparer.Ordinal);

    // Read once per file; a new picture gets a new file name.
    private readonly Dictionary<string, Bitmap?> _pictures = new(StringComparer.Ordinal);

    /// <summary>Next to settings.json, which a reinstall keeps, not in the install folder, which an update replaces.</summary>
    public static string DefaultFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arcadia", "logos");

    /// <summary>The launcher as it shows now. Call on the UI thread.</summary>
    public LauncherBadge BadgeOf(LauncherInfo launcher) => new(
        Formatting.Monogram(launcher.DisplayName),
        Formatting.LauncherTileBrush(launcher.Id),
        ShapeOf(launcher.Logo),
        settings.Current.PreferencesOf(launcher.Id).LogoFile is { } file ? PictureOf(file) : null);

    /// <summary>The user picked <paramref name="path"/> as the logo of <paramref name="launcherId"/>. Reads and writes files: runs off the UI thread.</summary>
    public Task<LogoImport> ImportAsync(string launcherId, string path) => Task.Run(() => Import(launcherId, path));

    /// <summary>The copy of a logo no longer used; a file that can't be deleted is left for later.</summary>
    public void Delete(string? fileName)
    {
        if (fileName is null || PathOf(fileName) is not { } path)
        {
            return;
        }

        _pictures.Remove(fileName);
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Altes Logo nicht gelöscht: {ex.Message}");
        }
    }

    private static Geometry? ShapeOf(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
        {
            return null;
        }

        if (!Shapes.TryGetValue(logo, out var shape))
        {
            try
            {
                // F1: filled like SVG fills a path by default (non-zero), so the holes of a letter stay holes.
                shape = StreamGeometry.Parse("F1 " + logo);
            }
            catch (FormatException ex)
            {
                // Later a plugin's; a broken logo shows the letter instead.
                AppLog.Write($"Launcher-Logo nicht lesbar: {ex.Message}");
                return null;
            }

            Shapes[logo] = shape;
        }

        return shape;
    }

    private Bitmap? PictureOf(string fileName)
    {
        if (_pictures.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        Bitmap? picture = null;
        if (PathOf(fileName) is { } path)
        {
            try
            {
                picture = new Bitmap(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                // Deleted or broken since: the launcher's own logo shows instead.
                AppLog.Write($"Eigenes Logo „{fileName}“ nicht lesbar: {ex.Message}");
            }
        }

        _pictures[fileName] = picture;
        return picture;
    }

    /// <summary>A file name from settings.json stays inside the logo folder, whatever was written there by hand.</summary>
    private string? PathOf(string fileName) =>
        fileName.Length > 0 && fileName == Path.GetFileName(fileName) && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(folder, fileName)
            : null;

    private LogoImport Import(string launcherId, string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return new LogoImport(null, "Die Datei gibt es nicht mehr.");
            }

            if (LogoFile.SizeProblem(file.Length) is { } sizeProblem)
            {
                return new LogoImport(null, sizeProblem);
            }

            var content = File.ReadAllBytes(path);
            var check = LogoFile.Check(content);
            if (check.Format is not { } format)
            {
                return new LogoImport(null, check.Problem);
            }

            var (picture, problem) = format == LogoFormat.Svg ? DrawSvg(content) : DrawPicture(content);
            if (picture is null)
            {
                return new LogoImport(null, problem);
            }

            using (picture)
            {
                return Save(launcherId, picture);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LogoImport(null, $"Die Datei ließ sich nicht lesen: {ex.Message}");
        }
    }

    private static (SKBitmap? Picture, string? Problem) DrawPicture(byte[] content)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Ico or SKEncodedImageFormat.Webp))
        {
            return (null, "Das Bild ließ sich nicht lesen.");
        }

        // Checked before decoding: a few kilobytes can claim a picture of gigabytes.
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width < MinSide || height < MinSide)
        {
            return (null, $"Das Bild ist kleiner als {MinSide} × {MinSide} Pixel.");
        }

        if (width > MaxSide || height > MaxSide)
        {
            return (null, $"Das Bild ist größer als {MaxSide} × {MaxSide} Pixel.");
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var full = new SKBitmap(info);
        if (codec.GetPixels(info, full.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return (null, "Das Bild ließ sich nicht lesen.");
        }

        var (fitWidth, fitHeight) = Fit(width, height);
        return (full.Resize(new SKImageInfo(fitWidth, fitHeight, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell)), null);
    }

    private static (SKBitmap? Picture, string? Problem) DrawSvg(byte[] content)
    {
        using var svg = new SKSvg();
        using var stream = new MemoryStream(content, writable: false);
        if (svg.Load(stream) is not { } drawing || drawing.CullRect is not { Width: > 0, Height: > 0 } bounds)
        {
            return (null, "Das SVG zeigt nichts.");
        }

        var (width, height) = Fit(bounds.Width, bounds.Height);
        var picture = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(picture);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(width / bounds.Width, height / bounds.Height);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(drawing);
        return (picture, null);
    }

    /// <summary>The longest side becomes <see cref="PictureSize"/>, the shape stays.</summary>
    private static (int Width, int Height) Fit(double width, double height)
    {
        var scale = PictureSize / Math.Max(width, height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private LogoImport Save(string launcherId, SKBitmap picture)
    {
        using var image = SKImage.FromBitmap(picture);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();

        // A new name for every picture, so a Bitmap read before never stands in for the new one.
        var name = $"{SafeName(launcherId)}-{Convert.ToHexStringLower(SHA256.HashData(bytes))[..12]}.png";
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, name);
        var temp = target + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, target, overwrite: true);
        AppLog.Write($"Eigenes Logo für {launcherId} übernommen ({picture.Width} × {picture.Height})");
        return new LogoImport(name, null);
    }

    /// <summary>A launcher id as part of a file name; a plugin's id may hold anything.</summary>
    private static string SafeName(string launcherId)
    {
        var safe = new string(launcherId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return safe.Length is > 0 and <= 32 ? safe : Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(launcherId)))[..12];
    }
}
