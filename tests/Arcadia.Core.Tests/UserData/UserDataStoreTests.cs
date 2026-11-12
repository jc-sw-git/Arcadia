using Arcadia.Core.Library;
using Arcadia.Core.UserData;

namespace Arcadia.Core.Tests.UserData;

/// <summary>
/// #496: the file of own titles, kept per game key with the own tags (#495): written in the background through a
/// temporary file, never overwritten when unreadable, put back when a write fails (T496-14, T496-15).
/// </summary>
public sealed class UserDataStoreTests : IAsyncDisposable
{
    private const string Key = "steam:4711";

    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-user-data-").FullName;
    private readonly List<string> _log = [];
    private readonly List<UserDataStore> _stores = [];

    // A change writes in the background; Windows can't delete the folder while that write holds its temporary file.
    public async ValueTask DisposeAsync()
    {
        foreach (var store in _stores)
        {
            await store.WhenWrittenAsync();
        }

        Directory.Delete(_folder, recursive: true);
    }

    private string FilePath => Path.Combine(_folder, UserDataStore.FileName);

    private UserDataStore Open()
    {
        var store = new UserDataStore(_folder) { Log = _log.Add };
        _stores.Add(store);
        store.Open();
        return store;
    }

    [Fact]
    public async Task A_title_holds_at_once_and_after_a_restart()
    {
        var store = Open();
        var changed = new List<string>();
        store.Changed += (_, keys) => changed.AddRange(keys);

        Assert.True(store.Change(Key, data => data with { Title = "  Kölner Ära: „Straße“?  " }));

        Assert.Equal("Kölner Ära: „Straße“?", store.Of(Key).Title);
        Assert.Equal([Key], changed);
        await store.WhenWrittenAsync();
        Assert.Equal("Kölner Ära: „Straße“?", Open().Of(Key).Title);
    }

    [Fact]
    public async Task A_game_absent_from_the_library_keeps_its_title_and_others_stay()
    {
        var store = Open();
        store.Change(Key, data => data with { Title = "Aal" });
        store.Change("epic:other", data => data with { Title = "Zander" });
        await store.WhenWrittenAsync();

        var again = Open();

        Assert.Equal("Aal", again.Of(Key).Title);
        Assert.Equal("Zander", again.Of("epic:other").Title);
        Assert.Same(GameUserData.None, again.Of("steam:never-set"));
    }

    [Fact]
    public async Task Clearing_a_title_removes_the_game_from_the_file()
    {
        var store = Open();
        store.Change(Key, data => data with { Title = "Aal" });
        store.Change(Key, data => data with { Title = null });
        await store.WhenWrittenAsync();

        Assert.Null(Open().Of(Key).Title);
        Assert.DoesNotContain(Key, File.ReadAllText(FilePath));
    }

    [Fact]
    public void An_empty_title_clears_the_own_one_and_one_too_long_or_with_control_characters_is_refused()
    {
        var store = Open();
        store.Change(Key, data => data with { Title = "Aal" });

        store.Change(Key, data => data with { Title = new string('x', UserDataStore.MaxTitleLength + 1) });
        Assert.Equal("Aal", store.Of(Key).Title);
        store.Change(Key, data => data with { Title = "Aal\u0007" });
        Assert.Equal("Aal", store.Of(Key).Title);
        store.Change(Key, data => data with { Title = new string('x', UserDataStore.MaxTitleLength) });
        Assert.Equal(UserDataStore.MaxTitleLength, store.Of(Key).Title!.Length);
        store.Change(Key, data => data with { Title = "   " });
        Assert.Null(store.Of(Key).Title);
    }

    [Fact]
    public async Task A_write_that_broke_off_leaves_the_old_file_whole_and_its_rest_goes()
    {
        var store = Open();
        store.Change(Key, data => data with { Title = "Aal" });
        await store.WhenWrittenAsync();
        // What a crash during the next write leaves: the temporary file beside the complete old one.
        var leftover = FilePath + ".1a2b3c4d" + CacheFiles.TemporarySuffix;
        File.WriteAllText(leftover, "{ \"version\": 1, \"games\": { \"steam:4711\": { \"ti");

        var again = Open();

        Assert.Equal("Aal", again.Of(Key).Title);
        Assert.False(File.Exists(leftover));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"version\": 2, \"games\": {} }")]
    [InlineData("{ \"version\": 1, \"games\": { \"steam:4711\": { \"title\": 5 } } }")]
    public async Task An_unreadable_file_is_kept_copied_and_never_overwritten(string content)
    {
        File.WriteAllText(FilePath, content);

        var store = Open();

        Assert.True(store.IsUnreadable);
        Assert.Null(store.Of(Key).Title);
        Assert.False(store.Change(Key, data => data with { Title = "Aal" }));
        await store.WhenWrittenAsync();
        Assert.Equal(content, File.ReadAllText(FilePath));
        Assert.Equal(content, File.ReadAllText(FilePath + UserDataStore.CopySuffix));
        var line = Assert.Single(_log);
        Assert.Contains(UserDataStore.FileName, line);
        Assert.DoesNotContain(_folder, line);
    }

    [Fact]
    public async Task After_the_file_was_deleted_everything_works_again()
    {
        File.WriteAllText(FilePath, "{ not json");
        Assert.True(Open().IsUnreadable);
        File.Delete(FilePath);

        var store = Open();

        Assert.False(store.IsUnreadable);
        Assert.True(store.Change(Key, data => data with { Title = "Aal" }));
        await store.WhenWrittenAsync();
        Assert.Equal("Aal", Open().Of(Key).Title);
    }

    [Fact]
    public async Task A_failed_write_puts_back_the_stored_title_and_says_so()
    {
        var store = Open();
        store.Change(Key, data => data with { Title = "Aal" });
        await store.WhenWrittenAsync();
        // The file can't be replaced: a folder stands where it would go.
        File.Delete(FilePath);
        Directory.CreateDirectory(FilePath);
        var failures = new List<Exception>();
        var changed = new List<string>();
        store.WriteFailed += (_, e) => failures.Add(e);
        store.Changed += (_, keys) => changed.AddRange(keys);

        store.Change(Key, data => data with { Title = "Zander" });
        Assert.Equal("Zander", store.Of(Key).Title);
        await store.WhenWrittenAsync();

        Assert.Equal("Aal", store.Of(Key).Title);
        Assert.Single(failures);
        Assert.Equal([Key, Key], changed);
    }
}
