using Arcadia.Core.Diagnostics;

namespace Arcadia.Core.Tests;

public sealed class OldCacheLocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-caches-" + Guid.NewGuid());

    private string LocalAppData => Path.Combine(_root, "local");

    private string DataFolder => Path.Combine(_root, "roaming", "Arcadia");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Caches_move_with_their_files_and_leave_the_install_folder()
    {
        Write(Path.Combine(LocalAppData, "Arcadia", "artwork", "own", "cover.png"), "mine");
        Write(Path.Combine(LocalAppData, "Arcadia", "metadata", "1.json"), "{}");

        OldCacheLocation.Move(LocalAppData, DataFolder);

        Assert.Equal("mine", File.ReadAllText(Path.Combine(DataFolder, "artwork", "own", "cover.png")));
        Assert.True(File.Exists(Path.Combine(DataFolder, "metadata", "1.json")));
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "Arcadia", "artwork")));
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "Arcadia", "metadata")));
    }

    [Fact]
    public void Existing_new_files_win_and_old_only_files_are_added()
    {
        Write(Path.Combine(LocalAppData, "Arcadia", "artwork", "a.png"), "old");
        Write(Path.Combine(LocalAppData, "Arcadia", "artwork", "b.png"), "old-b");
        Write(Path.Combine(DataFolder, "artwork", "a.png"), "new");

        OldCacheLocation.Move(LocalAppData, DataFolder);

        Assert.Equal("new", File.ReadAllText(Path.Combine(DataFolder, "artwork", "a.png")));
        Assert.Equal("old-b", File.ReadAllText(Path.Combine(DataFolder, "artwork", "b.png")));
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "Arcadia", "artwork")));
    }

    [Fact]
    public void Nothing_to_move_is_fine()
    {
        OldCacheLocation.Move(LocalAppData, DataFolder);

        Assert.False(Directory.Exists(DataFolder));
    }
}
