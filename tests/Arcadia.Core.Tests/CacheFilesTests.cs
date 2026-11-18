using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public sealed class CacheFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "arcadia-cachefiles-" + Guid.NewGuid());

    public CacheFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        CacheFiles.OpenTemporary.Value = null;
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_durable_write_flushes_the_new_file_to_the_disk_and_not_only_to_the_cache()
    {
        var path = Path.Combine(_folder, "user-data.json");
        File.WriteAllText(path, "old");
        var flushes = new List<bool>();
        CacheFiles.OpenTemporary.Value = temporary => new RecordingStream(temporary, flushes);

        CacheFiles.WriteDurably(path, "new"u8);

        Assert.Contains(true, flushes);
        Assert.DoesNotContain(false, flushes);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(_folder, "*" + CacheFiles.TemporarySuffix));
    }

    [Fact]
    public void A_cache_write_keeps_the_content_and_leaves_no_temporary_file_without_the_cost_of_a_disk_flush()
    {
        var path = Path.Combine(_folder, "cover.png");
        var flushes = new List<bool>();
        CacheFiles.OpenTemporary.Value = temporary => new RecordingStream(temporary, flushes);

        CacheFiles.WriteThroughTemporary(path, "cache"u8);

        Assert.DoesNotContain(true, flushes);
        Assert.Equal("cache", File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(_folder, "*" + CacheFiles.TemporarySuffix));
    }

    internal sealed class RecordingStream(string path, List<bool> flushes) : FileStream(path, FileMode.CreateNew, FileAccess.Write)
    {
        public override void Flush(bool flushToDisk)
        {
            flushes.Add(flushToDisk);
            base.Flush(flushToDisk);
        }
    }
}
