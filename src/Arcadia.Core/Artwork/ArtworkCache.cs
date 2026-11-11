using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// Where an image came from (#13): a launcher's own (also the player's own images in the launcher, #487), or a file the
/// player chose in "Artwork ändern" (#490), or SteamGridDB (#489).
/// </summary>
public enum ArtworkSource
{
    Launcher,
    File,
    SteamGridDb,
}

/// <summary>
/// One image in the cache: its file in the game's folder, where it came from and whether the player set it by hand.
/// </summary>
/// <param name="File">The file name in the game's folder, e.g. <c>cover.jpg</c>.</param>
/// <param name="Origin">The launcher's <see cref="ArtworkCandidate.Origin"/>, e.g. <c>steam-cache</c>.</param>
/// <param name="Stamp">What the original looked like when it was copied (length and time of a file, the address of a
/// download), so a changed original is copied again (#487, T487-17).</param>
public sealed record CachedImage(string File, ArtworkSource Source, string Origin, string Stamp, bool SetByHand = false);

/// <summary>What the cache holds for one game.</summary>
/// <param name="Cover">The cover in use.</param>
/// <param name="Hero">The background in use.</param>
/// <param name="OwnCover">The player's own file for the cover (#490), kept while another image is in use, so "Eigene Datei"
/// still offers it; in use when <paramref name="Cover"/> names the same file.</param>
/// <param name="OwnHero">The player's own file for the background, as <paramref name="OwnCover"/>.</param>
/// <param name="Unreadable">Its metadata couldn't be read: every image in the folder counts as set by hand (#13).</param>
public sealed record CachedArtwork(
    string GameKey, CachedImage? Cover, CachedImage? Hero, bool Unreadable = false, CachedImage? OwnCover = null, CachedImage? OwnHero = null)
{
    public CachedImage? Of(ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;

    public CachedImage? OwnOf(ArtworkKind kind) => kind == ArtworkKind.Cover ? OwnCover : OwnHero;

    /// <summary>Neither an image in use nor an own file: the game's folder can go.</summary>
    public bool IsEmpty => Cover is null && Hero is null && OwnCover is null && OwnHero is null;

    public CachedArtwork With(ArtworkKind kind, CachedImage? image) => kind == ArtworkKind.Cover ? this with { Cover = image } : this with { Hero = image };

    public CachedArtwork WithOwn(ArtworkKind kind, CachedImage? image) => kind == ArtworkKind.Cover ? this with { OwnCover = image } : this with { OwnHero = image };
}

/// <summary>
/// The artwork cache on disk (#13, #487): <c>%LOCALAPPDATA%\Arcadia\artwork\&lt;game&gt;\cover|hero.*</c> plus
/// <c>artwork.json</c> with source and "set by hand" per image, and the player's own files as <c>own-cover|own-hero.*</c>
/// (#490). Every file is written to a temporary file first and then
/// renamed, so a crash never leaves half an image or half a metadata file; temporary files left by one are removed on the
/// game's next write. A game's folder name is made from its key so that no key leads outside the cache.
/// </summary>
/// <remarks>Not thread-safe: one writer at a time (<see cref="ArtworkService"/>'s run).</remarks>
public sealed class ArtworkCache(string root)
{
    public const string MetadataFile = "artwork.json";

    /// <summary>
    /// Images "Artwork ändern" offers (#490) wait here until the player takes one; nothing in it is in use, and it is thrown
    /// away at every start. No game's folder can be called so: a key's dot is written as <c>~2e</c>.
    /// </summary>
    private const string SuggestionsFolder = ".suggestions";

    private const string TemporarySuffix = ".tmp";

    /// <summary>A folder name longer than this is a hash instead; the metadata keeps the key.</summary>
    private const int MaxFolderName = 100;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>
    /// The game's folder: the launcher id and the native id with every character other than a-z, 0-9 and '-' written as
    /// <c>~</c> and its UTF-8 bytes in hex, so no key holds a separator, a dot, a drive or a name two keys share (also on
    /// a file system that ignores case).
    /// </summary>
    public string FolderOf(string gameKey)
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

        var folder = name.Length <= MaxFolderName ? name.ToString() : "~" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(gameKey)))[..32];
        return Path.Combine(Root, folder);
    }

    public string PathOf(string gameKey, CachedImage image) => Path.Combine(FolderOf(gameKey), image.File);

    /// <summary>Every game the cache holds something for, read from the metadata of each folder.</summary>
    public IEnumerable<CachedArtwork> All()
    {
        if (!Directory.Exists(Root))
        {
            yield break;
        }

        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            if (ReadFolder(folder, gameKey: null) is { } artwork)
            {
                yield return artwork;
            }
        }
    }

    /// <summary>What the cache holds for the game; null if nothing.</summary>
    public CachedArtwork? Read(string gameKey) => ReadFolder(FolderOf(gameKey), gameKey);

    private CachedArtwork? ReadFolder(string folder, string? gameKey)
    {
        var metadata = Path.Combine(folder, MetadataFile);
        if (!File.Exists(metadata))
        {
            // Images without metadata are left from a run that ended before it wrote it: they count as missing.
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredArtwork>(File.ReadAllText(metadata), Json);
            if (stored?.Game is { Length: > 0 } key && (gameKey is null || key == gameKey) && FolderOf(key) == folder)
            {
                return new CachedArtwork(
                    key,
                    Checked(stored.Cover, ArtworkKind.Cover, inUse: true),
                    Checked(stored.Hero, ArtworkKind.Hero, inUse: true),
                    OwnCover: Checked(stored.OwnCover, ArtworkKind.Cover, inUse: false),
                    OwnHero: Checked(stored.OwnHero, ArtworkKind.Hero, inUse: false));
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Unreadable below.
        }

        if (gameKey is null)
        {
            // The key isn't known without the metadata: such a folder is only found again by its game's key.
            return null;
        }

        return Unreadable(folder, gameKey);
    }

    /// <summary>
    /// A stored image whose file name is one of ours; anything else (a path, another name) counts as missing. An image in use
    /// may be the own file (then it is from a file); an own file is only ever one.
    /// </summary>
    private static CachedImage? Checked(CachedImage? image, ArtworkKind kind, bool inUse)
    {
        if (image is not { File: { } file } || image.Origin is null || image.Stamp is null)
        {
            return null;
        }

        var own = image.Source == ArtworkSource.File;
        return (inUse || own) && IsFileName(file, BaseName(kind, own)) ? image : null;
    }

    private static bool IsFileName(string file, string baseName) =>
        Path.GetFileNameWithoutExtension(file) == baseName && Extensions.Contains(Path.GetExtension(file)) && Path.GetFileName(file) == file;

    private static readonly string[] Extensions = [".png", ".jpg", ".webp"];

    private static string BaseName(ArtworkKind kind, bool own = false) => (own ? "own-" : string.Empty) + (kind == ArtworkKind.Cover ? "cover" : "hero");

    /// <summary>The metadata can't be read: the images in the folder count as set by hand, so nothing overwrites them (#13).</summary>
    private static CachedArtwork Unreadable(string folder, string gameKey)
    {
        CachedImage? Found(ArtworkKind kind, bool own) => Extensions
            .Select(extension => BaseName(kind, own) + extension)
            .Where(file => File.Exists(Path.Combine(folder, file)))
            .Select(file => new CachedImage(file, own ? ArtworkSource.File : ArtworkSource.Launcher, "unknown", "unknown", SetByHand: true))
            .FirstOrDefault();

        // Which image was in use isn't known any more: the launcher's copy where there is one, else the own file.
        var ownCover = Found(ArtworkKind.Cover, own: true);
        var ownHero = Found(ArtworkKind.Hero, own: true);
        return new CachedArtwork(
            gameKey, Found(ArtworkKind.Cover, own: false) ?? ownCover, Found(ArtworkKind.Hero, own: false) ?? ownHero, Unreadable: true, ownCover, ownHero);
    }

    /// <summary>
    /// Writes the image into the game's folder, through a temporary file: the old image stays until the new one is complete.
    /// Removes the image of the same kind with another extension. The metadata is the caller's next step.
    /// </summary>
    /// <param name="extension">The file's type, from its content: <c>.png</c>, <c>.jpg</c> or <c>.webp</c>.</param>
    /// <param name="own">The player's own file (#490), kept beside the image in use.</param>
    /// <returns>The file name written.</returns>
    public string WriteImage(string gameKey, ArtworkKind kind, ReadOnlySpan<byte> content, string extension, bool own = false)
    {
        var folder = Directory.CreateDirectory(FolderOf(gameKey)).FullName;
        RemoveTemporaryFiles(folder);
        var file = BaseName(kind, own) + extension;
        WriteThroughTemporary(Path.Combine(folder, file), content);
        foreach (var other in Extensions.Where(e => e != extension))
        {
            File.Delete(Path.Combine(folder, BaseName(kind, own) + other));
        }

        return file;
    }

    /// <summary>Writes the game's metadata; with no image left, removes the game's folder.</summary>
    public void WriteMetadata(CachedArtwork artwork)
    {
        var folder = FolderOf(artwork.GameKey);
        if (artwork.IsEmpty)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            return;
        }

        Directory.CreateDirectory(folder);
        var stored = new StoredArtwork { Game = artwork.GameKey, Cover = artwork.Cover, Hero = artwork.Hero, OwnCover = artwork.OwnCover, OwnHero = artwork.OwnHero };
        WriteThroughTemporary(Path.Combine(folder, MetadataFile), JsonSerializer.SerializeToUtf8Bytes(stored, Json));
    }

    /// <summary>Deletes the game's launcher copy of <paramref name="kind"/>, whatever its extension; the own file stays.</summary>
    public void DeleteImage(string gameKey, ArtworkKind kind)
    {
        var folder = FolderOf(gameKey);
        foreach (var extension in Extensions)
        {
            File.Delete(Path.Combine(folder, BaseName(kind) + extension));
        }
    }

    /// <summary>
    /// Keeps an image "Artwork ändern" offers (#490) until the player takes one or the next offer of the same game and kind
    /// replaces it; each gets a name of its own, so a view never shows an older one by its path.
    /// </summary>
    /// <returns>The file's full path.</returns>
    public string WriteSuggestion(string gameKey, ArtworkKind kind, ReadOnlySpan<byte> content, string extension)
    {
        var folder = Directory.CreateDirectory(Path.Combine(Root, SuggestionsFolder, Path.GetFileName(FolderOf(gameKey)))).FullName;
        var path = Path.Combine(folder, BaseName(kind) + "-" + Guid.NewGuid().ToString("N")[..12] + extension);
        WriteThroughTemporary(path, content);
        return path;
    }

    /// <summary>Removes the offered images of the game and kind, before it is offered anew.</summary>
    public void ClearSuggestions(string gameKey, ArtworkKind kind)
    {
        var folder = Path.Combine(Root, SuggestionsFolder, Path.GetFileName(FolderOf(gameKey)));
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, BaseName(kind) + "-*"))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>Removes every offered image; at the start, when no view shows one.</summary>
    public void ClearAllSuggestions()
    {
        var folder = Path.Combine(Root, SuggestionsFolder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>True for a file <see cref="WriteSuggestion"/> wrote, so only those can be taken as an offer.</summary>
    public bool IsSuggestion(string path) =>
        Path.GetFullPath(path).StartsWith(Path.Combine(Root, SuggestionsFolder) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>The extension for an image's content, by its first bytes; null for none Arcadia shows.</summary>
    public static string? ExtensionOf(ReadOnlySpan<byte> content) => content switch
    {
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => ".png",
        [0xFF, 0xD8, ..] => ".jpg",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ".webp",
        _ => null,
    };

    private static void WriteThroughTemporary(string path, ReadOnlySpan<byte> content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N")[..8] + TemporarySuffix;
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        {
            file.Write(content);
            file.Flush();
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static void RemoveTemporaryFiles(string folder)
    {
        foreach (var leftover in Directory.EnumerateFiles(folder, "*" + TemporarySuffix))
        {
            File.Delete(leftover);
        }
    }

    private sealed class StoredArtwork
    {
        public string? Game { get; set; }

        public CachedImage? Cover { get; set; }

        public CachedImage? Hero { get; set; }

        public CachedImage? OwnCover { get; set; }

        public CachedImage? OwnHero { get; set; }
    }
}
