using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #506 C: before a hit is offered or taken SteamGridDB is asked whether it has an image of the kind sought; a hit
/// without one is dropped. The waiting list checks old or failed hits when shown, and searches by the player's own term.
/// </summary>
public sealed partial class ArtworkServiceByNameTests
{
    private const string Hero503Only = "https://cdn.example/hero/503-only.png";

    /// <summary>Five namesakes; 501 to 503 have a cover at SteamGridDB, 504 and 505 none (it doesn't know them by any image).</summary>
    private void FiveEchoes() =>
        _sgdb.Finds(Title, (501, "Echo Drift", 2019), (502, "Echo Drift", 2023), (503, "Echo Drift", 2015), (504, "Echo Drift", 2010), (505, "Echo Drift", null));

    private NameMatch Match(string key = "other:echo") => Store().Of(key)!;

    private NameMatchStore Store() => new(Path.Combine(_root, "artwork", NameMatchStore.FileName));

    [Fact]
    [Trait("TestCase", "T506-92")]
    public async Task Hits_without_a_cover_are_dropped_before_the_game_waits_in_steamgriddbs_order()
    {
        await WithKeyAsync();
        FiveEchoes();

        await ScanAsync();

        var waiting = Assert.Single(_service.Waiting);
        Assert.Equal([501, 502, 503], waiting.Hits.Select(h => h.Id));
        Assert.Equal(NameMatch.BothKinds, waiting.Match.Sought);
        Assert.Equal([501, 502, 503], waiting.Match.Checked.Order());
        Assert.Equal(["grids/game/501", "grids/game/502", "grids/game/503", "grids/game/504", "grids/game/505"], _sgdb.Asked().Where(a => a.StartsWith("grids/game/", StringComparison.Ordinal)).Take(5));

        // Kept across a restart: the same hits, checked, no new request for them.
        await RestartAsync();
        await ScanAsync();
        Assert.Equal([501, 502, 503], Assert.Single(_service.Waiting).Match.Checked.Order());
        Assert.DoesNotContain(_sgdb.Asked(), a => a.StartsWith("search/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T506-92")]
    [Trait("TestCase", "T506-89")]
    public async Task With_a_fitting_launcher_cover_only_the_background_is_sought_and_hits_need_a_background()
    {
        await WithKeyAsync();
        FiveEchoes();
        _sgdb.Hero("game", "503", Hero503Only, 1920, 620);
        _source.Cover = [new("other-art", Download: new Uri("https://images.example/echo.png"))];
        _images.Serve("https://images.example/echo.png", TestImages.Png(600, 900));

        await ScanAsync();

        var waiting = Assert.Single(_service.Waiting);
        Assert.Equal([ArtworkKind.Hero], waiting.Match.Sought);
        Assert.Equal(ArtworkKind.Hero, waiting.Match.TileKind);
        Assert.Equal([501, 502, 503], waiting.Hits.Select(h => h.Id));
        Assert.Contains("heroes/game/504", _sgdb.Asked());
        Assert.DoesNotContain("grids/game/504", _sgdb.Asked());
        Assert.Equal([ArtworkKind.Hero], _service.SoughtOf(Echo));
    }

    [Fact]
    [Trait("TestCase", "T506-90")]
    public async Task Taking_a_hit_when_only_the_background_is_sought_keeps_the_launcher_cover()
    {
        await WithKeyAsync();
        FiveEchoes();
        _source.Cover = [new("other-art", Download: new Uri("https://images.example/echo.png"))];
        _images.Serve("https://images.example/echo.png", TestImages.Png(600, 900));
        await ScanAsync();
        Assert.Equal([ArtworkKind.Hero], Assert.Single(_service.Waiting).Match.Sought);

        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[1]);

        Assert.Equal(ArtworkSource.Launcher, Cached()!.Cover!.Source);
        Assert.Equal((Hero502, ArtworkSource.SteamGridDb), (Cached()!.Hero!.Stamp, Cached()!.Hero!.Source));
        Assert.DoesNotContain(_images.Requests, r => r.RequestUri!.AbsoluteUri == Cover502);
    }

    [Fact]
    [Trait("TestCase", "T506-93")]
    public async Task One_hit_left_is_taken_and_none_left_is_no_result()
    {
        await WithKeyAsync();
        _sgdb.Finds(Title, (501, "Echo Drift", 2019), (504, "Echo Drift", 2010));
        _sgdb.Finds("Lonely Game", (504, "Lonely Game", null), (505, "Lonely Game", null));
        var lonely = GameOf("lonely", "Lonely Game");

        await ScanAsync(Echo, lonely);

        Assert.Empty(_service.Waiting);
        Assert.Equal(Cover501, Cached()!.Cover!.Stamp);
        Assert.Equal(NameMatchState.Chosen, Match().State);
        Assert.Null(Store().Of(lonely.Key));
        Assert.Null(Cached(lonely.Key)?.Cover);
    }

    [Fact]
    [Trait("TestCase", "T506-94")]
    public async Task Ersten_nehmen_takes_the_first_hit_with_an_image()
    {
        _settings = _settings with { SeveralHits = SeveralHits.TakeFirst };
        await WithKeyAsync();
        _sgdb.Finds(Title, (504, "Echo Drift", 2010), (502, "Echo Drift", 2023), (501, "Echo Drift", 2019));

        await ScanAsync();

        Assert.Equal((NameMatchState.Chosen, 502), (Match().State, Match().GameId));
        Assert.Equal(Cover502, Cached()!.Cover!.Stamp);
    }

    [Fact]
    [Trait("TestCase", "T506-97")]
    public async Task A_failing_check_in_a_run_stops_steamgriddb_after_one_request_and_the_game_is_searched_again_later()
    {
        await WithKeyAsync();
        FiveEchoes();
        _sgdb.FailingPaths.Add("grids/game/501");

        await ScanAsync();

        Assert.Empty(_service.Waiting);
        Assert.Null(Store().Of(Echo.Key));
        Assert.Equal(["search/autocomplete/Echo Drift", "grids/game/501"], _sgdb.Asked());

        _sgdb.FailingPaths.Clear();
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        Assert.Equal([501, 502, 503], Assert.Single(_service.Waiting).Hits.Select(h => h.Id));
    }

    /// <summary>A waiting list as before #506: hits without "checked", no "sought".</summary>
    private async Task OldWaitingListAsync(params int[] hits)
    {
        var list = string.Join(", ", hits.Select(id => $$"""{ "id": {{id}}, "name": "Echo Drift" }"""));
        Directory.CreateDirectory(Path.Combine(_root, "artwork"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "artwork", NameMatchStore.FileName),
            $$"""{ "other:echo": { "state": "Waiting", "position": 1, "hits": [ {{list}} ] } }""");
        await RestartAsync();
        await ScanAsync();
    }

    [Fact]
    [Trait("TestCase", "T506-96")]
    public async Task Checks_finishing_at_the_same_time_keep_every_result()
    {
        int[] withCover = [501, 502, 503, 601, 602];
        await OldWaitingListAsync([.. withCover, .. Enumerable.Range(900, 55)]);
        var hits = Assert.Single(_service.Waiting).Hits;

        await Task.WhenAll(hits.Select(hit => Task.Run(() => _service.CheckWaitingHitAsync(Echo, hit))));

        Assert.Equal(withCover, Match().Hits.Select(h => h.Id));
        Assert.Equal(withCover, Match().Checked.Order());
    }

    [Fact]
    [Trait("TestCase", "T506-96")]
    public async Task An_old_waiting_game_is_checked_only_when_shown_and_kept_checked()
    {
        await OldWaitingListAsync(501, 504, 502);
        var waiting = Assert.Single(_service.Waiting);
        Assert.Empty(waiting.Match.Checked);
        Assert.Equal(NameMatch.BothKinds, waiting.Match.Sought);
        Assert.Empty(_sgdb.Asked());

        Assert.Equal(HitCheck.HasImage, await _service.CheckWaitingHitAsync(Echo, waiting.Hits[0]));
        Assert.Equal(HitCheck.NoImage, await _service.CheckWaitingHitAsync(Echo, waiting.Hits[1]));
        Assert.Equal(HitCheck.HasImage, await _service.CheckWaitingHitAsync(Echo, waiting.Hits[2]));

        Assert.Equal([501, 502], Match().Hits.Select(h => h.Id));
        Assert.Equal([501, 502], Match().Checked.Order());
        var asked = _sgdb.Count;
        Assert.Equal(HitCheck.HasImage, await _service.CheckWaitingHitAsync(Echo, Match().Hits[0]));
        Assert.Equal(asked, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T506-96")]
    [Trait("TestCase", "T506-95")]
    public async Task One_or_no_hit_left_after_the_check_keeps_the_game_waiting_while_the_player_looks()
    {
        await OldWaitingListAsync(504, 505);
        var hits = _service.Waiting[0].Hits;

        await _service.CheckWaitingHitAsync(Echo, hits[0]);
        Assert.Equal([505], Assert.Single(_service.Waiting).Hits.Select(h => h.Id));
        await _service.CheckWaitingHitAsync(Echo, hits[1]);

        var waiting = Assert.Single(_service.Waiting);
        Assert.Empty(waiting.Hits);
        Assert.Equal(NameMatchState.Waiting, Match().State);

        // After a restart too, until it is skipped.
        await RestartAsync();
        await ScanAsync();
        Assert.Empty(Assert.Single(_service.Waiting).Hits);
    }

    [Fact]
    [Trait("TestCase", "T506-97")]
    public async Task A_check_that_fails_on_the_waiting_list_leaves_the_hit_unchecked_for_next_time()
    {
        await OldWaitingListAsync(501, 502);
        _sgdb.Offline = true;

        Assert.Equal(HitCheck.NoConnection, await _service.CheckWaitingHitAsync(Echo, _service.Waiting[0].Hits[0]));

        Assert.Equal([501, 502], Match().Hits.Select(h => h.Id));
        Assert.Empty(Match().Checked);
        _sgdb.Offline = false;
        Assert.Equal(HitCheck.HasImage, await _service.CheckWaitingHitAsync(Echo, _service.Waiting[0].Hits[0]));
    }

    [Fact]
    [Trait("TestCase", "T506-91")]
    public async Task What_is_sought_is_decided_again_when_shown_and_a_new_tile_kind_checks_again()
    {
        await WithKeyAsync();
        FiveEchoes();
        await ScanAsync();
        Assert.Equal(NameMatch.BothKinds, _service.Waiting[0].Match.Sought);

        // The launcher has a fitting cover meanwhile.
        _source.Cover = [new("other-art", Download: new Uri("https://images.example/echo.png"))];
        _images.Serve("https://images.example/echo.png", TestImages.Png(600, 900));
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();

        var now = _service.WaitingMatchNow(Echo)!;
        Assert.Equal([ArtworkKind.Hero], now.Sought);
        Assert.Empty(now.Checked);
        Assert.Equal(HitCheck.HasImage, await _service.CheckWaitingHitAsync(Echo, now.Hits[0]));
        Assert.Contains("heroes/game/501", _sgdb.Asked());
        Assert.Equal([ArtworkKind.Hero], Store().Of(Echo.Key)!.Sought);
    }

    [Fact]
    [Trait("TestCase", "T506-89")]
    public async Task A_hits_background_tile_is_its_best_background()
    {
        await WithKeyAsync();
        FiveEchoes();
        await ScanAsync();

        var tile = await _service.HitImageAsync(Echo, _service.Waiting[0].Hits[1], ArtworkKind.Hero);

        Assert.Equal(ArtworkKind.Hero, tile!.Kind);
        Assert.Contains(_images.Requests, r => r.RequestUri!.AbsoluteUri == Hero502);
    }

    [Fact]
    [Trait("TestCase", "T506-100")]
    public async Task An_own_term_finds_up_to_fifty_results_and_keeps_nothing()
    {
        await WithKeyAsync();
        _sgdb.Finds("Echo Saga", [.. Enumerable.Range(1, 60).Select(i => (1000 + i, $"Echo Saga {i}", (int?)null))]);

        var search = await _service.SearchByTermAsync("  Echo Saga™ ");

        Assert.Equal(SteamGridDbOutcome.Found, search.Outcome);
        Assert.Equal(SteamGridDbClient.MaxOwnTermResults, search.Hits.Count);
        Assert.Equal(["search/autocomplete/Echo Saga"], _sgdb.Asked());
        Assert.Empty(Store().All);
    }

    [Theory]
    [Trait("TestCase", "T506-103")]
    [InlineData("a/b\\c?d#e%f&g")]
    [InlineData("\"quoted\" Ärger 🎮")]
    public async Task An_own_term_stays_one_path_segment_of_the_api(string term)
    {
        await WithKeyAsync();

        await _service.SearchByTermAsync(term);

        var request = Assert.Single(_sgdb.Requests);
        Assert.Equal(FakeSteamGridDb.Host, request.RequestUri!.Host);
        Assert.Empty(request.RequestUri.Query);
        Assert.StartsWith("/api/v2/search/autocomplete/", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain('/', request.RequestUri.AbsolutePath["/api/v2/search/autocomplete/".Length..]);
    }

    [Theory]
    [Trait("TestCase", "T506-103")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("™®")]
    [InlineData("..")]
    public async Task An_empty_own_term_sends_nothing(string term)
    {
        await WithKeyAsync();

        var search = await _service.SearchByTermAsync(term);

        Assert.Equal((SteamGridDbOutcome.NotFound, 0), (search.Outcome, search.Hits.Count));
        Assert.Empty(_sgdb.Requests);
    }

    [Fact]
    [Trait("TestCase", "T506-104")]
    public async Task An_own_term_without_a_usable_key_sends_nothing()
    {
        _key.Clear();

        var search = await _service.SearchByTermAsync("Echo Drift");

        Assert.Equal(SteamGridDbOutcome.KeyRejected, search.Outcome);
        Assert.Empty(_sgdb.Requests);
    }
}
