using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests;

/// <summary>A launcher's own images (#490), one file per game and kind, in a folder of the test.</summary>
internal sealed class LauncherImages : ILauncherArtwork, IArtworkLookup, IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-launcher-images-").FullName;
    private readonly Dictionary<(string Key, ArtworkKind Kind), string> _files = [];

    public void Set(Game game, ArtworkKind kind, byte[] content)
    {
        var path = Path.Combine(_folder, $"{game.Id}-{kind}.png");
        TestImages.Write(path, content);
        _files[(game.Key, kind)] = path;
    }

    public IArtworkLookup Look() => this;

    public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) =>
        _files.TryGetValue((game.Key, kind), out var path) ? [new ArtworkCandidate("test", File: path)] : [];

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
