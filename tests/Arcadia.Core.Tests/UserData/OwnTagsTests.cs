using System.Globalization;
using Arcadia.Core.UserData;

namespace Arcadia.Core.Tests.UserData;

/// <summary>
/// #495: own tags over the file of own data: names (D1), "Neuer Tag" with an existing name (D2), the order (D3), tags
/// without games (D4), absent games (D5), rename and merge (D6, D7), delete, the store's checks and failures
/// (T495-4, T495-5, T495-10, T495-11, T495-16, T495-17, T495-21 to T495-26).
/// </summary>
public sealed class OwnTagsTests : IAsyncDisposable
{
    private const string Steam = "steam:4711";
    private const string Epic = "epic:aal";
    private const string Absent = "steam:9999";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-tags-").FullName;
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

    private OwnTags Open() => new(OpenStore(), German);

    private UserDataStore OpenStore()
    {
        var store = new UserDataStore(_folder) { Log = _log.Add };
        _stores.Add(store);
        store.Open();
        return store;
    }

    private async Task<OwnTags> RestartAsync()
    {
        foreach (var store in _stores)
        {
            await store.WhenWrittenAsync();
        }

        return Open();
    }

    [Theory]
    [InlineData("  Couch-Koop  ", "Couch-Koop")]
    [InlineData("", null)]
    [InlineData("    ", null)]
    [InlineData("Zeile\nzwei", null)]
    [InlineData("1234567890123456789012345678901234567890", "1234567890123456789012345678901234567890")]
    [InlineData("12345678901234567890123456789012345678901", null)]
    public void A_typed_name_is_trimmed_and_kept_only_when_it_fits(string typed, string? kept) =>
        Assert.Equal(kept, OwnTags.NameFrom(typed));

    [Fact]
    public async Task A_new_tag_from_a_game_is_ticked_for_it_and_holds_after_a_restart()
    {
        var tags = Open();

        Assert.Equal("Couch-Koop", tags.Create(" Couch-Koop ", Steam));

        Assert.Equal(["Couch-Koop"], tags.Of(Steam));
        var restarted = await RestartAsync();
        Assert.Equal(["Couch-Koop"], restarted.Of(Steam));
    }

    [Fact]
    public void A_new_tag_with_an_existing_name_in_any_case_makes_no_second_one_and_ticks_the_existing_one()
    {
        var tags = Open();
        tags.Create("Couch-Koop");

        Assert.Equal("Couch-Koop", tags.Create("couch-KOOP", Steam));

        var tag = Assert.Single(tags.All);
        Assert.Equal("Couch-Koop", tag.Name);
        Assert.Equal([Steam], tag.Games);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void An_empty_or_too_long_name_creates_nothing(string typed)
    {
        var tags = Open();

        Assert.Null(tags.Create(typed, Steam));

        Assert.Empty(tags.All);
    }

    [Fact]
    public void Tags_are_listed_alphabetically_ignoring_case_and_umlauts()
    {
        var tags = Open();
        foreach (var name in new[] { "zu zweit", "Äpfel", "Couch-Koop", "abends" })
        {
            tags.Create(name);
        }

        Assert.Equal(["abends", "Äpfel", "Couch-Koop", "zu zweit"], tags.All.Select(tag => tag.Name));
    }

    [Fact]
    public void A_tag_is_global_across_launchers_and_a_tag_without_games_stays()
    {
        var tags = Open();
        tags.Create("Couch-Koop", Steam);
        tags.Create("Couch-Koop", Epic);

        Assert.Equal([Epic, Steam], tags.Find("couch-koop")!.Games.Order());

        Assert.True(tags.Set("Couch-Koop", Steam, has: false));
        Assert.True(tags.Set("Couch-Koop", Epic, has: false));
        Assert.Empty(Assert.Single(tags.All).Games);
    }

    [Fact]
    public async Task Delete_removes_the_tag_from_every_game_absent_ones_included()
    {
        var tags = Open();
        tags.Create("Couch-Koop", Steam);
        tags.Create("Couch-Koop", Absent);
        tags.Create("Zu zweit", Absent);

        Assert.True(tags.Delete("couch-koop"));

        Assert.Empty(tags.Of(Steam));
        Assert.Equal(["Zu zweit"], tags.Of(Absent));
        Assert.Equal(["Zu zweit"], (await RestartAsync()).Of(Absent));
    }

    [Fact]
    public void Rename_reaches_absent_games_and_only_a_change_of_case_needs_no_question()
    {
        var tags = Open();
        tags.Create("Couch-Koop", Absent);

        Assert.Equal(TagRename.Renamed, tags.Rename("Couch-Koop", " Sofa "));
        Assert.Equal(["Sofa"], tags.Of(Absent));

        Assert.Equal(TagRename.Renamed, tags.Rename("Sofa", "SOFA"));
        Assert.Equal(["SOFA"], tags.Of(Absent));
    }

    [Theory]
    [InlineData("Couch-Koop")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void Renaming_to_the_same_an_empty_or_a_too_long_name_changes_nothing(string typed)
    {
        var tags = Open();
        tags.Create("Couch-Koop", Steam);

        Assert.Equal(TagRename.Unchanged, tags.Rename("Couch-Koop", typed));

        Assert.Equal(["Couch-Koop"], tags.Of(Steam));
    }

    [Fact]
    public void Renaming_to_another_tags_name_asks_and_merging_keeps_the_existing_name_and_every_game_once()
    {
        var tags = Open();
        tags.Create("Zu Zweit", Steam);
        tags.Create("Zu Zweit", Epic);
        tags.Create("Couch-Koop", Steam);
        tags.Create("Couch-Koop", Absent);

        Assert.Equal(TagRename.NeedsMerge, tags.Rename("Couch-Koop", "zu zweit"));
        Assert.Equal(2, tags.All.Count);

        Assert.True(tags.Merge("Couch-Koop", "zu zweit"));

        var tag = Assert.Single(tags.All);
        Assert.Equal("Zu Zweit", tag.Name);
        Assert.Equal([Epic, Steam, Absent], tag.Games.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_game_leaving_the_library_keeps_its_tags_with_their_new_names()
    {
        var tags = Open();
        tags.Create("Couch-Koop", Absent);
        tags.Create("Weg", Absent);
        tags.Rename("Couch-Koop", "Sofa");
        tags.Delete("Weg");

        Assert.Equal(["Sofa"], (await RestartAsync()).Of(Absent));
    }

    [Theory]
    [InlineData("Größe ß äöü")]
    [InlineData("🎮 Koop")]
    [InlineData("協力プレイ")]
    [InlineData("\"Zitat\" 'x'")]
    [InlineData("a/b\\c ..")]
    [InlineData("100 % <b>fett</b>")]
    public async Task A_name_with_any_characters_is_stored_and_read_back_unchanged(string name)
    {
        var tags = Open();
        tags.Create(name, Steam);

        Assert.Equal([name], (await RestartAsync()).Of(Steam));
    }

    [Fact]
    public async Task Values_from_the_file_are_checked_without_dropping_the_rest()
    {
        File.WriteAllText(FilePath, """
            { "version": 1, "games": { "steam:1": { "title": "Aal" } }, "tags": [
              { "name": "  Couch-Koop ", "games": ["steam:1", "", null] },
              { "name": "couch-koop", "games": ["epic:2", "steam:1"] },
              { "name": "12345678901234567890123456789012345678901", "games": ["steam:1"] },
              { "name": "Zeile\nzwei", "games": ["steam:1"] },
              { "name": "", "games": [] },
              null,
              { "name": "Leer" },
              { "name": "Unbekannt", "games": ["plugin:nie-gesehen"] }
            ] }
            """);

        var tags = Open();

        Assert.Equal(["Couch-Koop", "Leer", "Unbekannt"], tags.All.Select(tag => tag.Name));
        Assert.Equal(["epic:2", "steam:1"], tags.Find("Couch-Koop")!.Games.Order(StringComparer.Ordinal));
        Assert.Equal(["plugin:nie-gesehen"], tags.Find("Unbekannt")!.Games);
        Assert.Empty(_log);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_file_of_own_titles_without_tags_is_read_and_keeps_its_titles()
    {
        File.WriteAllText(FilePath, """{ "version": 1, "games": { "steam:1": { "title": "Aal" } } }""");
        var store = OpenStore();
        var tags = new OwnTags(store, German);

        tags.Create("Couch-Koop", "steam:1");
        await store.WhenWrittenAsync();

        var again = OpenStore();
        Assert.Equal("Aal", again.Of("steam:1").Title);
        Assert.Equal(["Couch-Koop"], new OwnTags(again, German).Of("steam:1"));
    }

    [Fact]
    public void No_file_is_no_tags_and_no_message()
    {
        var tags = Open();

        Assert.True(tags.CanChange);
        Assert.Empty(tags.All);
        Assert.Empty(_log);
        Assert.False(File.Exists(FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"version\": 1, \"games\": {}, \"tags\": 5 }")]
    [InlineData("{ \"version\": 1, \"games\": {}, \"tags\": [ { \"name\": 5 } ] }")]
    public async Task An_unreadable_file_shows_no_tags_changes_nothing_and_stays_as_it_is(string content)
    {
        File.WriteAllText(FilePath, content);

        var tags = Open();

        Assert.False(tags.CanChange);
        Assert.Empty(tags.All);
        Assert.Null(tags.Create("Couch-Koop", Steam));
        Assert.False(tags.Delete("Couch-Koop"));
        Assert.Equal(TagRename.Unchanged, tags.Rename("Couch-Koop", "Sofa"));
        await RestartAsync();
        Assert.Equal(content, File.ReadAllText(FilePath));
        Assert.True(File.Exists(FilePath + UserDataStore.CopySuffix));
    }

    [Fact]
    public async Task A_failed_write_puts_back_the_stored_tags_and_says_so()
    {
        var store = OpenStore();
        var tags = new OwnTags(store, German);
        tags.Create("Couch-Koop", Steam);
        await store.WhenWrittenAsync();
        // The file can't be replaced: a folder stands where it would go.
        File.Delete(FilePath);
        Directory.CreateDirectory(FilePath);
        var failures = new List<Exception>();
        var changes = 0;
        store.WriteFailed += (_, e) => failures.Add(e);
        tags.Changed += (_, _) => changes++;

        tags.Delete("Couch-Koop");
        await store.WhenWrittenAsync();

        Assert.Equal(["Couch-Koop"], tags.Of(Steam));
        Assert.Single(failures);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task The_file_holds_no_half_written_tags_after_a_write_broke_off()
    {
        var store = OpenStore();
        new OwnTags(store, German).Create("Couch-Koop", Steam);
        await store.WhenWrittenAsync();
        var leftover = FilePath + ".1a2b3c4d" + Library.CacheFiles.TemporarySuffix;
        File.WriteAllText(leftover, "{ \"version\": 1, \"tags\": [ { \"na");

        var again = Open();

        Assert.Equal(["Couch-Koop"], again.Of(Steam));
        Assert.False(File.Exists(leftover));
    }

    [Fact]
    public async Task Log_lines_never_name_a_tag()
    {
        File.WriteAllText(FilePath, "{ \"version\": 1, \"games\": {}, \"tags\": [ { \"name\": \"Geheim\", \"games\": 5 } ] }");

        Open();
        await RestartAsync();

        Assert.DoesNotContain(_log, line => line.Contains("Geheim", StringComparison.Ordinal));
    }
}
