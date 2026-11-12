using System.Text;
using Arcadia.Core.Artwork.SteamGridDb;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#491: how a name is searched, which hits count as matching, and how the matches are kept.</summary>
public sealed class SteamGridDbNameTests : IDisposable
{
    private readonly FakeSteamGridDb _server = new();
    private readonly SteamGridDbClient _client;
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-name-matches-").FullName;
    private readonly List<string> _log = [];

    public SteamGridDbNameTests() => _client = new SteamGridDbClient(new HttpClient(_server), new FakeTimeProvider());

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SteamGridDbHit Hit(int id, string name, int? year = null) => new(id, name, year);

    [Fact]
    [Trait("TestCase", "T491-5")]
    public void One_hit_of_the_same_name_is_taken_without_a_question()
    {
        var result = SteamGridDbNames.Decide("Celeste", [Hit(1, "Celeste Classic"), Hit(2, "CELESTE™")]);

        Assert.Equal(new NameSearchResult.One(Hit(2, "CELESTE™")), result);
    }

    [Fact]
    [Trait("TestCase", "T491-5")]
    public void Two_hits_of_the_same_name_are_several_hits_with_all_of_them_offered()
    {
        var hits = new[] { Hit(1, "Celeste", 2018), Hit(2, "Celeste", 2016), Hit(3, "Celeste Classic") };

        var several = Assert.IsType<NameSearchResult.Several>(SteamGridDbNames.Decide("Celeste", hits));

        Assert.Equal([1, 2, 3], several.Hits.Select(h => h.Id));
    }

    [Fact]
    [Trait("TestCase", "T491-5")]
    public void None_equal_but_several_similar_are_several_hits_and_one_result_of_any_name_is_taken()
    {
        Assert.IsType<NameSearchResult.Several>(SteamGridDbNames.Decide("Echo", [Hit(1, "Echo Drift"), Hit(2, "Echoes")]));
        Assert.Equal(new NameSearchResult.One(Hit(1, "Echo Drift")), SteamGridDbNames.Decide("Echo", [Hit(1, "Echo Drift")]));
        Assert.IsType<NameSearchResult.None>(SteamGridDbNames.Decide("Echo", []));
    }

    [Theory]
    [Trait("TestCase", "T491-5")]
    [InlineData("Pale Wilds II", "pale wilds ii")]
    [InlineData("Pale-Wilds: II!", "pale wilds ii")]
    [InlineData("  STAR®  Quest™ ©", "star quest")]
    [InlineData("Ölfeld", "ölfeld")]
    public void Case_punctuation_and_signs_are_ignored_when_names_are_compared(string name, string comparable) =>
        Assert.Equal(comparable, SteamGridDbNames.Comparable(SteamGridDbNames.SearchTermOf(name)));

    [Theory]
    [Trait("TestCase", "T491-6")]
    [InlineData("Star Quest™", "Star Quest")]
    [InlineData("  Golden®   Crown ©  ", "Golden Crown")]
    [InlineData("™®©", "")]
    public void The_name_sent_is_the_title_without_signs_trimmed(string title, string term) =>
        Assert.Equal(term, SteamGridDbNames.SearchTermOf(title));

    [Theory]
    [Trait("TestCase", "T491-6")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?x=1")]
    [InlineData("hash#tag")]
    [InlineData("100%")]
    [InlineData("A & B")]
    [InlineData("../../games/id/1")]
    [InlineData("\"quoted\"")]
    [InlineData("Größenwahn")]
    [InlineData("ゲーム")]
    [InlineData("Game 🎮")]
    public async Task A_name_is_sent_as_one_encoded_path_segment_to_the_api_host_only(string name)
    {
        await _client.SearchAsync(FakeSteamGridDb.Key, name, default);

        var request = Assert.Single(_server.Requests);
        var uri = request.RequestUri!;
        Assert.Equal(("https", FakeSteamGridDb.Host, string.Empty), (uri.Scheme, uri.Host, uri.Query));
        Assert.StartsWith("/api/v2/search/autocomplete/", uri.AbsolutePath, StringComparison.Ordinal);
        var segment = uri.AbsolutePath["/api/v2/search/autocomplete/".Length..];
        Assert.DoesNotContain('/', segment);
        Assert.Equal(name, Uri.UnescapeDataString(segment));
        Assert.Equal(("Bearer", FakeSteamGridDb.Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        Assert.DoesNotContain(FakeSteamGridDb.Key, uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("TestCase", "T491-6")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(" ... ")]
    public async Task An_empty_or_too_long_name_or_one_of_dots_only_sends_nothing(string name)
    {
        Assert.Equal(SteamGridDbOutcome.NotFound, (await _client.SearchAsync(FakeSteamGridDb.Key, name, default)).Outcome);
        Assert.Equal(SteamGridDbOutcome.NotFound, (await _client.SearchAsync(FakeSteamGridDb.Key, new string('x', 201), default)).Outcome);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    [Trait("TestCase", "T491-5")]
    public async Task At_most_eight_hits_are_kept_in_steamgriddbs_order_with_their_year()
    {
        _server.Finds("Echo", [.. Enumerable.Range(1, 10).Select(i => (i, "Echo " + i, (int?)(i == 1 ? null : 2000 + i)))]);

        var search = await _client.SearchAsync(FakeSteamGridDb.Key, "Echo", default);

        Assert.Equal(SteamGridDbOutcome.Found, search.Outcome);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], search.Hits.Select(h => h.Id));
        Assert.Equal((null, 2002), (search.Hits[0].Year, search.Hits[1].Year));
    }

    [Theory]
    [Trait("TestCase", "T491-8")]
    [InlineData("{ not json")]
    [InlineData("""{"success":true,"data":{}}""")]
    public async Task An_answer_that_isnt_a_list_of_games_counts_as_no_connection(string body)
    {
        _server.Body = body;

        Assert.Equal(SteamGridDbOutcome.NoConnection, (await _client.SearchAsync(FakeSteamGridDb.Key, "Echo", default)).Outcome);
    }

    [Fact]
    [Trait("TestCase", "T491-5")]
    public async Task A_hit_without_an_id_or_a_name_is_left_out()
    {
        _server.Body = """{"success":true,"data":[{"id":0,"name":"Zero"},{"id":"7","name":"Text id"},{"id":8},{"id":9,"name":" "},{"id":10,"name":"Good","release_date":0}]}""";

        var search = await _client.SearchAsync(FakeSteamGridDb.Key, "Echo", default);

        Assert.Equal([new SteamGridDbHit(10, "Good", null)], search.Hits);
    }

    private NameMatchStore Store() => new(Path.Combine(_root, NameMatchStore.FileName)) { Log = _log.Add };

    [Fact]
    [Trait("TestCase", "T491-22")]
    public void Choices_skips_and_waiting_games_are_read_back_as_written()
    {
        var store = Store();
        store.Set("other:a", NameMatch.Chosen(501));
        store.Set("other:b", NameMatch.Skipped);
        store.Set("other:c", NameMatch.Waiting([Hit(1, "C", 2001), Hit(2, "C")], 3));

        var read = Store();

        Assert.Equal(NameMatch.Chosen(501), read.Of("other:a"));
        Assert.Equal(NameMatchState.Skipped, read.Of("other:b")!.State);
        Assert.Equal((NameMatchState.Waiting, 3), (read.Of("other:c")!.State, read.Of("other:c")!.Position));
        Assert.Equal([Hit(1, "C", 2001), Hit(2, "C")], read.Of("other:c")!.Hits);
        Assert.Equal(4, read.NextPosition);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    [Theory]
    [Trait("TestCase", "T491-24")]
    [InlineData("{ broken")]
    [InlineData("[]")]
    [InlineData("")]
    public void An_unreadable_file_counts_as_no_matches_with_one_log_line(string content)
    {
        File.WriteAllText(Path.Combine(_root, NameMatchStore.FileName), content);

        Assert.Empty(Store().All);
        Assert.Single(_log);
    }

    [Fact]
    [Trait("TestCase", "T491-24")]
    public void Each_stored_value_is_checked_on_its_own()
    {
        File.WriteAllText(Path.Combine(_root, NameMatchStore.FileName), """
            {
              "other:text-id": { "state": "Chosen", "gameId": "../1" },
              "other:negative": { "state": "Chosen", "gameId": -4 },
              "other:fraction": { "state": "Chosen", "gameId": 1.5 },
              "other:unknown": { "state": "Deleted" },
              "other:one-hit": { "state": "Waiting", "hits": [ { "id": 1, "name": "A" } ] },
              "other:good": { "state": "Chosen", "gameId": 77 },
              "other:waiting": { "state": "Waiting", "position": 2, "hits": [ { "id": 1, "name": "A" }, { "id": "x", "name": "B" }, { "id": 3, "name": "C", "year": 99999 } ] }
            }
            """);

        var store = Store();

        // One hit is enough to wait since #506: the others may have been dropped for having no image.
        Assert.Equal(["other:good", "other:one-hit", "other:waiting"], store.All.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([Hit(1, "A"), Hit(3, "C")], store.Of("other:waiting")!.Hits);
    }

    [Fact]
    [Trait("TestCase", "T491-24")]
    public void A_temporary_file_left_by_a_crash_is_removed_and_the_last_complete_file_counts()
    {
        Store().Set("other:a", NameMatch.Chosen(5));
        var leftover = Path.Combine(_root, NameMatchStore.FileName + ".1a2b3c4d.tmp");
        File.WriteAllBytes(leftover, Encoding.UTF8.GetBytes("{ half"));

        var read = Store();

        Assert.Equal(NameMatch.Chosen(5), read.Of("other:a"));
        Assert.False(File.Exists(leftover));
    }
}
