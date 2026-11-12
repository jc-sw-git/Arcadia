using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #491: games SteamGridDB doesn't know by a launcher's id are found by their name; one matching game is taken, several
/// wait for the player or the first is taken; the choice, a skip and the waiting list last across restarts.
/// </summary>
public sealed class ArtworkServiceByNameTests : IDisposable
{
    private const string Title = "Echo Drift";
    private const string Cover501 = "https://cdn.example/grid/501.png";
    private const string Hero501 = "https://cdn.example/hero/501.png";
    private const string Cover502 = "https://cdn.example/grid/502.png";
    private const string Hero502 = "https://cdn.example/hero/502.png";

    private static readonly LauncherInfo Other = new("other", "Anderer Launcher", "other", null);
    private static readonly LauncherInfo Epic = new("epic", "Epic Games", "epic", null);

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-name-").FullName;
    private readonly FakeImageServer _images = new();
    private readonly FakeSteamGridDb _sgdb = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly Source _source = new();
    private ArtworkSettings _settings = new();
    private SteamGridDbKey _key;
    private ArtworkService _service;

    public ArtworkServiceByNameTests()
    {
        _key = NewKey();
        _service = NewService();
        _sgdb.Cover("game", "501", Cover501, 600, 900).Hero("game", "501", Hero501, 1920, 620)
            .Cover("game", "502", Cover502, 600, 900).Hero("game", "502", Hero502, 1920, 620);
        foreach (var url in new[] { Cover501, Cover502 })
        {
            _images.Serve(url, TestImages.Png(600, 900));
        }

        foreach (var url in new[] { Hero501, Hero502 })
        {
            _images.Serve(url, TestImages.Png(1920, 620));
        }
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private SteamGridDbKey NewKey() =>
        new(new SteamGridDbKeyStore(Path.Combine(_root, "data"), data => data, data => data), new SteamGridDbClient(new HttpClient(_sgdb), _time) { Log = Log }, _time);

    private ArtworkService NewService() => new(new ArtworkCache(Path.Combine(_root, "artwork")), new HttpClient(_images), _time)
    {
        Log = Log,
        Settings = () => _settings,
        SteamGridDb = _key,
    };

    private void Log(string line)
    {
        lock (_log)
        {
            _log.Add(line);
        }
    }

    private async Task RestartAsync()
    {
        _service.Dispose();
        _key = NewKey();
        _service = NewService();
        await WithKeyAsync();
    }

    private async Task WithKeyAsync()
    {
        Assert.Equal(SteamGridDbOutcome.Found, await _key.SetAsync(FakeSteamGridDb.Key));
        _sgdb.Requests.Clear();
    }

    private static Game GameOf(string id, string title = Title, string launcher = "other") => new()
    {
        Id = id,
        LauncherId = launcher,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri($"{launcher}://launch/{id}"),
    };

    private static readonly Game Echo = GameOf("echo");

    private async Task ScanAsync(params Game[] games)
    {
        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, games.Length == 0 ? [Echo] : games), _source)]);
        await _service.WhenIdleAsync();
    }

    private CachedArtwork? Cached(string key = "other:echo") => new ArtworkCache(Path.Combine(_root, "artwork")).Read(key);

    private IReadOnlyList<string> Searches() => [.. _sgdb.Asked().Where(a => a.StartsWith("search/", StringComparison.Ordinal))];

    private void ThreeEchoes() =>
        _sgdb.Finds(Title, (501, "Echo Drift", 2019), (502, "Echo Drift", 2023), (503, "Echo Drifter", 2015));

    [Fact]
    [Trait("TestCase", "T491-1")]
    public async Task An_epic_game_is_asked_by_its_app_name_on_egs_not_by_name()
    {
        await WithKeyAsync();
        _source.StoreIds["epic:Sugar"] = new ArtworkStoreId(ArtworkStoreId.Epic, "Sugar");
        _sgdb.Cover("egs", "Sugar", Cover501, 600, 900).Hero("egs", "Sugar", Hero501, 1920, 620);
        var game = GameOf("Sugar", launcher: "epic");

        _service.Update([new LibraryArtwork(new LauncherLibrary(Epic, [game]), _source)]);
        await _service.WhenIdleAsync();

        Assert.Equal(["grids/egs/Sugar", "heroes/egs/Sugar"], _sgdb.Asked());
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached("epic:Sugar")!.Hero!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T491-3")]
    public async Task A_game_without_an_id_and_one_matching_game_takes_its_images_without_a_question()
    {
        await WithKeyAsync();
        _sgdb.Finds(Title, (501, "Echo Drift", 2019), (503, "Echo Drifter", 2015));

        await ScanAsync();

        Assert.Equal((ArtworkService.SteamGridDbOrigin, Cover501), (Cached()!.Cover!.Origin, Cached()!.Cover!.Stamp));
        Assert.Equal(Hero501, Cached()!.Hero!.Stamp);
        Assert.Empty(_service.Waiting);
        Assert.Single(Searches());
    }

    [Fact]
    [Trait("TestCase", "T491-3")]
    public async Task A_game_steamgriddb_doesnt_know_by_its_id_is_searched_by_name()
    {
        await WithKeyAsync();
        _source.StoreIds["other:echo"] = new ArtworkStoreId(ArtworkStoreId.Epic, "Unknown1");
        _sgdb.Finds(Title, (502, "Echo Drift", 2023));

        await ScanAsync();

        Assert.Equal(Cover502, Cached()!.Cover!.Stamp);
        Assert.Contains("grids/egs/Unknown1", _sgdb.Asked());
        Assert.Single(Searches());
    }

    [Fact]
    [Trait("TestCase", "T491-3")]
    public async Task A_launcher_without_images_of_its_own_gets_steamgriddbs_by_name_only_with_a_key()
    {
        ThreeEchoes();
        _sgdb.Finds("Solo", (501, "Solo", 2010));
        var solo = GameOf("solo", "Solo");

        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, [solo]), Source: null)]);
        await _service.WhenIdleAsync();
        Assert.Null(Cached("other:solo"));

        await WithKeyAsync();
        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, [solo]), Source: null)]);
        await _service.WhenIdleAsync();

        Assert.Equal(Cover501, Cached("other:solo")!.Cover!.Stamp);
        Assert.Equal(1, _service.GameCount);
    }

    [Fact]
    [Trait("TestCase", "T491-4")]
    public async Task A_game_known_by_its_id_is_never_searched_by_name()
    {
        await WithKeyAsync();
        _source.StoreIds["other:echo"] = new ArtworkStoreId(ArtworkStoreId.Steam, "4000001");
        _sgdb.Knows("grids", "steam", "4000001");
        ThreeEchoes();

        await ScanAsync();
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();

        Assert.Empty(Searches());
        Assert.Empty(_service.Waiting);
    }

    [Fact]
    [Trait("TestCase", "T491-7")]
    public async Task Without_a_key_nothing_is_searched_and_nothing_waits()
    {
        ThreeEchoes();

        await ScanAsync();

        Assert.Equal(0, _sgdb.Count);
        Assert.Empty(_service.Waiting);
    }

    [Theory]
    [Trait("TestCase", "T491-8")]
    [InlineData("offline")]
    [InlineData("hangs")]
    [InlineData("server error")]
    [InlineData("broken json")]
    public async Task A_failed_search_puts_nothing_on_the_list_and_asks_again_at_the_next_scan(string failure)
    {
        await WithKeyAsync();
        ThreeEchoes();
        switch (failure)
        {
            case "offline": _sgdb.Offline = true; break;
            case "hangs": _sgdb.Hangs = true; break;
            case "server error": _sgdb.Failure = System.Net.HttpStatusCode.InternalServerError; break;
            default: _sgdb.Body = "{ not json"; break;
        }

        var scan = ScanAsync(Echo, GameOf("second", "Echo Drift II"));
        if (failure == "hangs")
        {
            while (_sgdb.Count == 0)
            {
                await Task.Delay(5);
            }

            _time.Advance(SteamGridDbClient.Timeout);
        }

        await scan;

        // One request for the run: SteamGridDB stopped after the first game failed.
        Assert.Single(_sgdb.Asked());
        Assert.Empty(_service.Waiting);
        (_sgdb.Offline, _sgdb.Hangs, _sgdb.Failure, _sgdb.Body) = (false, false, null, null);

        await ScanAsync();

        Assert.Single(_service.Waiting);
    }

    [Fact]
    [Trait("TestCase", "T491-10")]
    public async Task Several_matching_games_wait_for_the_player_and_their_name_isnt_searched_again()
    {
        await WithKeyAsync();
        ThreeEchoes();
        _source.Cover = [new("other-art", Download: new Uri("https://images.example/echo.png"))];
        _images.Serve("https://images.example/echo.png", TestImages.Png(600, 900));

        await ScanAsync();

        var waiting = Assert.Single(_service.Waiting);
        Assert.Equal(Echo.Key, waiting.Game.Key);
        Assert.Equal([501, 502, 503], waiting.Hits.Select(h => h.Id));
        Assert.Equal(2019, waiting.Hits[0].Year);
        Assert.Equal("other-art", Cached()!.Cover!.Origin);
        Assert.Null(Cached()!.Hero);
        Assert.DoesNotContain(_images.Requests, r => r.RequestUri!.Host == "cdn.example");
        Assert.Single(Searches());
        _sgdb.Requests.Clear();

        await ScanAsync();
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        await RestartAsync();
        await ScanAsync();

        Assert.Empty(Searches());
        Assert.Single(_service.Waiting);
    }

    [Fact]
    [Trait("TestCase", "T491-11")]
    public async Task With_ersten_nehmen_the_first_hit_is_taken_and_nothing_waits()
    {
        await WithKeyAsync();
        _settings = _settings with { SeveralHits = SeveralHits.TakeFirst };
        var games = Enumerable.Range(1, 20).Select(i => GameOf("g" + i)).ToArray();
        ThreeEchoes();

        await ScanAsync(games);

        Assert.Empty(_service.Waiting);
        Assert.All(games, g => Assert.Equal(Cover501, Cached(g.Key)!.Cover!.Stamp));
    }

    [Fact]
    [Trait("TestCase", "T491-12")]
    public async Task Switching_to_ersten_nehmen_takes_the_first_hit_of_every_waiting_game()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync(Echo, GameOf("two"));
        Assert.Equal(2, _service.Waiting.Count);

        Assert.Equal(2, _service.TakeFirstForAllWaiting());
        await _service.WhenIdleAsync();

        Assert.Empty(_service.Waiting);
        Assert.Equal(Cover501, Cached()!.Cover!.Stamp);
        Assert.Equal(Cover501, Cached("other:two")!.Cover!.Stamp);
        Assert.Equal(0, _service.TakeFirstForAllWaiting());
    }

    [Fact]
    [Trait("TestCase", "T491-15")]
    public async Task A_chosen_hit_gives_the_game_its_automatic_images_at_once()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        var changed = new List<string>();
        _service.Changed += (_, keys) => changed.AddRange(keys);

        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[1]);

        Assert.Empty(_service.Waiting);
        Assert.Equal((Cover502, false, ArtworkSource.SteamGridDb), (Cached()!.Cover!.Stamp, Cached()!.Cover!.SetByHand, Cached()!.Cover!.Source));
        Assert.Equal(Hero502, Cached()!.Hero!.Stamp);
        Assert.Contains(Echo.Key, changed);
    }

    [Fact]
    [Trait("TestCase", "T491-15")]
    public async Task A_chosen_hit_leaves_an_image_set_by_hand()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        var own = Path.Combine(_root, "own.png");
        File.WriteAllBytes(own, TestImages.Png(600, 900));
        Assert.Equal(OwnFileResult.Taken, await _service.UseFileAsync(Echo, ArtworkKind.Cover, own));

        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[0]);

        Assert.Equal((ArtworkSource.File, true), (Cached()!.Cover!.Source, Cached()!.Cover!.SetByHand));
        Assert.Equal(Hero501, Cached()!.Hero!.Stamp);
    }

    [Fact]
    [Trait("TestCase", "T491-16")]
    public async Task A_skipped_game_is_never_searched_by_name_again()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();

        _service.Skip(Echo);
        _sgdb.Requests.Clear();
        await ScanAsync();
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        _service.ReloadAll(overwriteSetByHand: true);
        await _service.WhenIdleAsync();
        await RestartAsync();
        await ScanAsync();

        Assert.Empty(_service.Waiting);
        Assert.Equal(0, _sgdb.Count);
        Assert.Null(Cached());
    }

    [Fact]
    [Trait("TestCase", "T491-22")]
    public async Task A_chosen_game_is_asked_by_its_steamgriddb_game_after_a_restart_and_a_reload()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[1]);

        await RestartAsync();
        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, [Echo]), _source)]);
        await _service.WhenIdleAsync();
        _service.ReloadAll(overwriteSetByHand: false);
        await _service.WhenIdleAsync();

        Assert.Empty(Searches());
        Assert.Contains("grids/game/502", _sgdb.Asked());
        Assert.Empty(_service.Waiting);
        Assert.Equal(Cover502, Cached()!.Cover!.Stamp);
    }

    [Fact]
    [Trait("TestCase", "T491-22")]
    public async Task A_waiting_game_waits_after_a_restart_with_the_same_hits_and_no_new_search()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync(Echo, GameOf("later", "Later Game"));
        _sgdb.Finds("Later Game", (601, "Later Game", null), (602, "Later Game", 2001));
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        Assert.Equal(["other:echo", "other:later"], _service.Waiting.Select(w => w.Game.Key));

        await RestartAsync();
        await ScanAsync(GameOf("later", "Later Game"), Echo);

        Assert.Empty(Searches());
        Assert.Equal(["other:echo", "other:later"], _service.Waiting.Select(w => w.Game.Key));
        Assert.Equal([501, 502, 503], _service.Waiting[0].Hits.Select(h => h.Id));
        Assert.Null(_service.Waiting[1].Hits[0].Year);
    }

    [Fact]
    [Trait("TestCase", "T491-23")]
    public async Task A_choice_and_a_skip_survive_the_game_leaving_the_library_and_coming_back()
    {
        await WithKeyAsync();
        ThreeEchoes();
        var other = GameOf("two");
        await ScanAsync(Echo, other);
        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[1]);
        _service.Skip(other);

        await ScanAsync(GameOf("third", "Unrelated"));
        _sgdb.Requests.Clear();
        await ScanAsync(Echo, other);

        Assert.Empty(Searches());
        Assert.Empty(_service.Waiting);
        Assert.Equal(Cover502, Cached()!.Cover!.Stamp);
    }

    [Fact]
    [Trait("TestCase", "T491-25")]
    public async Task Closing_during_a_name_search_doesnt_wait_and_the_next_start_keeps_the_list_and_searches_on()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        var later = GameOf("later", "Later Game");
        _sgdb.Finds("Later Game", (601, "Later Game", null), (602, "Later Game", 2001));
        _sgdb.Hangs = true;
        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, [Echo, later]), _source)]);
        for (var i = 0; i < 500 && Searches().Count == 0; i++)
        {
            await Task.Delay(10);
        }

        _service.Dispose();
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        _sgdb.Hangs = false;
        await RestartAsync();
        await ScanAsync(Echo, later);

        Assert.Equal(["search/autocomplete/Later Game"], Searches());
        Assert.Equal(["other:echo", "other:later"], _service.Waiting.Select(w => w.Game.Key));
    }

    [Fact]
    [Trait("TestCase", "T491-19")]
    public async Task A_hits_cover_is_loaded_for_its_tile_from_the_image_host_without_the_key()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        var hits = _service.Waiting[0].Hits;

        var cover = await _service.HitCoverAsync(Echo, hits[1]);
        var none = await _service.HitCoverAsync(Echo, hits[2]);

        Assert.NotNull(cover);
        Assert.True(File.Exists(cover.Path));
        Assert.Null(none);
        Assert.All(_images.Requests, r => Assert.Null(r.Headers.Authorization));
        Assert.Contains(_images.Requests, r => r.RequestUri!.AbsoluteUri == Cover502);
    }

    [Fact]
    [Trait("TestCase", "T491-19")]
    public async Task Offline_a_hit_has_no_cover_but_can_still_be_taken()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        var hit = _service.Waiting[0].Hits[0];
        _images.Offline = true;

        Assert.Null(await _service.HitCoverAsync(Echo, hit));
        await _service.ChooseAsync(Echo, hit);
        Assert.Empty(_service.Waiting);

        _images.Offline = false;
        await ScanAsync();
        Assert.Equal(Cover501, Cached()!.Cover!.Stamp);
    }

    [Fact]
    [Trait("TestCase", "T491-15")]
    public async Task Artwork_aendern_offers_the_images_of_the_chosen_game_and_none_while_it_waits()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();

        Assert.Empty(await _service.Suggestions!.FindAsync(Echo, ArtworkKind.Cover, default));
        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[0]);

        Assert.Equal([Cover501], (await _service.Suggestions!.FindAsync(Echo, ArtworkKind.Cover, default)).Select(s => s.Download.AbsoluteUri));
        Assert.Empty(Searches().Skip(1));
    }

    /// <summary>A launcher with optional store ids per game key and images to download.</summary>
    private sealed class Source : ILauncherArtwork, IArtworkLookup
    {
        public Dictionary<string, ArtworkStoreId> StoreIds { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<ArtworkCandidate> Cover { get; set; } = [];

        public IReadOnlyList<ArtworkCandidate> Hero { get; set; } = [];

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;

        public ArtworkStoreId? StoreIdOf(Game game) => StoreIds.GetValueOrDefault(game.Key);
    }
}
