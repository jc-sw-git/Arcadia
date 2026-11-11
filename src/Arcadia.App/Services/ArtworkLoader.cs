using Arcadia.Core.Artwork;
using Avalonia.Media.Imaging;

namespace Arcadia.App.Services;

/// <summary>
/// Decodes cached artwork for <see cref="Controls.ArtworkImage"/> (#487), at the size it is shown, never larger than the
/// file; an image it can't decode is reported to the cache as broken and logged once. Thread-safe: decoding runs off the UI
/// thread.
/// </summary>
public sealed class ArtworkLoader(ArtworkService? cache)
{
    // A few at a time: a page of covers decoded all at once would hold every full-size image in memory together.
    private readonly SemaphoreSlim _decoding = new(Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
    private int _decoded;

    /// <summary>How many images were decoded since the start; the tests count with it that only visible tiles load.</summary>
    public int Decoded => Volatile.Read(ref _decoded);

    /// <summary>An image was decoded, with the size it was decoded at; the tests see with it that images are decoded at the size shown.</summary>
    public event Action<ArtworkFile, Avalonia.PixelSize>? Loaded;

    /// <summary>The image <paramref name="width"/> pixels wide (at most its own width), or null if it can't be decoded; off the UI thread.</summary>
    public async Task<Bitmap?> DecodeAsync(ArtworkFile file, int width)
    {
        await _decoding.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Yield();
            using var stream = File.OpenRead(file.Path);
            var bitmap = width >= file.Size.Width ? new Bitmap(stream) : Bitmap.DecodeToWidth(stream, Math.Max(1, width), BitmapInterpolationMode.HighQuality);
            Interlocked.Increment(ref _decoded);
            Loaded?.Invoke(file, bitmap.PixelSize);
            return bitmap;
        }
        catch (Exception e)
        {
            // The file is damaged inside (the cache's check sees only its header and end), or gone since.
            AppLog.Write($"Artwork: {Path.GetFileName(file.Path)} von {file.GameKey} nicht lesbar: {e.GetType().Name}: {e.Message}");
            cache?.ReportBroken(file);
            return null;
        }
        finally
        {
            _decoding.Release();
        }
    }
}
