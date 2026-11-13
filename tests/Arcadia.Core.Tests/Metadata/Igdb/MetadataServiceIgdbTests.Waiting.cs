using System.Text.Json.Nodes;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>
/// #494 part 2: several IGDB hits with "Selbst auswählen" wait for the player (D6 to D8): kept with the launcher data,
/// never searched by name again while waiting or after a skip, a choice merged without a request, all of it kept in the
/// game's cache file across a restart and a game leaving the library.
/// </summary>
public sealed partial class MetadataServiceIgdbTests
{
    private static readonly string Hit2015 = "\"first_release_date\":1420070400,\"summary\":\"The 2015 one.\",\"cover\":{\"image_id\":\"co2015\"}";
    private static readonly string Hit2021 = "\"first_release_date\":1609459200,\"summary\":\"The 2021 one.\"";

    private static string EpicKey(string id) => Game.KeyOf(Epicish.Id, id);

    private int Searches => _igdb.Snapshot().Count(r => r.Address.Host == "api.igdb.com" && r.Body.StartsWith("search", StringComparison.Ordinal));

    private async Task WaitWithTwoHitsAsync(string title = "K?ln")
    {
        await GrantAccessAsync();
        _igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln", extra: Hit2015), FakeIgdb.Game(2, "Köln", extra: Hit2021));
        Scan(epic: [GameOf(Epicish, "e1", title)]);
        await RunToIdleAsync();
    }

    private async Task RestartAsync()
    {
        _service.Dispose();
        _service = NewService();
        _service.Open();
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();
    }

    [Fact]
    [Trait("TestCase", "T494-16")]
    public async Task Several_hits_wait_with_the_launcher_data_and_neither_a_rescan_a_reload_nor_a_restart_adds_or_searches_them_again()
    {
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        await WaitWithTwoHitsAsync();

        var waiting = Assert.Single(_service.Waiting);
        Assert.Equal(EpicKey("e1"), waiting.Game.Key);
        Assert.Equal("Made-up Epic", waiting.LauncherName);
        Assert.Equal([1L, 2L], waiting.Hits.Select(h => h.Id));
        Assert.Equal([2015, 2021], waiting.Hits.Select(h => h.Year ?? 0));
        Assert.Equal("co2015", waiting.Hits[0].CoverId);
        Assert.Equal("Made-up Epic", _service.Of(EpicKey("e1")).Developers?.Source);
        Assert.Null(_service.Of(EpicKey("e1")).Description);
        Assert.Null(_service.TitleOf(EpicKey("e1")));

        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
        await RestartAsync();

        Assert.Single(_service.Waiting);
        Assert.Equal(1, Searches);
    }

    [Fact]
    [Trait("TestCase", "T494-19")]
    public async Task A_chosen_hit_shows_its_fields_and_title_at_once_without_a_request_and_is_asked_by_its_id_from_then_on()
    {
        await WaitWithTwoHitsAsync();
        var requests = _igdb.ApiRequests;
        var game = Assert.Single(_service.Waiting);

        var changed = await ChangedAfterAsync(() => _service.Choose(game.Game, game.Hits[1]));

        Assert.Contains(EpicKey("e1"), changed);
        Assert.Empty(_service.Waiting);
        Assert.Equal("The 2021 one.", _service.Of(EpicKey("e1")).Description?.Value);
        Assert.Equal("Köln", _service.TitleOf(EpicKey("e1")));
        Assert.Equal(requests, _igdb.ApiRequests);

        _igdb.ServeId(2, FakeIgdb.Game(2, "Köln", extra: Hit2021));
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
        await RestartAsync();
        Assert.Equal(1, Searches);
        Assert.Contains(_igdb.Snapshot(), r => r.Body.Contains("where id = 2;", StringComparison.Ordinal));
        Assert.Equal("Köln", _service.TitleOf(EpicKey("e1")));
    }

    [Fact]
    [Trait("TestCase", "T494-19")]
    public async Task A_skipped_game_keeps_the_launcher_data_and_is_never_searched_by_name_again()
    {
        await WaitWithTwoHitsAsync();
        _service.Skip(Assert.Single(_service.Waiting).Game);
        await _service.WhenIdleAsync();

        Assert.Empty(_service.Waiting);
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
        await RestartAsync();

        Assert.Empty(_service.Waiting);
        Assert.Equal(1, Searches);
        Assert.Null(_service.TitleOf(EpicKey("e1")));
        Assert.Null(_service.Of(EpicKey("e1")).Description);
    }

    [Fact]
    [Trait("TestCase", "T494-17")]
    public async Task Ersten_nehmen_while_games_wait_takes_each_first_hit_at_once_and_counts_the_games_shown()
    {
        await WaitWithTwoHitsAsync();
        _severalHits = SeveralHits.TakeFirst;

        Assert.Equal(1, _service.TakeFirstForAllWaiting());
        await _service.WhenIdleAsync();

        Assert.Empty(_service.Waiting);
        Assert.Equal("The 2015 one.", _service.Of(EpicKey("e1")).Description?.Value);
        Assert.Equal("Köln", _service.TitleOf(EpicKey("e1")));
        Assert.Equal(0, _service.TakeFirstForAllWaiting());
    }

    [Fact]
    [Trait("TestCase", "T494-20")]
    public async Task The_waiting_list_is_kept_across_a_restart_and_a_game_leaving_the_library_and_coming_back()
    {
        await WaitWithTwoHitsAsync();
        Scan(epic: []);
        await RunToIdleAsync();
        Assert.Empty(_service.Waiting);

        await RestartAsync();

        var waiting = Assert.Single(_service.Waiting);
        Assert.Equal([1L, 2L], waiting.Hits.Select(h => h.Id));
        Assert.Equal("The 2015 one.", waiting.Hits[0].Data.Description);
        Assert.Equal(1, Searches);
    }

    [Fact]
    [Trait("TestCase", "T494-20")]
    public async Task A_stored_hit_whose_id_is_no_plain_number_is_ignored_and_a_damaged_file_counts_as_not_yet_searched()
    {
        await WaitWithTwoHitsAsync();
        await _service.WhenIdleAsync();
        _service.Dispose();
        var file = new MetadataCache(Path.Combine(_root, "metadata")).PathOf(EpicKey("e1"));
        var stored = JsonNode.Parse(File.ReadAllText(file))!;
        stored["igdbByName"]!["hits"]![0]!["id"] = "1; drop";
        File.WriteAllText(file, stored.ToJsonString());

        await RestartAsync();
        Assert.Equal([2L], Assert.Single(_service.Waiting).Hits.Select(h => h.Id));
        Assert.Equal(1, Searches);

        _service.Dispose();
        File.WriteAllText(file, "{ not json");
        await RestartAsync();

        Assert.Single(_log, line => line.Contains("beschädigte Cache-Datei", StringComparison.Ordinal));
        Assert.Equal(2, Searches);
        Assert.Single(_service.Waiting);
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetDirectoryName(file)!), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T494-18")]
    public async Task An_own_term_finds_igdbs_main_games_only_and_without_access_sends_nothing()
    {
        Assert.Equal(IgdbOutcome.Rejected, (await _service.SearchByTermAsync("Nordwind Saga")).Outcome);
        Assert.Empty(_igdb.Snapshot());

        await GrantAccessAsync();
        _igdb.ServeName("Nordwind Saga", FakeIgdb.Game(5, "Nordwind Saga"), FakeIgdb.Game(6, "Nordwind Saga – Soundtrack", 1), FakeIgdb.Game(7, "Nordwind Saga Remastered", 9));
        var found = await _service.SearchByTermAsync("Nordwind Saga");

        Assert.Equal(IgdbOutcome.Found, found.Outcome);
        Assert.Equal([5L, 7L], found.Games.Select(g => g.Id));
    }
}
