using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #506 part B: when the artwork run goes online (new games only, never on a rescan, the first key, "Jetzt suchen"),
/// cancelling a search, sources switched off and the images on disk following order and tolerance at once. Made-up
/// games, a fake SteamGridDB and a fake image server count every request.
/// </summary>
public sealed class ArtworkSearchTriggerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-506-").FullName;
    private readonly FakeImageServer _images = new();
    private readonly FakeSteamGridDb _sgdb = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly Source _source = new();
    private ArtworkSettings _settings = new();
    private SteamGridDbKey _key;
    private ArtworkService _service;

    public ArtworkSearchTriggerTests()
    {
        _key = NewKey();
        _service = NewService();
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private string ArtworkRoot => Path.Combine(_root, "artwork");

    private SteamGridDbKey NewKey() =>
        new(new SteamGridDbKeyStore(Path.Combine(_root, "data"), data => data, data => data), new SteamGridDbClient(new HttpClient(_sgdb), _time) { Log = Log }, _time);

    private ArtworkService NewService() => new(new ArtworkCache(ArtworkRoot), new HttpClient(_images), _time)
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

    private List<string> LogLines()
    {
        lock (_log)
        {
            return [.. _log];
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

    private void ClearRequests()
    {
        lock (_sgdb.Requests)
        {
            _sgdb.Requests.Clear();
        }

        lock (_images.Requests)
        {
            _images.Requests.Clear();
        }
    }

    private int ImageRequests
    {
        get
        {
            lock (_images.Requests)
            {
                return _images.Requests.Count;
            }
        }
    }

    private static Game GameOf(string id) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri($"steam://rungameid/{id}"),
    };

    private static readonly LauncherInfo Steam = new("steam", "Steam", "steam", null);

    private static string[] Ids(int count) => [.. Enumerable.Range(1, count).Select(i => (4100000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture))];

    private void Scan(params string[] ids) => _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, [.. ids.Select(GameOf)]), _source)]);

    private async Task ScanAsync(params string[] ids)
    {
        Scan(ids);
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task SearchAsync()
    {
        Assert.True(_service.SearchAgain(byPlayer: true));
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task ChangeSettingsAsync(ArtworkSettings settings)
    {
        _settings = settings;
        _service.SettingsChanged();
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private CachedArtwork? Cached(string id) => new ArtworkCache(ArtworkRoot).Read("steam:" + id);

    private static string GridCover(string id) => $"https://cdn.example/grid/{id}.png";

    private static string GridHero(string id) => $"https://cdn.example/hero/{id}.png";

    /// <summary>SteamGridDB has a fitting cover (and hero) for the game, and the image host serves them.</summary>
    private void SteamGridDbHas(string id, bool hero = false)
    {
        _sgdb.Cover("steam", id, GridCover(id), 600, 900);
        _images.Serve(GridCover(id), TestImages.Png(600, 900));
        if (hero)
        {
            _sgdb.Hero("steam", id, GridHero(id), 1920, 620);
            _images.Serve(GridHero(id), TestImages.Png(1920, 620));
        }
        else
        {
            _sgdb.Knows("heroes", "steam", id);
        }
    }

    /// <summary>The launcher's own cover as a file on this PC, as Steam's library cache holds it.</summary>
    private string LauncherCoverFile(string id, int width, int height)
    {
        var path = Path.Combine(_root, "launcher", id + ".png");
        TestImages.Write(path, TestImages.Png(width, height));
        _source.Covers[id] = [new ArtworkCandidate("steam-cache", File: path)];
        return path;
    }

    private IEnumerable<string> AskedFor(string id) => _sgdb.Asked().Where(path => path.EndsWith("/" + id, StringComparison.Ordinal));

    [Fact]
    [Trait("TestCase", "T506-40")]
    public async Task A_scan_asks_steamgriddb_only_for_the_new_game_and_says_nothing()
    {
        await WithKeyAsync();
        var ids = Ids(3);
        foreach (var id in ids)
        {
            SteamGridDbHas(id);
        }

        await ScanAsync(ids[0], ids[1]);
        ClearRequests();
        var seen = new List<ArtworkRun>();
        _service.SearchChanged += (_, _) =>
        {
            lock (seen)
            {
                if (_service.Search.Running is { } running)
                {
                    seen.Add(running);
                }
            }
        };

        await ScanAsync(ids);

        Assert.All(_sgdb.Asked(), path => Assert.EndsWith("/" + ids[2], path, StringComparison.Ordinal));
        Assert.NotEmpty(AskedFor(ids[2]));
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(ids[2])!.Cover!.Origin);
        Assert.Null(_service.Search.Result);
        Assert.All(seen, running => Assert.Equal((1, false), (running.Total, running.ByPlayer)));
    }

    [Fact]
    [Trait("TestCase", "T506-41")]
    public async Task A_rescan_without_a_new_game_sends_no_request_to_any_source_but_reads_the_launchers_files()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        _sgdb.Knows("grids", "steam", id).Knows("heroes", "steam", id);
        _source.Heroes[id] = [new ArtworkCandidate("steam-server", Download: new Uri("https://images.example/" + id + "/hero.jpg"))];
        var cover = LauncherCoverFile(id, 600, 900);
        await ScanAsync(id);
        Assert.NotEqual(0, _sgdb.Count);
        Assert.Null(_service.Of("steam:" + id)!.Hero);
        ClearRequests();
        var logged = LogLines().Count;

        // The launcher's file changed: read at the rescan; its download and SteamGridDB wait for a search.
        TestImages.Write(cover, TestImages.Png(400, 600));
        await ScanAsync(id);
        await ScanAsync(id);

        Assert.Equal((0, 0), (_sgdb.Count, ImageRequests));
        Assert.Equal(new ImageSize(400, 600), _service.Of("steam:" + id)!.Cover!.Size);
        Assert.DoesNotContain(LogLines().Skip(logged), line => line.StartsWith("Artwork-Suche", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T506-42")]
    public async Task A_new_game_that_hit_no_network_is_asked_again_only_by_Jetzt_suchen_also_after_a_restart()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        SteamGridDbHas(id);
        _sgdb.Offline = true;
        await ScanAsync(id);
        Assert.Equal(1, _sgdb.Count);
        _sgdb.Offline = false;
        ClearRequests();

        await ScanAsync(id);
        Restart();
        await ScanAsync(id);
        Assert.Equal(0, _sgdb.Count);

        await SearchAsync();
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T506-43")]
    public async Task A_new_game_steamgriddb_doesnt_know_by_id_is_searched_by_name_in_the_same_run()
    {
        await WithKeyAsync();
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        _sgdb.Finds(GameOf(ids[1]).Title, (701, GameOf(ids[1]).Title, null));
        _sgdb.Cover("game", "701", "https://cdn.example/grid/701.png", 600, 900);
        _images.Serve("https://cdn.example/grid/701.png", TestImages.Png(600, 900));

        await ScanAsync(ids);

        Assert.Equal("https://cdn.example/grid/701.png", Cached(ids[1])!.Cover!.Stamp);
        Assert.Single(LogLines(), line => line.StartsWith("Artwork-Suche gestartet", StringComparison.Ordinal));
        ClearRequests();
        await ScanAsync(ids);
        Assert.Equal(0, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T506-44")]
    public async Task A_game_arriving_without_a_key_or_with_steamgriddb_off_is_known_without_a_request()
    {
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        SteamGridDbHas(ids[1]);
        await ScanAsync(ids[0]);
        await WithKeyAsync();
        await ScanAsync(ids[0]);
        Assert.Equal(0, _sgdb.Count);

        _settings = _settings with { Off = [ArtworkSourceKind.SteamGridDb] };
        await ScanAsync(ids);
        await ChangeSettingsAsync(_settings with { Off = [] });
        await ScanAsync(ids);
        Assert.Equal(0, _sgdb.Count);

        // The first key's search (or "Jetzt suchen") asks them.
        Assert.True(_service.SearchAgain(byPlayer: false));
        await _service.WhenIdleAsync();
        Assert.NotEmpty(AskedFor(ids[0]));
        Assert.NotEmpty(AskedFor(ids[1]));
    }

    [Fact]
    [Trait("TestCase", "T506-45")]
    public async Task Games_leaving_with_their_launcher_and_coming_back_are_not_new()
    {
        await WithKeyAsync();
        var ids = Ids(3);
        await ScanAsync(ids);
        _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, []), _source)]);
        await _service.WhenIdleAsync();
        ClearRequests();

        await ScanAsync(ids);

        Assert.Equal(0, _sgdb.Count);
    }

    [Fact]
    [Trait("TestCase", "T506-49")]
    public async Task Each_search_writes_its_start_and_end_with_its_trigger_never_the_key_or_a_name()
    {
        await WithKeyAsync();
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        await ScanAsync(ids[0]);
        await ScanAsync(ids[0]);
        Assert.True(_service.SearchAgain(byPlayer: false));
        await _service.WhenIdleAsync();
        await SearchAsync();
        Assert.True(_service.ReloadAll(overwriteSetByHand: false));
        await _service.WhenIdleAsync();

        var runs = LogLines().Where(line => line.StartsWith("Artwork-Suche", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                "Artwork-Suche gestartet (neue Spiele): 1 Spiele",
                "Artwork-Suche beendet (neue Spiele): 1 von 1 Spielen, 1 Cover, 0 Hintergründe",
                "Artwork-Suche gestartet (erster Schlüssel): 1 Spiele",
                "Artwork-Suche beendet (erster Schlüssel): 1 von 1 Spielen, 0 Cover, 0 Hintergründe",
                "Artwork-Suche gestartet (Jetzt suchen): 1 Spiele",
                "Artwork-Suche beendet (Jetzt suchen): 1 von 1 Spielen, 0 Cover, 0 Hintergründe",
                "Artwork-Suche gestartet (Neu laden): 1 Spiele",
                "Artwork-Suche beendet (Neu laden): 1 von 1 Spielen, 1 Cover, 0 Hintergründe",
            ],
            runs);
        Assert.DoesNotContain(LogLines(), line => line.Contains(FakeSteamGridDb.Key, StringComparison.Ordinal) || line.Contains("Made-up Game", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T506-52")]
    public async Task A_changed_order_picks_among_the_images_on_disk_at_once_without_a_request()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        SteamGridDbHas(id);

        // 667 × 900 cut to 2 : 3 loses 10 %: it fits.
        LauncherCoverFile(id, 667, 900);
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };
        await ScanAsync(id);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
        ClearRequests();

        await ChangeSettingsAsync(_settings with { Order = ArtworkSettings.DefaultOrder });
        Assert.Equal(("steam-cache", ArtworkService.SteamGridDbOrigin), (Cached(id)!.Cover!.Origin, Cached(id)!.OffCover!.Origin));

        await ChangeSettingsAsync(_settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] });
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
        Assert.Equal(new ImageSize(600, 900), _service.Of("steam:" + id)!.Cover!.Size);
        Assert.Equal((0, 0), (_sgdb.Count, ImageRequests));
    }

    [Fact]
    [Trait("TestCase", "T506-52")]
    public async Task A_changed_order_leaves_an_image_set_by_hand()
    {
        var id = Ids(1)[0];
        LauncherCoverFile(id, 600, 900);
        await ScanAsync(id);
        var cache = new ArtworkCache(ArtworkRoot);
        var held = cache.Read("steam:" + id)!;
        cache.WriteMetadata(held with { Cover = held.Cover! with { SetByHand = true } });
        Restart();
        await ScanAsync(id);

        await ChangeSettingsAsync(_settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher], Off = [ArtworkSourceKind.Launcher] });

        Assert.True(Cached(id)!.Cover!.SetByHand);
        Assert.NotNull(_service.Of("steam:" + id)!.Cover);
    }

    [Fact]
    [Trait("TestCase", "T506-54")]
    [Trait("TestCase", "T506-56")]
    public async Task Suche_abbrechen_stops_after_the_request_in_flight_keeps_what_it_found_and_the_next_search_asks_the_rest()
    {
        // Scanned before the key: SteamGridDB knows the games by their Steam id at the search.
        var ids = Ids(3);
        await ScanAsync(ids);
        await WithKeyAsync();
        foreach (var id in ids)
        {
            SteamGridDbHas(id);
        }

        _images.Hang(GridCover(ids[1]));
        ClearRequests();

        Assert.True(_service.SearchAgain(byPlayer: true));
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var asked = _sgdb.Count;
        Assert.True(_service.CancelSearch());
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(asked, _sgdb.Count);
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(ids[0])!.Cover!.Origin);
        Assert.Null(Cached(ids[1])?.Cover);
        Assert.Empty(AskedFor(ids[2]));
        Assert.Equal((null, null, 2), (_service.Search.Running, _service.Search.Result, _service.Search.MissingCovers));
        Assert.False(_service.CancelSearch());
        Assert.Contains("Artwork-Suche abgebrochen (Jetzt suchen): 1 von 3 Spielen, 1 Cover, 0 Hintergründe", LogLines());

        _images.Serve(GridCover(ids[1]), TestImages.Png(600, 900));
        ClearRequests();
        await SearchAsync();
        Assert.DoesNotContain($"grids/steam/{ids[0]}", _sgdb.Asked());
        Assert.All(ids, id => Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin));
    }

    [Fact]
    [Trait("TestCase", "T506-55")]
    public async Task Cancelling_neu_laden_keeps_the_images_replaced_and_the_others_as_they_were()
    {
        var ids = Ids(2);
        foreach (var id in ids)
        {
            LauncherCoverFile(id, 600, 900);
        }

        await ScanAsync(ids);
        var before = Cached(ids[1])!.Cover;
        var url = "https://images.example/" + ids[1] + "/hero.jpg";
        _source.Heroes[ids[1]] = [new ArtworkCandidate("steam-server", Download: new Uri(url))];
        _images.Hang(url);

        Assert.True(_service.ReloadAll(overwriteSetByHand: false));
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_service.CancelSearch());
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(_service.Of("steam:" + ids[0])!.Cover);
        Assert.Equal(before, Cached(ids[1])!.Cover);
        Assert.NotNull(_service.Of("steam:" + ids[1])!.Cover);
    }

    [Fact]
    [Trait("TestCase", "T506-57")]
    public async Task A_search_for_new_games_can_be_cancelled_and_the_games_it_didnt_reach_wait_for_Jetzt_suchen()
    {
        await WithKeyAsync();
        var ids = Ids(3);
        foreach (var id in ids)
        {
            SteamGridDbHas(id);
        }

        _images.Hang(GridCover(ids[0]));
        Scan(ids);
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(_service.Search.Running);
        Assert.True(_service.CancelSearch());
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        ClearRequests();

        await ScanAsync(ids);
        Restart();
        await ScanAsync(ids);
        Assert.Equal(0, _sgdb.Count);

        _images.Serve(GridCover(ids[0]), TestImages.Png(600, 900));
        await SearchAsync();
        Assert.All(ids, id => Assert.NotEmpty(AskedFor(id)));
    }

    [Fact]
    [Trait("TestCase", "T506-60")]
    public async Task A_source_switched_off_hides_its_images_at_once_keeps_them_on_disk_and_brings_them_back_without_a_download()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        SteamGridDbHas(id);
        LauncherCoverFile(id, 600, 900);
        _settings = _settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] };
        await ScanAsync(id);
        await ChangeSettingsAsync(_settings with { Order = ArtworkSettings.DefaultOrder });
        var launcherFile = _service.Of("steam:" + id)!.Cover!.Path;
        ClearRequests();

        // Launcher images off: SteamGridDB's cover on disk takes its place at once, the launcher's stays on disk.
        _settings = _settings with { Off = [ArtworkSourceKind.Launcher] };
        _service.SettingsChanged();
        Assert.NotEqual(launcherFile, _service.Of("steam:" + id)?.Cover?.Path);
        await _service.WhenIdleAsync();
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
        var kept = Cached(id)!.OffCover!;
        Assert.Equal(ArtworkSource.Launcher, kept.Source);
        Assert.True(File.Exists(Path.Combine(new ArtworkCache(ArtworkRoot).FolderOf("steam:" + id), kept.File)));

        // On again: the launcher's cover is back; then SteamGridDB off and on: the same.
        await ChangeSettingsAsync(_settings with { Off = [] });
        Assert.Equal("steam-cache", Cached(id)!.Cover!.Origin);
        await ChangeSettingsAsync(_settings with { Order = [ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher] });
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
        await ChangeSettingsAsync(_settings with { Off = [ArtworkSourceKind.SteamGridDb] });
        Assert.Equal("steam-cache", Cached(id)!.Cover!.Origin);
        await ChangeSettingsAsync(_settings with { Off = [] });
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
        Assert.Equal((0, 0), (_sgdb.Count, ImageRequests));
    }

    [Fact]
    [Trait("TestCase", "T506-61")]
    public async Task Both_sources_off_leave_placeholders_except_images_set_by_hand_and_ask_nothing()
    {
        await WithKeyAsync();
        var ids = Ids(2);
        foreach (var id in ids)
        {
            SteamGridDbHas(id, hero: true);
            LauncherCoverFile(id, 600, 900);
        }

        await ScanAsync(ids);
        var cache = new ArtworkCache(ArtworkRoot);
        var held = cache.Read("steam:" + ids[0])!;
        cache.WriteMetadata(held with { Cover = held.Cover! with { SetByHand = true } });
        Restart();
        await ScanAsync(ids);
        ClearRequests();

        await ChangeSettingsAsync(_settings with { Off = [ArtworkSourceKind.Launcher, ArtworkSourceKind.SteamGridDb] });
        await ScanAsync([.. ids, Ids(3)[2]]);
        await SearchAsync();

        Assert.Equal((true, null), (Cached(ids[0])!.Cover!.SetByHand, _service.Of("steam:" + ids[0])!.Hero));
        Assert.Null(_service.Of("steam:" + ids[1]));
        Assert.Equal((0, 0), (_sgdb.Count, ImageRequests));
    }

    [Fact]
    [Trait("TestCase", "T506-62")]
    public async Task With_steamgriddb_off_no_request_goes_by_id_or_name_and_the_key_stays()
    {
        await WithKeyAsync();
        _settings = _settings with { Off = [ArtworkSourceKind.SteamGridDb] };
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        _source.WithoutStoreId.Add(ids[1]);
        _sgdb.Finds("Made-up Game " + ids[1], (501, "Made-up Game " + ids[1], null));

        await ScanAsync(ids);
        await SearchAsync();
        Assert.True(_service.ReloadAll(overwriteSetByHand: false));
        await _service.WhenIdleAsync();

        Assert.Equal(0, _sgdb.Count);
        Assert.Equal(SteamGridDbKeyState.Valid, _key.State);
    }

    [Fact]
    [Trait("TestCase", "T506-67")]
    [Trait("TestCase", "T506-68")]
    public async Task Artwork_aendern_offers_nothing_of_a_source_switched_off_and_asks_it_nothing()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        SteamGridDbHas(id);
        LauncherCoverFile(id, 600, 900);
        await ScanAsync(id);
        ClearRequests();

        _settings = _settings with { Off = [ArtworkSourceKind.SteamGridDb] };
        var choices = await _service.ChoicesAsync(GameOf(id), ArtworkKind.Cover);
        Assert.Equal(0, _sgdb.Count);
        Assert.All(choices.Offers, offer => Assert.Equal(ArtworkSource.Launcher, offer.Source));
        Assert.Equal([ArtworkSourceKind.SteamGridDb], choices.Off);

        _settings = _settings with { Off = [ArtworkSourceKind.Launcher] };
        choices = await _service.ChoicesAsync(GameOf(id), ArtworkKind.Cover);
        Assert.DoesNotContain(choices.Offers, offer => offer.Source == ArtworkSource.Launcher);
        Assert.Contains(choices.Offers, offer => offer.Source == ArtworkSource.SteamGridDb);
    }

    [Fact]
    [Trait("TestCase", "T506-69")]
    public async Task Closing_during_a_search_for_new_games_leaves_the_games_it_didnt_reach_new_for_the_next_start()
    {
        await WithKeyAsync();
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        SteamGridDbHas(ids[1]);
        _images.Hang(GridCover(ids[1]));
        Scan(ids);
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));

        _service.Dispose();
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        _images.Serve(GridCover(ids[1]), TestImages.Png(600, 900));
        Restart();
        ClearRequests();
        await ScanAsync(ids);

        Assert.Empty(AskedFor(ids[0]));
        Assert.NotEmpty(AskedFor(ids[1]));
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(ids[1])!.Cover!.Origin);
    }

    [Theory]
    [Trait("TestCase", "T506-70")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    public async Task Without_a_usable_list_of_known_games_but_images_in_the_cache_every_game_counts_as_known(string? stored)
    {
        await WithKeyAsync();
        var ids = Ids(3);
        foreach (var id in ids)
        {
            SteamGridDbHas(id);
        }

        // An earlier version left images, but no list.
        var cache = new ArtworkCache(ArtworkRoot);
        cache.WriteImage("steam:" + ids[0], ArtworkKind.Hero, TestImages.Png(1920, 620), ".png");
        cache.WriteMetadata(new CachedArtwork("steam:" + ids[0], null, new CachedImage("hero.png", ArtworkSource.Launcher, "steam-cache", "1:1")));
        var list = Path.Combine(ArtworkRoot, "known-games.json");
        if (stored is not null)
        {
            File.WriteAllText(list, stored);
        }

        await ScanAsync(ids[0], ids[1]);
        Assert.Equal(0, _sgdb.Count);
        Assert.True(File.Exists(list));
        Assert.Empty(Directory.EnumerateFiles(ArtworkRoot, "*.tmp"));
        if (stored is { Length: > 0 } and not "null")
        {
            Assert.Contains(LogLines(), line => line.Contains("Liste bekannter Spiele nicht lesbar", StringComparison.Ordinal));
        }

        // From now on a game is new only when it comes.
        await ScanAsync(ids);
        Assert.Equal([$"grids/steam/{ids[2]}", $"heroes/steam/{ids[2]}"], _sgdb.Asked());
    }

    [Fact]
    [Trait("TestCase", "T506-70")]
    public async Task At_the_very_first_start_every_game_is_new()
    {
        await WithKeyAsync();
        var id = Ids(1)[0];
        SteamGridDbHas(id);

        await ScanAsync(id);

        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(id)!.Cover!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T506-71")]
    public async Task A_new_game_arriving_during_a_run_is_asked_once_right_after_it()
    {
        await WithKeyAsync();
        var ids = Ids(2);
        SteamGridDbHas(ids[0]);
        SteamGridDbHas(ids[1]);
        _images.Hang(GridCover(ids[0]));
        Scan(ids[0]);
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Scan(ids);
        Scan(ids);
        _time.Advance(ArtworkService.DownloadTimeout);
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(_sgdb.Asked(), path => path == $"grids/steam/{ids[1]}");
        Assert.Equal(ArtworkService.SteamGridDbOrigin, Cached(ids[1])!.Cover!.Origin);
    }

    [Fact]
    [Trait("TestCase", "T506-72")]
    public async Task SteamGridDB_switched_off_during_a_run_is_asked_nothing_after_the_request_in_flight()
    {
        var ids = Ids(2);
        await ScanAsync(ids);
        await WithKeyAsync();
        SteamGridDbHas(ids[0], hero: true);
        SteamGridDbHas(ids[1], hero: true);
        _images.Hang(GridCover(ids[0]));
        ClearRequests();

        Assert.True(_service.SearchAgain(byPlayer: true));
        await _images.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _settings = _settings with { Off = [ArtworkSourceKind.SteamGridDb] };
        _service.SettingsChanged();
        _time.Advance(ArtworkService.DownloadTimeout);
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([$"grids/steam/{ids[0]}"], _sgdb.Asked());
        await ChangeSettingsAsync(_settings with { Off = [] });
        Assert.Equal(1, _sgdb.Count);
    }

    /// <summary>A launcher with images per game (a file or a download) and the game's Steam id unless left out.</summary>
    private sealed class Source : ILauncherArtwork, IArtworkLookup
    {
        public Dictionary<string, IReadOnlyList<ArtworkCandidate>> Covers { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<ArtworkCandidate>> Heroes { get; } = new(StringComparer.Ordinal);

        public HashSet<string> WithoutStoreId { get; } = new(StringComparer.Ordinal);

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) =>
            (kind == ArtworkKind.Cover ? Covers : Heroes).GetValueOrDefault(game.Id) ?? [];

        public ArtworkStoreId? StoreIdOf(Game game) => WithoutStoreId.Contains(game.Id) ? null : new(ArtworkStoreId.Steam, game.Id);
    }
}
