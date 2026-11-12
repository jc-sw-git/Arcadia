using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #488: Epic's images from the <c>keyImages</c> of its catalog cache, as docs/launchers/epic.md describes them, with made-up
/// catalog ids, app names and addresses; downloaded through the artwork run with a fake image server
/// (T488-2 to T488-7, T488-9 to T488-12 and the variants of T488-1 and T488-8).
/// </summary>
public sealed class EpicArtworkTests : IDisposable
{
    private const string TallUrl = "https://cdn1.epicgames.com/offer/ns1/made-up-tall_1200x1600.jpg";
    private const string WideUrl = "https://cdn1.epicgames.com/offer/ns1/made-up-wide_2560x1440.jpg";

    private static readonly LauncherInfo Epic = EpicIntegration.Info;

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-epic-artwork-").FullName;
    private readonly FakeImageServer _server = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private ArtworkService _service;

    public EpicArtworkTests() => _service = NewService();

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private string CatalogPath => Path.Combine(_root, "Data", "Catalog", "catcache.bin");

    private string CacheFolder => Path.Combine(_root, "artwork");

    private ArtworkCache Cache => new(CacheFolder);

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

    private void Restart()
    {
        _service.Dispose();
        _service = NewService();
    }

    /// <summary>A catalog entry with the fields Arcadia reads; <paramref name="images"/> as Epic writes <c>keyImages</c>.</summary>
    private static object Entry(string appName, object? images, string? mainGame = null, string? id = null) => new Dictionary<string, object?>
    {
        ["id"] = id ?? "item" + appName,
        ["namespace"] = "ns" + appName,
        ["title"] = "Made-up " + appName,
        ["categories"] = new[] { new { path = "games" }, new { path = "applications" } },
        ["mainGameItem"] = new { id = mainGame ?? string.Empty, @namespace = string.Empty },
        ["releaseInfo"] = new[] { new { appId = appName, platform = new[] { "Windows" } } },
        ["keyImages"] = images,
    };

    private static object[] Images(params (string Type, string? Url)[] images) =>
        [.. images.Select(image => new { type = image.Type, url = image.Url, width = 1200, height = 1600, md5 = "0" })];

    private void WriteCatalog(params object[] entries) => WriteRaw(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries))));

    private void WriteRaw(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
        File.WriteAllText(CatalogPath, content);
    }

    private IArtworkLookup Look() => new EpicArtwork(CatalogPath).Look();

    private static Game GameOf(string appName, bool installed = false, string? catalogItemId = null) => new()
    {
        Id = appName,
        LauncherId = Epic.Id,
        Title = "Made-up " + appName,
        IsInstalled = installed,
        Launch = new LaunchTarget.ProtocolUri($"com.epicgames.launcher://apps/ns{appName}%3A{catalogItemId ?? "item" + appName}%3A{appName}?action=launch&silent=true"),
    };

    private async Task ScanAsync(params Game[] games)
    {
        _service.Update([new LibraryArtwork(new LauncherLibrary(Epic, games), new EpicArtwork(CatalogPath))]);
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>The lines the Epic source writes through <see cref="Trace"/>, which the App sends to arcadia.log.</summary>
    private static List<string> Traced(Action action)
    {
        var lines = new List<string>();
        using var listener = new LineListener(lines);
        Trace.Listeners.Add(listener);
        try
        {
            action();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        return lines;
    }

    [Fact]
    [Trait("TestCase", "T488-1")]
    public void The_tall_image_is_the_cover_and_the_wide_one_the_background()
    {
        WriteCatalog(Entry("aa01", Images(("Thumbnail", "https://cdn1.epicgames.com/thumb.jpg"), ("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));

        var lookup = Look();

        var cover = Assert.Single(lookup.For(GameOf("aa01"), ArtworkKind.Cover));
        var hero = Assert.Single(lookup.For(GameOf("aa01"), ArtworkKind.Hero));
        Assert.Equal((EpicArtwork.Origin, TallUrl), (cover.Origin, cover.Download!.AbsoluteUri));
        Assert.Equal((EpicArtwork.Origin, WideUrl), (hero.Origin, hero.Download!.AbsoluteUri));
        Assert.Null(cover.File);
    }

    [Fact]
    [Trait("TestCase", "T488-2")]
    public void An_entry_with_one_kind_offers_only_that_one_and_one_with_none_offers_nothing_without_a_log_line()
    {
        WriteCatalog(
            Entry("aa01", Images(("DieselGameBoxTall", TallUrl))),
            Entry("aa02", Images(("DieselGameBox", WideUrl))),
            Entry("aa03", Images()),
            Entry("aa04", images: null));

        IArtworkLookup? lookup = null;
        var lines = Traced(() => lookup = Look());

        Assert.Single(lookup!.For(GameOf("aa01"), ArtworkKind.Cover));
        Assert.Empty(lookup.For(GameOf("aa01"), ArtworkKind.Hero));
        Assert.Empty(lookup.For(GameOf("aa02"), ArtworkKind.Cover));
        Assert.Single(lookup.For(GameOf("aa02"), ArtworkKind.Hero));
        Assert.All(new[] { "aa03", "aa04" }, app =>
        {
            Assert.Empty(lookup.For(GameOf(app), ArtworkKind.Cover));
            Assert.Empty(lookup.For(GameOf(app), ArtworkKind.Hero));
        });
        Assert.Empty(lines);
    }

    [Fact]
    [Trait("TestCase", "T488-3")]
    public void Other_types_are_ignored_the_first_usable_of_a_type_wins_and_types_match_exactly()
    {
        WriteCatalog(Entry("aa01", Images(
            ("OfferImageTall", "https://cdn1.epicgames.com/offer-tall.jpg"),
            ("DieselStoreFrontWide", "https://cdn1.epicgames.com/storefront.jpg"),
            ("dieselgameboxtall", "https://cdn1.epicgames.com/lower-case.jpg"),
            ("DieselGameBoxTall", "http://cdn1.epicgames.com/not-https.jpg"),
            ("DieselGameBoxTall", TallUrl),
            ("DieselGameBoxTall", "https://cdn1.epicgames.com/second.jpg"),
            ("DieselGameBoxLogo", "https://cdn1.epicgames.com/logo.png"))));

        var lookup = Look();

        Assert.Equal(TallUrl, Assert.Single(lookup.For(GameOf("aa01"), ArtworkKind.Cover)).Download!.AbsoluteUri);
        Assert.Empty(lookup.For(GameOf("aa01"), ArtworkKind.Hero));
    }

    [Theory]
    [Trait("TestCase", "T488-4")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/offer/relative.jpg")]
    [InlineData("http://cdn1.epicgames.com/a.jpg")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("https://images.example/a.jpg")]
    [InlineData("https://epicgames.com.images.example/a.jpg")]
    [InlineData("https://notepicgames.com/a.jpg")]
    [InlineData("https://192.168.0.1/a.jpg")]
    [InlineData("https://user:secret@cdn1.epicgames.com/a.jpg")]
    [InlineData("https://cdn1.epicgames.com:8443/a.jpg")]
    public void An_address_that_isnt_https_on_epics_servers_counts_as_missing_with_one_log_line(string? url)
    {
        WriteCatalog(
            Entry("aa01", Images(("DieselGameBoxTall", url), ("DieselGameBox", WideUrl))),
            Entry("aa02", Images(("DieselGameBoxTall", TallUrl))));

        IArtworkLookup? lookup = null;
        var lines = Traced(() => lookup = Look());

        Assert.Empty(lookup!.For(GameOf("aa01"), ArtworkKind.Cover));
        Assert.Single(lookup.For(GameOf("aa01"), ArtworkKind.Hero));
        Assert.Single(lookup.For(GameOf("aa02"), ArtworkKind.Cover));
        Assert.Single(lines, line => line.Contains("itemaa01", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("secret", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("TestCase", "T488-4")]
    [InlineData("https://cdn1.epicgames.com/a.jpg")]
    [InlineData("https://cdn2.unrealengine.com/a.jpg")]
    [InlineData("https://EPICGAMES.COM/a.jpg")]
    public void Epics_image_hosts_are_accepted(string url) => Assert.NotNull(EpicArtwork.ImageAddress(url));

    [Fact]
    [Trait("TestCase", "T488-4")]
    public void Malformed_entries_are_passed_over_and_the_rest_is_read()
    {
        var raw = $$"""
            [
              42,
              { "id": "itembb01", "releaseInfo": [{ "appId": "bb01" }], "keyImages": "not an array" },
              { "id": "itembb02", "releaseInfo": [{ "appId": "bb02" }], "keyImages": [7, { "url": "{{TallUrl}}" }, { "type": "DieselGameBoxTall" }, { "type": 3, "url": "{{TallUrl}}" }, { "type": "DieselGameBoxTall", "url": 5 }] },
              { "id": "itembb03", "releaseInfo": "none", "keyImages": [{ "type": "DieselGameBoxTall", "url": "{{TallUrl}}" }] },
              { "id": "itembb04", "releaseInfo": [{ "appId": "bb04" }], "keyImages": [{ "type": "DieselGameBoxTall", "url": "{{TallUrl}}" }] }
            ]
            """;
        WriteRaw(Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));

        var lookup = Look();

        Assert.Empty(lookup.For(GameOf("bb01"), ArtworkKind.Cover));
        Assert.Empty(lookup.For(GameOf("bb02"), ArtworkKind.Cover));
        // Without an app the entry is still found by its item id, which an installed game's launch address names.
        Assert.Single(lookup.For(GameOf("bb03"), ArtworkKind.Cover));
        Assert.Single(lookup.For(GameOf("bb04"), ArtworkKind.Cover));
    }

    [Fact]
    [Trait("TestCase", "T488-5")]
    public void A_dlc_lends_its_game_no_images_and_a_game_without_an_entry_has_none()
    {
        const string dlcUrl = "https://cdn1.epicgames.com/offer/dlc-tall.jpg";
        WriteCatalog(
            Entry("cc02", Images(("DieselGameBoxTall", dlcUrl)), mainGame: "itemcc02", id: "dlc-of-cc02"),
            Entry("cc02", Images(("DieselGameBoxTall", TallUrl))),
            Entry("cc03dlc", Images(("DieselGameBoxTall", dlcUrl)), mainGame: "itemcc04"),
            Entry("cc04", images: null),
            Entry("cc06", Images(("DieselGameBoxTall", dlcUrl)), mainGame: "itemcc06", id: "edition-of-cc06"),
            Entry("cc06", images: null));

        var lookup = Look();

        // The first DLC names the game's app as its own release (an edition) and comes first: the base game's image still wins.
        Assert.Equal(TallUrl, Assert.Single(lookup.For(GameOf("cc02"), ArtworkKind.Cover)).Download!.AbsoluteUri);
        Assert.Empty(lookup.For(GameOf("cc04"), ArtworkKind.Cover));
        // A base game without images keeps its placeholder rather than its edition's image.
        Assert.Empty(lookup.For(GameOf("cc06"), ArtworkKind.Cover));
        Assert.Empty(lookup.For(GameOf("cc05", installed: true), ArtworkKind.Cover));
    }

    [Fact]
    [Trait("TestCase", "T488-5")]
    public void An_installed_game_whose_app_the_catalog_files_under_another_release_is_found_by_its_item_id()
    {
        WriteCatalog(Entry("dd01Mac", Images(("DieselGameBoxTall", TallUrl)), id: "itemdd01"));

        Assert.Single(Look().For(GameOf("dd01", installed: true, catalogItemId: "itemdd01"), ArtworkKind.Cover));
    }

    [Fact]
    [Trait("TestCase", "T488-6")]
    public void An_unreal_engine_entry_shown_as_a_tool_uses_its_images_too()
    {
        WriteCatalog(Entry("UE_5.4", Images(("DieselGameBoxTall", TallUrl))), Entry("UE_5.3", images: null));
        var engine = GameOf("UE_5.4") with { Type = GameType.Tool };

        var lookup = Look();

        Assert.Single(lookup.For(engine, ArtworkKind.Cover));
        Assert.Empty(lookup.For(GameOf("UE_5.3") with { Type = GameType.Tool }, ArtworkKind.Cover));
    }

    [Fact]
    public void A_game_of_another_launcher_gets_nothing()
    {
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl))));

        Assert.Empty(Look().For(GameOf("aa01") with { LauncherId = "steam" }, ArtworkKind.Cover));
    }

    [Theory]
    [Trait("TestCase", "T488-7")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("bm90IGpzb24=")] // "not json"
    [InlineData("e30=")] // "{}"
    public void A_missing_or_unreadable_catalog_offers_nothing_with_one_log_line(string? content)
    {
        if (content is not null)
        {
            WriteRaw(content);
        }

        IArtworkLookup? lookup = null;
        var lines = Traced(() => lookup = Look());

        Assert.Empty(lookup!.For(GameOf("aa01"), ArtworkKind.Cover));
        Assert.Single(lines);
    }

    [Fact]
    [Trait("TestCase", "T488-1")]
    public async Task The_cover_is_epics_tall_image_and_the_background_its_wide_one_for_installed_and_owned_games()
    {
        WriteCatalog(
            Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))),
            Entry("aa02", Images(("DieselGameBoxTall", TallUrl + "?2"), ("DieselGameBox", WideUrl + "?2"))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        _server.Serve(WideUrl, TestImages.Jpeg(2560, 1440));
        _server.Serve(TallUrl + "?2", TestImages.Jpeg(1200, 1600));
        _server.Serve(WideUrl + "?2", TestImages.Jpeg(2560, 1440));

        await ScanAsync(GameOf("aa01", installed: true), GameOf("aa02"));

        foreach (var key in new[] { "epic:aa01", "epic:aa02" })
        {
            var shown = _service.Of(key)!;
            Assert.Equal(new ImageSize(1200, 1600), shown.Cover!.Size);
            Assert.Equal(new ImageSize(2560, 1440), shown.Hero!.Size);
            var cached = Cache.Read(key)!;
            Assert.Equal((ArtworkSource.Launcher, EpicArtwork.Origin, false), (cached.Cover!.Source, cached.Cover.Origin, cached.Cover.SetByHand));
            Assert.Equal((ArtworkSource.Launcher, EpicArtwork.Origin, false), (cached.Hero!.Source, cached.Hero.Origin, cached.Hero.SetByHand));
        }

        Assert.True(ArtworkFormat.Fits(new ImageSize(1200, 1600), ArtworkKind.Cover, ArtworkFormat.NormalTolerance));
        Assert.False(ArtworkFormat.Fits(new ImageSize(2560, 1440), ArtworkKind.Hero, ArtworkFormat.NormalTolerance));
    }

    [Fact]
    [Trait("TestCase", "T488-6")]
    public async Task Two_entries_naming_the_same_image_both_show_it_after_one_scan()
    {
        // Unreal Engine's versions and a game's editions share their catalog art.
        WriteCatalog(Entry("UE_5.3", Images(("DieselGameBoxTall", TallUrl))), Entry("UE_5.4", Images(("DieselGameBoxTall", TallUrl))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));

        await ScanAsync(GameOf("UE_5.3") with { Type = GameType.Tool }, GameOf("UE_5.4") with { Type = GameType.Tool });

        Assert.NotNull(_service.Of("epic:UE_5.3")?.Cover);
        Assert.NotNull(_service.Of("epic:UE_5.4")?.Cover);
    }

    [Fact]
    [Trait("TestCase", "T488-8")]
    public async Task Images_are_downloaded_once_without_account_data_and_shown_offline_after_a_restart()
    {
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        _server.Serve(WideUrl, TestImages.Jpeg(2560, 1440));

        await ScanAsync(GameOf("aa01"));
        await ScanAsync(GameOf("aa01"));
        Restart();
        _server.Offline = true;
        await ScanAsync(GameOf("aa01"));

        Assert.Equal(2, RequestCount);
        Assert.All(_server.Requests, request => Assert.Empty(request.Headers));
        Assert.NotNull(_service.Of("epic:aa01")!.Cover);
        Assert.NotNull(_service.Of("epic:aa01")!.Hero);
    }

    [Theory]
    [Trait("TestCase", "T488-9")]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_failed_download_leaves_the_placeholder_and_the_next_scan_asks_again(HttpStatusCode status)
    {
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));
        _server.Fail(TallUrl, status);
        _server.Fail(WideUrl, status);

        await ScanAsync(GameOf("aa01"));
        Assert.Null(_service.Of("epic:aa01"));
        Assert.Single(_log, line => line.Contains("epic:aa01", StringComparison.Ordinal));

        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        await ScanAsync(GameOf("aa01"));

        Assert.NotNull(_service.Of("epic:aa01")!.Cover);
    }

    [Fact]
    [Trait("TestCase", "T488-9")]
    public async Task Without_network_or_with_a_download_that_hangs_the_next_scan_brings_the_image()
    {
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl))));
        _server.Offline = true;
        await ScanAsync(GameOf("aa01"));
        Assert.Null(_service.Of("epic:aa01"));

        _server.Offline = false;
        _server.Hang(TallUrl);
        _service.Update([new LibraryArtwork(new LauncherLibrary(Epic, [GameOf("aa01")]), new EpicArtwork(CatalogPath))]);
        await _server.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _time.Advance(ArtworkService.DownloadTimeout);
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(_service.Of("epic:aa01"));
        Assert.Contains(_log, line => line.Contains("antwortet nicht in 10 s", StringComparison.Ordinal));

        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        await ScanAsync(GameOf("aa01"));
        Assert.NotNull(_service.Of("epic:aa01")!.Cover);
    }

    [Fact]
    [Trait("TestCase", "T488-9")]
    public async Task An_answer_that_is_no_image_or_too_large_is_dropped()
    {
        var large = new byte[ImageCheck.MaxBytes + 10];
        TestImages.Jpeg(1200, 1600).AsSpan(0, 40).CopyTo(large);
        WriteCatalog(
            Entry("aa01", Images(("DieselGameBoxTall", TallUrl))),
            Entry("aa02", Images(("DieselGameBoxTall", TallUrl + "?large"))),
            Entry("aa03", Images(("DieselGameBoxTall", TallUrl + "?pixels"))));
        _server.Serve(TallUrl, "<html>Error</html>"u8.ToArray());
        _server.Serve(TallUrl + "?large", large);
        _server.Serve(TallUrl + "?pixels", TestImages.Jpeg(6000, 8000));

        await ScanAsync(GameOf("aa01"), GameOf("aa02"), GameOf("aa03"));

        Assert.All(new[] { "epic:aa01", "epic:aa02", "epic:aa03" }, key => Assert.Null(_service.Of(key)));
    }

    [Theory]
    [Trait("TestCase", "T488-10")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("con")]
    [InlineData("..\\..\\y")]
    public async Task No_catalog_value_leads_outside_the_cache(string appName)
    {
        WriteCatalog(Entry(appName, Images(("DieselGameBoxTall", TallUrl))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));

        await ScanAsync(GameOf(appName));

        var shown = _service.Of("epic:" + appName)!.Cover!;
        Assert.StartsWith(Path.GetFullPath(CacheFolder) + Path.DirectorySeparatorChar, Path.GetFullPath(shown.Path));
        Assert.Matches(@"^cover\.[a-z]+$", Path.GetFileName(shown.Path));
        Assert.All(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories), file =>
            Assert.True(file.StartsWith(CacheFolder, StringComparison.Ordinal) || file == CatalogPath, file));
    }

    [Fact]
    [Trait("TestCase", "T488-11")]
    public async Task New_art_in_the_catalog_is_downloaded_at_the_next_scan_and_one_set_by_hand_stays()
    {
        const string newTall = "https://cdn1.epicgames.com/offer/ns1/new-tall_1200x1600.jpg";
        const string newWide = "https://cdn1.epicgames.com/offer/ns1/new-wide_2560x1440.jpg";
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        _server.Serve(WideUrl, TestImages.Jpeg(2560, 1440));
        _server.Serve(newTall, TestImages.Jpeg(900, 1200));
        _server.Serve(newWide, TestImages.Jpeg(1920, 1080));
        await ScanAsync(GameOf("aa01"));

        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", newTall), ("DieselGameBox", newWide))));
        await ScanAsync(GameOf("aa01"));

        Assert.Equal(new ImageSize(900, 1200), _service.Of("epic:aa01")!.Cover!.Size);
        Assert.Equal(new ImageSize(1920, 1080), _service.Of("epic:aa01")!.Hero!.Size);
        Assert.Equal(4, RequestCount);

        // The same with a cover the player set: the newer art comes for the background only.
        var cache = Cache;
        var held = cache.Read("epic:aa01")!;
        cache.WriteMetadata(held with { Cover = held.Cover! with { SetByHand = true } });
        Restart();
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));
        await ScanAsync(GameOf("aa01"));

        Assert.Equal(new ImageSize(900, 1200), _service.Of("epic:aa01")!.Cover!.Size);
        Assert.Equal(new ImageSize(2560, 1440), _service.Of("epic:aa01")!.Hero!.Size);
    }

    [Fact]
    [Trait("TestCase", "T488-12")]
    public async Task Installing_or_uninstalling_a_game_keeps_its_images_without_a_new_download()
    {
        WriteCatalog(Entry("aa01", Images(("DieselGameBoxTall", TallUrl), ("DieselGameBox", WideUrl))));
        _server.Serve(TallUrl, TestImages.Jpeg(1200, 1600));
        _server.Serve(WideUrl, TestImages.Jpeg(2560, 1440));
        await ScanAsync(GameOf("aa01"));
        var folder = Cache.FolderOf("epic:aa01");

        await ScanAsync(GameOf("aa01", installed: true));
        await ScanAsync(GameOf("aa01"));

        Assert.Equal(2, RequestCount);
        Assert.Single(Directory.GetFiles(folder, "cover.*"));
        Assert.Single(Directory.GetFiles(folder, "hero.*"));
        Assert.NotNull(_service.Of("epic:aa01")!.Cover);
    }

    private sealed class LineListener(List<string> lines) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            // Only the Epic source's lines: tests of other classes trace at the same time.
            if (message?.StartsWith("Epic-Katalogbilder:", StringComparison.Ordinal) == true)
            {
                lock (lines)
                {
                    lines.Add(message);
                }
            }
        }
    }

    [Theory]
    [Trait("TestCase", "T491-1")]
    [InlineData("Sugar")]
    [InlineData("9d2d0eb64d5c44529cece33fe2a46482")]
    [InlineData("Made-up_Game-1")]
    public void An_app_name_as_epic_writes_it_is_the_games_id_on_steamgriddbs_egs(string appName) =>
        Assert.Equal(new ArtworkStoreId(ArtworkStoreId.Epic, appName), new EpicArtwork(Path.Combine(Path.GetTempPath(), "none.bin")).Look().StoreIdOf(GameOf(appName)));

    [Theory]
    [Trait("TestCase", "T491-2")]
    [InlineData("")]
    [InlineData("../grids")]
    [InlineData("a/b")]
    [InlineData("a?b=1")]
    [InlineData("with space")]
    [InlineData("Ümlaut")]
    public void Anything_else_is_no_id_and_the_game_is_found_by_name(string appName) =>
        Assert.Null(new EpicArtwork(Path.Combine(Path.GetTempPath(), "none.bin")).Look().StoreIdOf(GameOf(appName)));

    [Fact]
    [Trait("TestCase", "T491-2")]
    public void An_id_over_the_length_limit_or_of_another_launcher_is_no_id()
    {
        var look = new EpicArtwork(Path.Combine(Path.GetTempPath(), "none.bin")).Look();
        Assert.Null(look.StoreIdOf(GameOf(new string('a', 101))));
        Assert.NotNull(look.StoreIdOf(GameOf(new string('a', 100))));
        Assert.Null(look.StoreIdOf(GameOf("Sugar") with { LauncherId = "steam" }));
    }
}
