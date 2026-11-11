using System.Net;
using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #487: the background run that brings covers and backgrounds into the cache: the fitting-format choice, downloads and
/// their failures, the cache's failure cases (T487-4 to T487-6, T487-8, T487-10, T487-12 to T487-17).
/// </summary>
public sealed class ArtworkServiceTests : IDisposable
{
    private const string CoverUrl = "https://images.example/4000001/library_600x900.jpg";
    private const string HeroUrl = "https://images.example/4000001/library_hero.jpg";

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-run-").FullName;
    private readonly FakeImageServer _server = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly TestSource _source = new();
    private ArtworkService _service;

    public ArtworkServiceTests() => _service = NewService();

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private string CacheFolder => Path.Combine(_root, "artwork");

    private ArtworkCache Cache => new(CacheFolder);

    private ArtworkService NewService() => new(new ArtworkCache(CacheFolder), new HttpClient(_server), _time)
    {
        Log = line =>
        {
            lock (_log)
            {
                _log.Add(line);
            }
        },
    };

    /// <summary>Arcadia started again: a new service over the same cache.</summary>
    private void Restart()
    {
        _service.Dispose();
        _service = NewService();
    }

    private static Game GameOf(string id) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri($"steam://rungameid/{id}"),
    };

    private static readonly LauncherInfo Steam = new("steam", "Steam", "steam", null);

    private async Task ScanAsync(params Game[] games) => await ScanAsync(new LauncherLibrary(Steam, games), _source);

    private async Task ScanAsync(LauncherLibrary library, ILauncherArtwork source)
    {
        _service.Update([new LibraryArtwork(library, source)]);
        await _service.WhenIdleAsync();
    }

    private string File(string name, byte[] content)
    {
        var path = Path.Combine(_root, "launcher", name);
        TestImages.Write(path, content);
        return path;
    }

    private int RequestCount
    {
        get
        {
            lock (_server.Requests)
            {
                return _server.Requests.Count;
            }
        }
    }

    [Fact]
    public async Task A_launchers_files_are_copied_into_the_cache_and_shown()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        _source.Hero = [new("steam-cache", File: File("hero.jpg", TestImages.Jpeg(1920, 620)))];

        await ScanAsync(GameOf("4000001"));

        var shown = _service.Of("steam:4000001")!;
        Assert.Equal(new ImageSize(600, 900), shown.Cover!.Size);
        Assert.StartsWith(CacheFolder, shown.Cover.Path);
        Assert.Equal(new ImageSize(1920, 620), shown.Hero!.Size);
        var cached = Cache.Read("steam:4000001")!;
        Assert.Equal(("cover.jpg", ArtworkSource.Launcher, "steam-cache", false), (cached.Cover!.File, cached.Cover.Source, cached.Cover.Origin, cached.Cover.SetByHand));
        Assert.Equal(0, RequestCount);
    }

    [Fact]
    public async Task Without_local_images_they_are_downloaded_once_and_shown_again_offline_after_a_restart()
    {
        _server.Serve(CoverUrl, TestImages.Jpeg(600, 900));
        _server.Serve(HeroUrl, TestImages.Jpeg(1920, 620));
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl))];
        _source.Hero = [new("steam-server", Download: new Uri(HeroUrl))];

        await ScanAsync(GameOf("4000001"));
        Restart();
        _server.Offline = true;
        await ScanAsync(GameOf("4000001"));

        Assert.Equal(2, RequestCount);
        Assert.All(_server.Requests, r => Assert.Empty(r.Headers));
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
        Assert.NotNull(_service.Of("steam:4000001")!.Hero);
    }

    [Fact]
    public async Task The_cache_shows_before_the_first_scan_ends()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        await ScanAsync(GameOf("4000001"));
        Restart();
        var changed = new List<string>();
        _service.Changed += (_, keys) => changed.AddRange(keys);

        _service.Open();
        await _service.WhenIdleAsync();

        Assert.Equal(["steam:4000001"], changed);
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_image_the_server_doesnt_have_leaves_the_placeholder_and_one_log_line(HttpStatusCode status)
    {
        // Both images, each from two hosts: four failures, one line.
        _server.Fail(CoverUrl, status);
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl)), new("steam-server", Download: new Uri(CoverUrl + "?2"))];
        _source.Hero = [new("steam-server", Download: new Uri(HeroUrl))];

        await ScanAsync(GameOf("4000001"));

        Assert.Null(_service.Of("steam:4000001"));
        Assert.Single(_log, l => l.Contains("steam:4000001", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task An_image_the_server_adds_later_comes_with_the_next_scan(HttpStatusCode status)
    {
        _server.Fail(CoverUrl, status);
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl))];
        await ScanAsync(GameOf("4000001"));

        _server.Serve(CoverUrl, TestImages.Jpeg(600, 900));
        await ScanAsync(GameOf("4000001"));

        Assert.Equal(2, RequestCount);
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Fact]
    public async Task Without_network_nothing_is_shown_and_the_next_scan_asks_again()
    {
        _server.Offline = true;
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl))];
        _source.Hero = [new("steam-server", Download: new Uri(HeroUrl))];

        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        var asked = RequestCount;
        _server.Offline = false;
        _server.Serve(CoverUrl, TestImages.Jpeg(600, 900));
        await ScanAsync(GameOf("4000001"), GameOf("4000002"));

        // The first failed connection ends the run's downloads: no wait per game.
        Assert.Equal(1, asked);
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Fact]
    public async Task A_download_that_doesnt_answer_in_10_seconds_waits_for_the_next_scan()
    {
        _server.Hang(CoverUrl);
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl))];
        _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, [GameOf("4000001")]), _source)]);
        await _server.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));

        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.False(_service.WhenIdleAsync().IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(1));
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(_service.Of("steam:4000001"));
        Assert.Contains(_log, l => l.Contains("antwortet nicht in 10 s", StringComparison.Ordinal));
        _server.Serve(CoverUrl, TestImages.Jpeg(600, 900));
        await ScanAsync(GameOf("4000001"));
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Fact]
    public async Task A_download_that_is_no_image_cut_off_or_too_large_is_dropped()
    {
        var jpeg = TestImages.Jpeg(600, 900);
        var large = new byte[ImageCheck.MaxBytes + 10];
        jpeg.AsSpan(0, 40).CopyTo(large);
        _server.Serve("https://images.example/html.jpg", "<html>Error</html>"u8.ToArray());
        _server.Serve("https://images.example/cut.jpg", jpeg[..^2]);
        _server.Serve("https://images.example/large.jpg", large);
        _source.Cover =
        [
            new("steam-server", Download: new Uri("https://images.example/html.jpg")),
            new("steam-server", Download: new Uri("https://images.example/cut.jpg")),
            new("steam-server", Download: new Uri("https://images.example/large.jpg")),
        ];

        await ScanAsync(GameOf("4000001"));

        Assert.Null(_service.Of("steam:4000001"));
        Assert.Equal(3, RequestCount);
        Assert.False(Directory.Exists(Cache.FolderOf("steam:4000001")) && Directory.EnumerateFiles(Cache.FolderOf("steam:4000001"), "cover.*").Any());
    }

    [Fact]
    public async Task Only_https_is_downloaded()
    {
        _source.Cover = [new("steam-server", Download: new Uri("http://images.example/4000001/library_600x900.jpg"))];

        await ScanAsync(GameOf("4000001"));

        Assert.Equal(0, RequestCount);
    }

    [Fact]
    public async Task The_first_image_that_fits_wins_over_an_earlier_one_that_doesnt()
    {
        // T487-8: a square own cover and a fitting cached one; a 2 : 1 own hero and a fitting one from the server.
        _server.Serve(HeroUrl, TestImages.Jpeg(1920, 620));
        _source.Cover = [new("steam-grid", File: File("own-cover.png", TestImages.Png(90, 90))), new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        _source.Hero = [new("steam-grid", File: File("own-hero.png", TestImages.Png(200, 100))), new("steam-server", Download: new Uri(HeroUrl))];

        await ScanAsync(GameOf("4000001"));

        var cached = Cache.Read("steam:4000001")!;
        Assert.Equal("steam-cache", cached.Cover!.Origin);
        Assert.Equal("steam-server", cached.Hero!.Origin);
    }

    [Fact]
    public async Task Without_a_fitting_image_the_first_image_at_all_is_used()
    {
        // T487-10: every cover is 16 : 9.
        _source.Cover = [new("steam-grid", File: File("wide1.png", TestImages.Png(160, 90))), new("steam-cache", File: File("wide2.jpg", TestImages.Jpeg(1600, 900)))];

        await ScanAsync(GameOf("4000001"));

        Assert.Equal("steam-grid", Cache.Read("steam:4000001")!.Cover!.Origin);
        Assert.Equal(new ImageSize(160, 90), _service.Of("steam:4000001")!.Cover!.Size);
    }

    [Fact]
    public async Task No_image_anywhere_leaves_the_placeholder()
    {
        _source.Cover = [new("steam-cache", File: Path.Combine(_root, "missing.jpg"))];

        await ScanAsync(GameOf("4000001"));

        Assert.Null(_service.Of("steam:4000001"));
        Assert.False(Directory.Exists(Cache.FolderOf("steam:4000001")));
    }

    [Fact]
    public async Task A_broken_launcher_file_is_passed_over()
    {
        _source.Cover = [new("steam-grid", File: File("broken.png", TestImages.Png(60, 90)[..50])), new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];

        await ScanAsync(GameOf("4000001"));

        Assert.Equal("steam-cache", Cache.Read("steam:4000001")!.Cover!.Origin);
        Assert.Single(_log, l => l.Contains("kein lesbares Bild", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_broken_cached_image_counts_as_missing_and_is_loaded_again()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        await ScanAsync(GameOf("4000001"));
        var path = _service.Of("steam:4000001")!.Cover!.Path;
        System.IO.File.WriteAllBytes(path, TestImages.Jpeg(600, 900)[..30]);
        Restart();

        _service.Open();
        await _service.WhenIdleAsync();
        Assert.Null(_service.Of("steam:4000001"));
        await ScanAsync(GameOf("4000001"));

        Assert.Equal(new ImageSize(600, 900), ImageCheck.OfFile(path));
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Fact]
    public async Task An_image_the_view_couldnt_decode_is_loaded_again_at_the_next_scan()
    {
        var source = File("cover.jpg", TestImages.Jpeg(600, 900));
        _source.Cover = [new("steam-cache", File: source)];
        await ScanAsync(GameOf("4000001"));
        var before = _service.Of("steam:4000001")!.Cover!;

        _service.ReportBroken(before);
        await ScanAsync(GameOf("4000001"));

        Assert.True(_service.Of("steam:4000001")!.Cover!.Version > before.Version);
    }

    [Fact]
    public async Task A_cut_off_write_leaves_no_image_and_the_next_scan_writes_it_whole()
    {
        // A crash during the copy leaves only the temporary file; the metadata never named an image.
        var folder = Directory.CreateDirectory(Cache.FolderOf("steam:4000001")).FullName;
        System.IO.File.WriteAllBytes(Path.Combine(folder, "cover.jpg.0badf00d.tmp"), TestImages.Jpeg(600, 900)[..100]);
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];

        await ScanAsync(GameOf("4000001"));

        Assert.Equal(["artwork.json", "cover.jpg"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task An_unreadable_metadata_file_keeps_every_whole_image_as_set_by_hand_and_reloads_a_broken_one()
    {
        var cache = Cache;
        cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Jpeg(300, 450), ".jpg");
        cache.WriteImage("steam:4000001", ArtworkKind.Hero, TestImages.Jpeg(1920, 620)[..40], ".jpg");
        System.IO.File.WriteAllText(Path.Combine(cache.FolderOf("steam:4000001"), ArtworkCache.MetadataFile), "not json");
        _source.Cover = [new("steam-grid", File: File("own.jpg", TestImages.Jpeg(600, 900)))];
        _source.Hero = [new("steam-cache", File: File("hero.jpg", TestImages.Jpeg(1920, 620)))];

        await ScanAsync(GameOf("4000001"));

        var cached = Cache.Read("steam:4000001")!;
        Assert.True(cached.Cover!.SetByHand);
        Assert.Equal(new ImageSize(300, 450), _service.Of("steam:4000001")!.Cover!.Size);
        Assert.Equal(("steam-cache", false), (cached.Hero!.Origin, cached.Hero.SetByHand));
        Assert.Single(_log, l => l.Contains("Metadaten von steam:4000001 nicht lesbar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_run_cut_off_is_taken_up_by_the_next_without_asking_again_for_the_games_done()
    {
        _server.Serve(CoverUrl, TestImages.Jpeg(600, 900));
        _source.Cover = [new("steam-server", Download: new Uri(CoverUrl))];
        await ScanAsync(GameOf("4000001"));
        Restart();
        var second = new TestSource { Cover = [new("steam-server", Download: new Uri("https://images.example/4000002/library_600x900.jpg"))] };
        _server.Serve("https://images.example/4000002/library_600x900.jpg", TestImages.Jpeg(600, 900));

        await ScanAsync(new LauncherLibrary(Steam, [GameOf("4000001"), GameOf("4000002")]), new PerGameSource(new() { ["4000001"] = _source, ["4000002"] = second }));

        Assert.Equal(["4000001", "4000002"], _server.Requests.Select(r => r.RequestUri!.Segments[1].TrimEnd('/')));
        Assert.NotNull(_service.Of("steam:4000002")!.Cover);
    }

    [Fact]
    public async Task A_game_leaving_the_library_loses_its_automatic_images_and_keeps_those_set_by_hand()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        _source.Hero = [new("steam-cache", File: File("hero.jpg", TestImages.Jpeg(1920, 620)))];
        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        var cache = Cache;
        var handSet = cache.Read("steam:4000002")!;
        cache.WriteMetadata(handSet with { Cover = handSet.Cover! with { SetByHand = true } });
        Restart();

        await ScanAsync(GameOf("4000003"));

        Assert.False(Directory.Exists(cache.FolderOf("steam:4000001")));
        Assert.Null(_service.Of("steam:4000001"));
        var kept = cache.Read("steam:4000002")!;
        Assert.True(kept.Cover!.SetByHand);
        Assert.Null(kept.Hero);
    }

    [Fact]
    public async Task A_library_that_failed_to_read_takes_no_image_away()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        await ScanAsync(GameOf("4000001"));

        await ScanAsync(new LauncherLibrary(Steam, [], ScanFailed: true), _source);

        Assert.NotNull(_service.Of("steam:4000001"));
    }

    [Fact]
    public async Task A_changed_launcher_file_is_copied_again_and_one_set_by_hand_stays()
    {
        var own = File("own.jpg", TestImages.Jpeg(600, 900));
        _source.Cover = [new("steam-grid", File: own)];
        await ScanAsync(GameOf("4000001"), GameOf("4000002"));
        var cache = Cache;
        var handSet = cache.Read("steam:4000002")!;
        cache.WriteMetadata(handSet with { Cover = handSet.Cover! with { SetByHand = true } });
        Restart();

        TestImages.Write(own, TestImages.Jpeg(400, 600));
        System.IO.File.SetLastWriteTimeUtc(own, DateTime.UtcNow.AddMinutes(1));
        await ScanAsync(GameOf("4000001"), GameOf("4000002"));

        Assert.Equal(new ImageSize(400, 600), _service.Of("steam:4000001")!.Cover!.Size);
        Assert.Equal(new ImageSize(600, 900), _service.Of("steam:4000002")!.Cover!.Size);
    }

    [Fact]
    public async Task An_unchanged_launcher_file_is_not_copied_again()
    {
        _source.Cover = [new("steam-cache", File: File("cover.jpg", TestImages.Jpeg(600, 900)))];
        await ScanAsync(GameOf("4000001"));
        var written = System.IO.File.GetLastWriteTimeUtc(_service.Of("steam:4000001")!.Cover!.Path);
        Restart();
        var changed = 0;
        _service.Changed += (_, keys) => changed += keys.Count;

        await ScanAsync(GameOf("4000001"));

        Assert.Equal(written, System.IO.File.GetLastWriteTimeUtc(_service.Of("steam:4000001")!.Cover!.Path));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task A_cached_image_whose_launcher_file_went_is_kept_without_a_download()
    {
        var cover = File("cover.jpg", TestImages.Jpeg(600, 900));
        _source.Cover = [new("steam-cache", File: cover), new("steam-server", Download: new Uri(CoverUrl))];
        await ScanAsync(GameOf("4000001"));

        System.IO.File.Delete(cover);
        await ScanAsync(GameOf("4000001"));

        Assert.Equal(0, RequestCount);
        Assert.NotNull(_service.Of("steam:4000001")!.Cover);
    }

    [Fact]
    public async Task Steams_own_images_reach_the_cache_from_steams_folders()
    {
        // End to end with Steam's folders (made-up AppID and account): the account's own cover over Steam's cache, which
        // doesn't count as set by hand; the server's hero.
        var steam = Path.Combine(_root, "Steam");
        TestImages.Write(Path.Combine(steam, "appcache", "librarycache", "4000005", "library_600x900.jpg"), TestImages.Jpeg(600, 900));
        TestImages.Write(Path.Combine(steam, "userdata", "11111111", "config", "grid", "4000005p.png"), TestImages.Png(60, 90));
        _server.Serve("https://shared.steamstatic.com/store_item_assets/steam/apps/4000005/library_hero.jpg", TestImages.Jpeg(1920, 620));

        await ScanAsync(new LauncherLibrary(Steam, [GameOf("4000005")]), new SteamArtwork(() => new SteamInstallation(steam, 11111111)));

        var cached = Cache.Read("steam:4000005")!;
        Assert.Equal(("steam-grid", false, "steam-server"), (cached.Cover!.Origin, cached.Cover.SetByHand, cached.Hero!.Origin));
        Assert.Equal(ArtworkSource.Launcher, cached.Cover.Source);
        Assert.Single(_server.Requests);
    }

    private sealed class TestSource : ILauncherArtwork, IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> Cover { get; set; } = [];

        public IReadOnlyList<ArtworkCandidate> Hero { get; set; } = [];

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;
    }

    private sealed class PerGameSource(Dictionary<string, TestSource> sources) : ILauncherArtwork, IArtworkLookup
    {
        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => sources[game.Id].For(game, kind);
    }
}
