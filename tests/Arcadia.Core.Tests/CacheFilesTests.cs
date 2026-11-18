using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public sealed class CacheFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "arcadia-cachefiles-" + Guid.NewGuid());

    public CacheFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Writing_has_the_content_flushed_before_it_replaces_the_old_file()
    {
        var path = Path.Combine(_folder, "user-data.json");
        File.WriteAllText(path, "old");
        string? atFlush = null;
        string? oldAtFlush = null;

        CacheFiles.WriteThroughTemporary(path, "new"u8, file =>
        {
            file.Flush();
            atFlush = Directory.EnumerateFiles(_folder, "*" + CacheFiles.TemporarySuffix).Single();
            oldAtFlush = File.ReadAllText(path);
        });

        Assert.NotNull(atFlush);
        Assert.Equal("old", oldAtFlush);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(_folder, "*" + CacheFiles.TemporarySuffix));
    }

    [Fact]
    public void The_default_write_flushes_to_the_disk_and_keeps_the_content()
    {
        var path = Path.Combine(_folder, "data.json");

        CacheFiles.WriteThroughTemporary(path, "content"u8);

        Assert.Equal("content", File.ReadAllText(path));
    }
}
