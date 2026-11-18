using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Arcadia.Core.Library;

/// <summary>What Arcadia's caches of game data (artwork, metadata) share: names from game keys, writes that are never half done.</summary>
public static class CacheFiles
{
    /// <summary>The ending of a file still being written; one left over is a write that broke off.</summary>
    public const string TemporarySuffix = ".tmp";

    /// <summary>A name longer than this is a hash instead; the file's content keeps the key.</summary>
    private const int MaxName = 100;

    /// <summary>
    /// A file or folder name for <paramref name="gameKey"/>: every character other than a-z, 0-9 and '-' written as <c>~</c>
    /// and its UTF-8 bytes in hex, so no key holds a separator, a dot, a drive or a name two keys share (also on a file
    /// system that ignores case).
    /// </summary>
    public static string NameOf(string gameKey)
    {
        var name = new StringBuilder(gameKey.Length + 8);
        foreach (var b in Encoding.UTF8.GetBytes(gameKey))
        {
            if (b is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'-')
            {
                name.Append((char)b);
            }
            else
            {
                name.Append('~').Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        return name.Length <= MaxName ? name.ToString() : "~" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(gameKey)))[..32];
    }

    /// <summary>
    /// A test's way to watch how the temporary file is flushed (a power cut can't be tested): opens the stream in its place,
    /// for the test's own flow of work only (also the writes it starts on other threads).
    /// </summary>
    internal static readonly AsyncLocal<Func<string, FileStream>?> OpenTemporary = new();

    /// <summary>
    /// Writes <paramref name="path"/> through a temporary file and a rename: the old file stays until the new one is complete.
    /// For what Arcadia can fetch again (artwork, metadata): a file a power cut leaves empty is discarded when read.
    /// </summary>
    public static void WriteThroughTemporary(string path, ReadOnlySpan<byte> content) => Write(path, content, flushToDisk: false);

    /// <summary>
    /// The same, and the new file is on the disk before it replaces the old one: for what the player entered and Arcadia
    /// can't fetch again (own titles and tags). A flush to the file system's cache alone can leave an empty file after a
    /// power cut. Not for caches: a flush to disk per file made 1,000 cache writes slow the Windows test runs.
    /// </summary>
    public static void WriteDurably(string path, ReadOnlySpan<byte> content) => Write(path, content, flushToDisk: true);

    private static void Write(string path, ReadOnlySpan<byte> content, bool flushToDisk)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N")[..8] + TemporarySuffix;
        using (var file = OpenTemporary.Value?.Invoke(temporary) ?? new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        {
            file.Write(content);
            file.Flush(flushToDisk);
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Removes what writes that broke off left in <paramref name="folder"/>.</summary>
    public static void RemoveTemporaryFiles(string folder)
    {
        foreach (var leftover in Directory.EnumerateFiles(folder, "*" + TemporarySuffix))
        {
            File.Delete(leftover);
        }
    }
}
