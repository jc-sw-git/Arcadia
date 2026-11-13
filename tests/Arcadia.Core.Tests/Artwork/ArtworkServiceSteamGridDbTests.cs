using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #489: SteamGridDB as a second source of the artwork run: no request without a key, the order of the sources and
/// the 15 % crop rule (#566), a turned-down key, the counts and the search again (T489-12, T489-13, T489-15 to T489-23; T507-1).
/// </summary>
public sealed class ArtworkServiceSteamGridDbTests : IDisposable
{
    private const string Id = "4000001";
    private const string LauncherCover = "https://images.example/4000001/cover.jpg";
    private const string LauncherHero = "https://images.example/4000001/hero.jpg";
    private const string GridCover = "https://cdn.example/grid/4000001.png";
    private const string GridHero = "https://cdn.example/hero/4000001.png";

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-sgdb-").FullName;
    private readonly FakeImageServer _images = new();
    private readonly FakeSteamGridDb _sgdb = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly StoreSource _source = new();
    private ArtworkSettings _settings = new();
    private SteamGridDbKey _key;
    private ArtworkService _service;

    public ArtworkServiceSteamGridDbTests()
    {
        _key = NewKey();
        _service = NewService();
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

    private void Restart()
    {
        _service.Dispose();
        _key = NewKey();
        _service = NewService();
    }

    private async Task WithKeyAsync()
    {
        Assert.Equal(SteamGridDbOutcome.Found, await _key.SetAsync(FakeSteamGridDb.Key));
        _sgdb.Requests.Clear();
    }

    private static Game GameOf(string id) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri($"steam://rungameid/{id}"),
    };

    private static readonly LauncherInfo Steam = new("steam", "Steam", "steam", null);

    private async Task ScanAsync(params Game[] games)
    {
        _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, games.Length == 0 ? [GameOf(Id)] : games), _source)]);
        await _service.WhenIdleAsync();
    }

    /// <summary>"Jetzt suchen": a scan asks SteamGridDB only for new games (#506).</summary>
    private async Task SearchAsync()
    {
        Assert.True(_service.SearchAgain(byPlayer: true));
        await _service.WhenIdleAsync();
    }

    private CachedArtwork? Cached(string id = Id) => new ArtworkCache(Path.Combine(_root, "artwork")).Read("steam:" + id);

    /// <summary>The launcher offers a cover and a hero to download, SteamGridDB a fitting one of each.</summary>
    private void BothSources(int launcherCoverWidth = 600, int launcherHeroWidth = 1920)
    {
        _source.Cover = [new("steam-server", Download: new Uri(LauncherCover))];
        _source.Hero = [new("steam-server", Download: new Uri(LauncherHero))];
        _images.Serve(LauncherCover, TestImages.Png(launcherCoverWidth, 900));
        _images.Serve(LauncherHero, TestImages.Png(launcherHeroWidth, 620));
        _sgdb.Cover("steam", Id, GridCover, 600, 900).Hero("steam", Id, GridHero, 1920, 620);
        _images.Serve(GridCover, TestImages.Png(600, 900));
        _images.Serve(GridHero, TestImages.Png(1920, 620));
    }

    [Fact]
    [Trait("TestCase", "T489-12")]
    public async Task Without_a_key_not_a_single_request_reaches_steamgriddb()
    {
        _source.Hero = [new("steam-server", Download: new Uri(LauncherHero))];
        _images.Serve(LauncherHero, TestImages.Png(1600, 900));
        _sgdb.Hero("steam", Id, GridHero, 1920, 620);

        await ScanAsync();
        await ScanAsync();
        Assert.True(_service.SearchAgain(byPlayer: true));
        await _service.WhenIdleAsync();

        Assert.Equal(0, _sgdb.Count);
        Assert.Equal(("steam-server", ArtworkSource.Launcher), (Cached()!.Hero!.Origin, Cached()!.Hero!.Source));
        Assert.Equal(new ArtworkSearchResult(0, 0, false, null), _service.Search.Result);
    }

    [Fact]
    [Trait("TestCase", "T507-1")]
    public async Task A_launcher_host_that_cant_be_reached_leaves_steamgriddbs_image()
    {
        await WithKeyAsync();
        _source.Cover = [new("steam-server", Download: new Uri(LauncherCover))];
        _images.Unreachable(LauncherCover);
        _sgdb.Cover("steam", Id, GridCover, 600, 900);
        _images.Serve(GridCover, TestImages.Png(600, 900));

        await ScanAsync();

        Assert.Equal((ArtworkService.SteamGridDbOrigin, ArtworkSource.SteamGridDb), (Cached()!.Cover!.Origin, Cached()!.Cover!.Source));
    }

    [Fact]
    [Trait("TestCase", "T489-15")]
    public async Task When_both_fit_the_first_in_order_wins_and_steamgriddb_isnt_even_asked()
    {
        await WithKeyAsync();
        BothSources();

        await ScanAsync();

        Assert.Equal(("steam-server", "steam-server"), (Cached()!.Cover!.Origin, Cached()!.Hero!.Origin));
        Assert.Equal(0, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-15")]
    public async Task With_steamgriddb_first_its_images_win()
    {
        await WithKeyAsync();
        BothSources();
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };

        await ScanAsync();

        Assert.Equal((ArtworkService.SteamGridDbOrigin, ArtworkSource.SteamGridDb), (Cached()!.Cover!.Origin, Cached()!.Cover!.Source));
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Hero!.Origin);
        Assert.DoesNotContain(_images.Requests, r => r.RequestUri!.AbsoluteUri.StartsWith("https://images.example/", StringComparison.Ordinal));
        // The images' server never gets the key.
        Assert.All(_images.Requests, r => Assert.Null(r.Headers.Authorization));
    }

    [Fact]
    [Trait("TestCase", "T489-15")]
    public async Task With_steamgriddb_first_but_no_key_the_launchers_image_comes_without_a_request()
    {
        BothSources();
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };

        await ScanAsync();

        Assert.Equal("steam-server", Cached()!.Cover!.Origin);
        Assert.Equal(0, _sgdb.Count);
    }

    [Theory]
    [Trait("TestCase", "T566-2")]
    [InlineData(667, true)] // 10 % cut away
    [InlineData(675, true)] // 3 : 4, Epic's format: 11.1 %
    [InlineData(511, true)] // 14.8 %
    [InlineData(505, false)] // 15.8 %
    [InlineData(750, false)] // 20 %, wider than 2 : 3
    public async Task A_launcher_cover_fits_while_the_crop_cuts_away_at_most_fifteen_percent(int coverWidth, bool launcherFits)
    {
        await WithKeyAsync();
        // A cover 2 : 3 needs 600 × 900; narrower ones are cut at top and bottom, wider ones at the sides.
        BothSources(launcherCoverWidth: coverWidth);

        await ScanAsync();

        Assert.Equal(launcherFits ? "steam-server" : ArtworkService.SteamGridDbOrigin, Cached()!.Cover!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T566-2")]
    public async Task A_background_that_cuts_more_than_fifteen_percent_gives_way_to_steamgriddbs()
    {
        await WithKeyAsync();
        // 1500 × 620 (2.42 : 1) loses 22 % cut to the flat 3.1 : 1 and 27 % cut to 16 : 9.
        BothSources(launcherHeroWidth: 1500);

        await ScanAsync();

        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Hero!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T562-1")]
    public async Task A_sixteen_by_nine_background_fits_and_steamgriddb_isnt_asked_for_one()
    {
        // #562: Epic's 16 : 9 image is the detail page's own shape; a key arriving later doesn't replace it.
        _source.Hero = [new("epic-catalog", Download: new Uri(LauncherHero))];
        _images.Serve(LauncherHero, TestImages.Png(2560, 1440));
        _sgdb.Hero("steam", Id, GridHero, 1920, 620);
        _images.Serve(GridHero, TestImages.Png(1920, 620));
        await ScanAsync();

        await WithKeyAsync();
        _service.SearchAgain(byPlayer: false);
        await _service.WhenIdleAsync();

        Assert.Equal("epic-catalog", Cached()!.Hero!.Origin);
        Assert.DoesNotContain("heroes/steam/" + Id, _sgdb.Asked());
    }

    [Fact]
    [Trait("TestCase", "T562-2")]
    public async Task A_flat_background_fits_so_steamgriddb_isnt_asked_for_a_steam_game()
    {
        await WithKeyAsync();
        BothSources();

        await ScanAsync();

        Assert.Equal("steam-server", Cached()!.Hero!.Origin);
        Assert.DoesNotContain("heroes/steam/" + Id, _sgdb.Asked());
    }

    [Fact]
    [Trait("TestCase", "T562-2")]
    public async Task Of_steamgriddbs_backgrounds_a_sixteen_by_nine_one_beats_a_better_rated_flat_one()
    {
        await WithKeyAsync();
        BothSources();
        const string WideHero = "https://cdn.example/hero/4000001-wide.png";
        _sgdb.Hero("steam", Id, WideHero, 1920, 1080, score: -1);
        _images.Serve(WideHero, TestImages.Png(1920, 1080));
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };

        await ScanAsync();

        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Hero!.Origin);
        Assert.Equal(new ImageSize(1920, 1080), _service.Of("steam:" + Id)!.Hero!.Size);
    }

    [Fact]
    [Trait("TestCase", "T489-16")]
    public async Task Without_a_fitting_image_anywhere_the_first_source_with_one_is_cropped()
    {
        await WithKeyAsync();
        _source.Hero = [new("epic-catalog", Download: new Uri(LauncherHero))];
        _images.Serve(LauncherHero, TestImages.Png(2560, 1440));
        _sgdb.Knows("heroes", "steam", Id);

        await ScanAsync();

        Assert.Equal("epic-catalog", Cached()!.Hero!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T489-18")]
    public async Task Steamgriddbs_best_rated_fitting_image_is_taken_and_only_it_is_downloaded()
    {
        await WithKeyAsync();
        _sgdb.Cover("steam", Id, "https://cdn.example/grid/wide.png", 920, 430, score: 50)
            .Cover("steam", Id, "https://cdn.example/grid/good.png", 600, 900, score: 10)
            .Cover("steam", Id, "https://cdn.example/grid/best.png", 600, 900, score: 30);
        _images.Serve("https://cdn.example/grid/best.png", TestImages.Png(600, 900));

        await ScanAsync();

        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Cover!.Origin);
        Assert.Equal(["https://cdn.example/grid/best.png"], _images.Requests.Select(r => r.RequestUri!.AbsoluteUri).Where(u => u.Contains("/grid/", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("TestCase", "T489-22")]
    public async Task An_image_steamgriddb_names_that_isnt_one_leaves_the_placeholder()
    {
        await WithKeyAsync();
        _sgdb.Cover("steam", Id, GridCover, 600, 900);
        _images.Serve(GridCover, "<html>not an image</html>"u8.ToArray());

        await ScanAsync();

        Assert.Null(Cached()?.Cover);
    }

    [Fact]
    [Trait("TestCase", "T489-13")]
    public async Task A_key_turned_down_during_the_run_costs_one_request_not_one_per_game()
    {
        await WithKeyAsync();
        _sgdb.ValidKey = "ffffffffffffffffffffffffffffffff";

        await ScanAsync(GameOf("4000001"), GameOf("4000002"), GameOf("4000003"));

        Assert.Equal(1, _sgdb.Count);
        Assert.Equal(SteamGridDbKeyState.Rejected, _key.State);

        // A later scan sends nothing with that key; after a restart with it stored, a search sends at most one request again.
        await ScanAsync(GameOf("4000001"), GameOf("4000002"), GameOf("4000003"));
        Assert.Equal(1, _sgdb.Count);
        Restart();
        await ScanAsync(GameOf("4000001"), GameOf("4000002"), GameOf("4000003"));
        Assert.Equal(1, _sgdb.Count);
        await SearchAsync();
        Assert.Equal(2, _sgdb.Count);
        Assert.Equal(SteamGridDbKeyState.Rejected, _key.State);
    }

    [Fact]
    [Trait("TestCase", "T489-17")]
    public async Task A_changed_order_changes_no_fitting_image_and_none_set_by_hand()
    {
        await WithKeyAsync();
        BothSources();
        await ScanAsync();
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };

        await ScanAsync();
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();

        Assert.Equal(("steam-server", "steam-server"), (Cached()!.Cover!.Origin, Cached()!.Hero!.Origin));
        Assert.Equal(0, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-20")]
    public async Task A_game_steamgriddb_answered_for_is_asked_again_only_when_searched_again()
    {
        await WithKeyAsync();
        _sgdb.Knows("grids", "steam", Id).Knows("heroes", "steam", Id);

        await ScanAsync();
        await ScanAsync();
        Assert.Equal(2, _sgdb.Count);

        _service.SearchAgain(byPlayer: true);
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        Assert.Equal(4, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-21")]
    public async Task No_connection_to_steamgriddb_skips_it_for_the_run_and_only_the_next_search_tries_again()
    {
        await WithKeyAsync();
        _sgdb.Offline = true;

        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        Assert.Equal(1, _sgdb.Count);

        _sgdb.Offline = false;
        _sgdb.Cover("steam", "4000002", GridCover, 600, 900);
        _images.Serve(GridCover, TestImages.Png(600, 900));
        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        Assert.Equal(1, _sgdb.Count);
        await SearchAsync();

        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached("4000002")!.Cover!.Origin);
        Assert.Equal(SteamGridDbKeyState.Valid, _key.State);
    }

    [Fact]
    [Trait("TestCase", "T489-19")]
    public async Task The_search_counts_whats_missing_and_a_search_by_the_player_says_what_it_found()
    {
        await WithKeyAsync();
        _sgdb.Cover("steam", "4000001", GridCover, 600, 900).Knows("heroes", "steam", "4000001");
        _images.Serve(GridCover, TestImages.Png(600, 900));
        var seen = new List<ArtworkSearch>();
        _service.SearchChanged += (_, _) =>
        {
            lock (seen)
            {
                seen.Add(_service.Search);
            }
        };

        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        Assert.Equal(new ArtworkSearch(1, 2, null, null), _service.Search);
        Assert.Contains(seen, s => s.Running == new ArtworkRun(0, 2, false));

        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();
        Assert.Equal(new ArtworkSearch(1, 2, null, new ArtworkSearchResult(0, 0, true, null)), _service.Search);
        Assert.Contains(seen, s => s.Running is { ByPlayer: true, Total: 2 });
    }

    [Fact]
    [Trait("TestCase", "T489-23")]
    public async Task Closing_during_a_run_doesnt_wait_for_steamgriddb_and_the_next_start_goes_on()
    {
        await WithKeyAsync();
        _sgdb.Hangs = true;
        _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, [GameOf(Id)]), _source)]);
        for (var i = 0; i < 500 && _sgdb.Count == 0; i++)
        {
            await Task.Delay(10);
        }

        _service.Dispose();
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        _sgdb.Hangs = false;
        _sgdb.Cover("steam", Id, GridCover, 600, 900);
        _images.Serve(GridCover, TestImages.Png(600, 900));
        _key = NewKey();
        _service = NewService();
        await ScanAsync();
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Cover!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T489-10")]
    public async Task No_log_line_of_the_run_holds_the_key()
    {
        await WithKeyAsync();
        BothSources();
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };
        await ScanAsync();
        _sgdb.ValidKey = "ffffffffffffffffffffffffffffffff";
        _service.SearchAgain(byPlayer: true);
        await _service.WhenIdleAsync();

        Assert.NotEmpty(_log);
        Assert.DoesNotContain(_log, line => line.Contains(FakeSteamGridDb.Key, StringComparison.Ordinal));
    }

    /// <summary>A launcher whose games have a Steam store id.</summary>
    [Theory]
    [Trait("TestCase", "T489-15")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SteamGridDbs_image_stays_at_the_next_scan_when_the_launcher_offers_a_file(bool steamGridDbFirst)
    {
        // With SteamGridDB first a launcher cover that fits (10 % cut away) gives way; with the launcher first one that
        // doesn't (20 %): either way SteamGridDB's is the one to show.
        var file = Path.Combine(_root, "cover.png");
        File.WriteAllBytes(file, TestImages.Png(steamGridDbFirst ? 667 : 750, 900));
        _source.Cover = [new("steam-cache", File: file)];
        _sgdb.Cover("steam", Id, GridCover, 600, 900);
        _images.Serve(GridCover, TestImages.Png(600, 900));
        if (steamGridDbFirst)
        {
            _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };
        }

        await WithKeyAsync();

        await ScanAsync();
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Cover!.Origin);
        await ScanAsync();
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached()!.Cover!.Origin);
        Assert.Single(_sgdb.Asked(), path => path.StartsWith("grids/", StringComparison.Ordinal));
    }

    private sealed class StoreSource : ILauncherArtwork, IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> Cover { get; set; } = [];

        public IReadOnlyList<ArtworkCandidate> Hero { get; set; } = [];

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;

        public ArtworkStoreId? StoreIdOf(Game game) => new(ArtworkStoreId.Steam, game.Id);
    }
}
