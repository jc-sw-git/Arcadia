using System.Globalization;
using System.Net;
using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>
/// #493: the background run that reads the launchers' local data and asks their store, with a fake store and fake time:
/// fetching after scans, throttling, failures, the cache, the start, closing, the progress and "Alle Metadaten neu laden"
/// (T493-16 to T493-19, T493-21 to T493-23, T493-25 to T493-27, T493-29), the store asked in the UI language (T557-4).
/// </summary>
public sealed class MetadataServiceTests : IDisposable
{
    private static readonly LauncherInfo Launcher = new("madeup", "Made-up Launcher", "madeup");

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-metadata-run-").FullName;
    private readonly FakeTimeProvider _time = new();
    private readonly FakeStoreServer _server;
    private readonly FakeMetadataSource _source = new();
    private readonly List<string> _log = [];
    private MetadataService _service;
    private CultureInfo _language = CultureInfo.GetCultureInfo("de-DE");

    public MetadataServiceTests()
    {
        _server = new FakeStoreServer(_time);
        _service = NewService();
        _service.Open();
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private string CacheFolder => Path.Combine(_root, "metadata");

    private IReadOnlyList<string> Log
    {
        get
        {
            lock (_log)
            {
                return [.. _log];
            }
        }
    }

    private MetadataService NewService() => new(new MetadataCache(CacheFolder), new HttpClient(_server), _time)
    {
        // A German PC (#557) unless the test changes the language, whatever the machine running the tests speaks.
        Language = _language,
        FirstAgeRatingBoard = AgeRatingBoard.Usk,
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
        _service.Open();
    }

    private static Game GameOf(string id) => new()
    {
        Id = id,
        LauncherId = Launcher.Id,
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri("madeup://run/" + id),
    };

    private static string Key(string id) => Game.KeyOf(Launcher.Id, id);

    private void Scan(params string[] ids) => _service.Update([new LibraryMetadata(new LauncherLibrary(Launcher, [.. ids.Select(GameOf)]), _source)]);

    /// <summary>Lets fake time pass in steps until the run is idle.</summary>
    private async Task RunToIdleAsync(TimeSpan? step = null)
    {
        for (var i = 0; i < 2000; i++)
        {
            var idle = _service.WhenIdleAsync();
            if (idle.IsCompleted)
            {
                await idle;
                return;
            }

            await Task.Delay(1);
            _time.Advance(step ?? TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException("The metadata run didn't end.");
    }

    /// <summary>Lets fake time pass until the store holds a request open; the request in flight then waits for real.</summary>
    private async Task WaitForHangAsync()
    {
        for (var i = 0; i < 2000 && !_server.Hanging.Task.IsCompleted; i++)
        {
            await Task.Delay(1);
            if (!_server.Hanging.Task.IsCompleted)
            {
                _time.Advance(TimeSpan.FromMilliseconds(500));
            }
        }

        await _server.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private async Task WaitForRequestsAsync(int count)
    {
        for (var i = 0; i < 2000 && _server.RequestCount < count; i++)
        {
            await Task.Delay(1);
        }

        Assert.True(_server.RequestCount >= count, $"{_server.RequestCount} requests, expected {count}");
    }

    private void ServeAll(params string[] ids)
    {
        foreach (var id in ids)
        {
            _server.Serve(id, "Beschreibung von " + id + ", die nicht zu lang ist und dir gefällt.");
        }
    }

    [Fact]
    [Trait("TestCase", "T493-18")]
    public async Task After_a_scan_only_games_without_store_data_are_asked_and_a_cached_game_is_never_asked_again()
    {
        ServeAll("1", "2");
        Scan("1");
        await RunToIdleAsync();
        Assert.Equal(["1"], _server.AskedIds);

        Scan("1", "2");
        await RunToIdleAsync();
        Scan("1", "2");
        await RunToIdleAsync();

        Assert.Equal(["1", "2"], _server.AskedIds);
        Assert.StartsWith("Beschreibung von 2", _service.Of(Key("2")).Description!.Value);
    }

    [Fact]
    [Trait("TestCase", "T493-18")]
    public async Task A_scan_while_a_fetch_runs_asks_no_game_twice()
    {
        ServeAll("1", "2", "3", "4");
        Scan("1", "2", "3", "4");
        await WaitForRequestsAsync(1);
        Scan("1", "2", "3", "4");
        await RunToIdleAsync();

        Assert.Equal(["1", "2", "3", "4"], _server.AskedIds);
    }

    [Fact]
    [Trait("TestCase", "T493-18")]
    public async Task Local_data_shows_at_once_before_any_store_answer()
    {
        _source.Local["1"] = new GameMetadata { Genres = [Genre.Action], Developers = ["Made-up Studio"], ReleaseDate = new DateOnly(2021, 5, 1) };
        _server.Hang("1");
        Scan("1");
        await WaitForHangAsync();

        var shown = _service.Of(Key("1"));
        Assert.Equal([Genre.Action], shown.Genres!.Value);
        Assert.Equal("Made-up Launcher", shown.Developers!.Source);
        Assert.Null(shown.Description);
    }

    [Fact]
    [Trait("TestCase", "T493-16")]
    public async Task Requests_keep_the_stores_gap_and_http_429_waits_and_asks_the_same_game_again()
    {
        ServeAll("1", "2", "3");
        _server.ThrottleOnce("2", TimeSpan.FromSeconds(30));
        _server.ThrottleOnce("3");
        Scan("1", "2", "3");
        await RunToIdleAsync();

        List<(string Id, DateTimeOffset At)> asked;
        lock (_server.Requests)
        {
            asked = [.. _server.Requests.Select(r => (FakeStoreServer.IdOf(r.Request.RequestUri!), r.At))];
        }

        Assert.Equal(["1", "2", "2", "3", "3"], asked.Select(a => a.Id));
        for (var i = 1; i < asked.Count; i++)
        {
            Assert.True(asked[i].At - asked[i - 1].At >= TimeSpan.FromSeconds(1), $"request {i} came too early");
        }

        Assert.True(asked[2].At - asked[1].At >= TimeSpan.FromSeconds(30));
        Assert.True(asked[4].At - asked[3].At >= MetadataService.ThrottledWait);
        Assert.NotNull(_service.Of(Key("3")).Description);
        Assert.Contains(Log, line => line.Contains("gedrosselt"));
    }

    [Fact]
    [Trait("TestCase", "T493-17")]
    public async Task A_timeout_or_server_error_skips_the_game_silently_until_the_next_scan()
    {
        ServeAll("3");
        _server.Hang("1");
        _server.Fail("2", HttpStatusCode.ServiceUnavailable);
        Scan("1", "2", "3");
        await WaitForHangAsync();
        _time.Advance(MetadataService.RequestTimeout);
        await RunToIdleAsync();

        Assert.Equal(["1", "2", "3"], _server.AskedIds);
        Assert.NotNull(_service.Of(Key("3")).Description);
        Assert.Equal(2, _service.Progress.Missing);
        Assert.Single(Log, line => line.Contains("übersprungen"));

        ServeAll("1", "2");
        Scan("1", "2", "3");
        await RunToIdleAsync();
        Assert.Equal(["1", "2", "3", "1", "2"], _server.AskedIds);
        Assert.Equal(0, _service.Progress.Missing);
    }

    [Fact]
    [Trait("TestCase", "T493-17")]
    public async Task No_network_stops_the_run_with_one_log_line_and_the_next_scan_asks_again()
    {
        _server.Offline = true;
        Scan("1", "2", "3");
        await RunToIdleAsync();

        Assert.Equal(["1"], _server.AskedIds);
        Assert.Single(Log, line => line.Contains("keine Verbindung"));
        Assert.Equal(3, _service.Progress.Missing);

        _server.Offline = false;
        ServeAll("1", "2", "3");
        Scan("1", "2", "3");
        await RunToIdleAsync();
        Assert.Equal(0, _service.Progress.Missing);
    }

    [Fact]
    [Trait("TestCase", "T493-17")]
    public async Task A_store_that_has_no_page_for_a_game_is_an_answer_and_an_unusable_answer_is_asked_again()
    {
        _server.Serve("1", "-");
        _server.Serve("2", "broken");
        Scan("1", "2");
        await RunToIdleAsync();
        Scan("1", "2");
        await RunToIdleAsync();

        Assert.Equal(["1", "2", "2"], _server.AskedIds);
        Assert.True(_service.Of(Key("1")).IsEmpty);
        Assert.Equal(1, _service.Progress.Missing);
    }

    [Fact]
    [Trait("TestCase", "T493-19")]
    public async Task Answers_are_cached_per_provider_through_a_temporary_file_and_read_back_checked()
    {
        ServeAll("1");
        Scan("1");
        await RunToIdleAsync();

        var file = new MetadataCache(CacheFolder).PathOf(Key("1"));
        Assert.True(File.Exists(file));
        Assert.Equal(Path.Combine(CacheFolder, "madeup"), Path.GetDirectoryName(file));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.tmp"));

        // Written by another program: markup and a rating USK doesn't have are checked again when read.
        File.WriteAllText(file, File.ReadAllText(file).Replace("Beschreibung von 1", "<script>x()</script><b>Beschreibung</b> von 1").Replace("\"12\"", "\"17\""));
        Restart();
        await RunToIdleAsync();

        var shown = _service.Of(Key("1"));
        Assert.StartsWith("Beschreibung von 1", shown.Description!.Value);
        Assert.Empty(shown.AgeRatings);
    }

    [Theory]
    [Trait("TestCase", "T493-19")]
    [InlineData("Drücke &lt;E&gt; zum Bauen, wenn 3 &lt; 5 Spieler da sind.", "Drücke <E> zum Bauen, wenn 3 < 5 Spieler da sind.")]
    [InlineData("Drücke &amp;lt;E&amp;gt; zum Bauen, wenn 3 &amp;lt; 5 Spieler da sind.", "Drücke &lt;E&gt; zum Bauen, wenn 3 &lt; 5 Spieler da sind.")]
    [InlineData("Tom &amp; Jerry &amp;amp; Co. • „Zitat“ – <b>fett</b>", "Tom & Jerry &amp; Co. • „Zitat“ – fett")]
    public async Task A_text_read_back_from_the_cache_shows_as_it_did_before_the_restart(string answer, string shown)
    {
        // The store's HTML is read once; the cache must not let its decoded "<" or "&" be read as markup a second time.
        _source.Local["1"] = new GameMetadata { Developers = ["Kraut &amp; Rüben"], Tags = ["3 &lt; 5"] };
        _server.Serve("1", answer);
        Scan("1");
        await RunToIdleAsync();
        var before = _service.Of(Key("1"));
        Assert.Equal(shown, before.Description!.Value);

        Restart();
        await RunToIdleAsync();

        var after = _service.Of(Key("1"));
        Assert.Equal(shown, after.Description!.Value);
        Assert.Equal(["Kraut & Rüben"], after.Developers!.Value);
        Assert.Equal(["3 < 5"], after.Tags!.Value);
    }

    [Fact]
    [Trait("TestCase", "T493-19")]
    public async Task A_damaged_cache_file_is_discarded_with_one_log_line_and_fetched_again()
    {
        ServeAll("1", "2");
        Scan("1", "2");
        await RunToIdleAsync();
        var cache = new MetadataCache(CacheFolder);
        File.WriteAllText(cache.PathOf(Key("1")), "{ not json");
        File.WriteAllText(cache.PathOf(Key("2")), """{"game":"madeup:9","source":"X"}""");

        Restart();
        await RunToIdleAsync();
        Assert.False(File.Exists(cache.PathOf(Key("1"))));
        Assert.Single(Log, line => line.Contains("2 beschädigte"));

        Scan("1", "2");
        await RunToIdleAsync();
        Assert.Equal(["1", "2", "1", "2"], _server.AskedIds);
    }

    [Fact]
    [Trait("TestCase", "T493-21")]
    public async Task Right_after_the_start_cached_metadata_shows_and_nothing_is_fetched_before_the_first_scan()
    {
        ServeAll("1");
        Scan("1");
        await RunToIdleAsync();

        Restart();
        await RunToIdleAsync();

        Assert.StartsWith("Beschreibung von 1", _service.Of(Key("1")).Description!.Value);
        Assert.Equal(1, _server.RequestCount);
        Assert.Equal(MetadataProgress.NotYet, _service.Progress with { Running = false });
    }

    [Fact]
    [Trait("TestCase", "T493-22")]
    public async Task Closing_during_a_fetch_waits_for_no_request_and_the_next_start_continues_with_the_rest()
    {
        ServeAll("1", "3");
        _server.Hang("2");
        Scan("1", "2", "3");
        await WaitForHangAsync();

        _service.Dispose();
        await _service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        ServeAll("2");
        _service = NewService();
        _service.Open();
        Scan("1", "2", "3");
        await RunToIdleAsync();

        Assert.Equal(["1", "2", "2", "3"], _server.AskedIds);
        Assert.NotNull(_service.Of(Key("1")).Description);
    }

    [Fact]
    [Trait("TestCase", "T493-23")]
    public async Task A_game_leaving_the_library_and_coming_back_shows_its_cached_data_without_a_request()
    {
        ServeAll("1", "2");
        Scan("1", "2");
        await RunToIdleAsync();
        Scan("2");
        await RunToIdleAsync();
        Scan("1", "2");
        await RunToIdleAsync();

        Assert.Equal(2, _server.RequestCount);
        Assert.NotNull(_service.Of(Key("1")).Description);
    }

    [Fact]
    [Trait("TestCase", "T493-25")]
    public async Task The_progress_counts_down_and_its_estimate_follows_the_limit_and_the_rate_seen()
    {
        ServeAll("1", "2", "3", "4", "5", "6");
        _server.Fail("6", HttpStatusCode.InternalServerError);
        var reports = new List<MetadataProgress>();
        _service.ProgressChanged += (_, _) =>
        {
            lock (reports)
            {
                reports.Add(_service.Progress);
            }
        };

        Scan("1", "2", "3", "4", "5", "6");
        await RunToIdleAsync(TimeSpan.FromSeconds(4));

        List<MetadataProgress> seen;
        lock (reports)
        {
            seen = [.. reports];
        }

        var first = seen.First(p => p.Running);
        Assert.Equal(6, first.Total);
        Assert.Equal(6, first.Missing);
        Assert.Equal(TimeSpan.FromSeconds(6), first.Remaining);
        Assert.Contains(seen, p => p is { Running: true, Missing: 3 });

        // Each request took 4 s of fake time: after three, the estimate follows that rate, not the store's 1 s.
        var later = seen.Last(p => p is { Running: true, Missing: 2 });
        Assert.True(later.Remaining >= TimeSpan.FromSeconds(6), $"estimate {later.Remaining}");
        Assert.Equal(new MetadataProgress(1, 6, false, false, null), _service.Progress);
    }

    [Fact]
    [Trait("TestCase", "T493-26")]
    public async Task Reloading_asks_every_game_again_and_keeps_the_old_data_until_it_is_replaced()
    {
        ServeAll("1", "2");
        Scan("1", "2");
        await RunToIdleAsync();

        _server.Serve("1", "Neue Beschreibung, die nicht zu lang ist und dir gefällt.");
        _server.Hang("2");
        Assert.True(_service.ReloadAll());
        await WaitForHangAsync();

        Assert.StartsWith("Neue Beschreibung", _service.Of(Key("1")).Description!.Value);
        Assert.StartsWith("Beschreibung von 2", _service.Of(Key("2")).Description!.Value);
        Assert.True(_service.Progress.Reload);
        Assert.False(_service.ReloadAll());

        _time.Advance(MetadataService.RequestTimeout);
        await RunToIdleAsync();
        Assert.Equal(["1", "2", "1", "2"], _server.AskedIds);
        Assert.StartsWith("Beschreibung von 2", _service.Of(Key("2")).Description!.Value);
    }

    [Fact]
    [Trait("TestCase", "T547-2")]
    public async Task Cancelling_a_search_keeps_what_was_found_and_the_next_search_asks_for_the_rest()
    {
        ServeAll("1", "3");
        _server.Hang("2");
        Scan("1", "2", "3");
        await WaitForHangAsync();

        _service.CancelRun();
        await RunToIdleAsync();

        Assert.Equal(["1", "2"], _server.AskedIds);
        Assert.NotNull(_service.Of(Key("1")).Description);
        Assert.Equal(new MetadataProgress(2, 3, false, false, null), _service.Progress);
        Assert.Contains(Log, line => line.Contains("vom Spieler abgebrochen", StringComparison.Ordinal));

        ServeAll("2");
        Assert.True(_service.FetchMissing());
        await RunToIdleAsync();
        Assert.Equal(["1", "2", "2", "3"], _server.AskedIds);
        Assert.Equal(new MetadataProgress(0, 3, false, false, null), _service.Progress);
    }

    [Fact]
    [Trait("TestCase", "T547-3")]
    public async Task Cancelling_a_reload_keeps_the_old_data_of_the_games_it_didnt_reach_and_nothing_counts_as_missing()
    {
        ServeAll("1", "2", "3");
        Scan("1", "2", "3");
        await RunToIdleAsync();

        _server.Serve("1", "Neue Beschreibung, die nicht zu lang ist und dir gefällt.");
        _server.Hang("2");
        Assert.True(_service.ReloadAll());
        await WaitForHangAsync();
        _service.CancelRun();
        Assert.False(_service.Progress.Reload);
        await RunToIdleAsync();

        Assert.StartsWith("Neue Beschreibung", _service.Of(Key("1")).Description!.Value);
        Assert.StartsWith("Beschreibung von 3", _service.Of(Key("3")).Description!.Value);
        Assert.Equal(new MetadataProgress(0, 3, false, false, null), _service.Progress);
        Assert.True(_service.ReloadAll());
    }

    [Fact]
    [Trait("TestCase", "T493-27")]
    public async Task Reloading_without_any_network_reports_once_and_a_background_run_never()
    {
        var failures = 0;
        _service.ReloadFailed += (_, _) => Interlocked.Increment(ref failures);
        _server.Offline = true;
        Scan("1", "2");
        await RunToIdleAsync();
        Assert.Equal(0, failures);

        // From the confirmation on the reload counts as running, so a second press starts none; failing at once, it
        // ends as running no more and can be pressed again.
        Assert.True(_service.ReloadAll());
        Assert.True(_service.Progress.Reload);
        Assert.False(_service.ReloadAll());
        await RunToIdleAsync();
        Assert.Equal(1, failures);
        Assert.False(_service.Progress.Reload);
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
    }

    [Fact]
    [Trait("TestCase", "T493-26")]
    public void Reloading_before_the_first_scan_does_nothing()
    {
        Assert.False(_service.ReloadAll());
        Assert.Equal(0, _service.GameCount);
    }

    [Fact]
    [Trait("TestCase", "T493-29")]
    public async Task Log_lines_hold_neither_descriptions_nor_addresses()
    {
        ServeAll("1");
        _server.Fail("2", HttpStatusCode.BadGateway);
        Scan("1", "2");
        await RunToIdleAsync();

        Assert.NotEmpty(Log);
        Assert.DoesNotContain(Log, line => line.Contains("Beschreibung") || line.Contains("https://") || line.Contains("store.example"));
    }

    [Fact]
    [Trait("TestCase", "T493-29")]
    public async Task A_provider_that_throws_costs_only_its_own_data()
    {
        var throwing = new ThrowingSource();
        ServeAll("1");
        _service.Update([
            new LibraryMetadata(new LauncherLibrary(new LauncherInfo("broken", "Broken", "broken"), [GameOf("9") with { LauncherId = "broken" }]), throwing),
            new LibraryMetadata(new LauncherLibrary(Launcher, [GameOf("1")]), _source),
        ]);
        await RunToIdleAsync();

        Assert.NotNull(_service.Of(Key("1")).Description);
        Assert.Contains(Log, line => line.Contains("broken nicht lesbar"));
    }

    private sealed class ThrowingSource : ILauncherMetadata
    {
        public IReadOnlySet<MetadataField> Fields { get; } = new HashSet<MetadataField>();

        public string Summary => "Made-up summary";

        public string About => "Made-up about";

        public IMetadataLookup Look(IReadOnlyCollection<Game> games) => throw new InvalidOperationException("made-up failure");
    }

    [Fact]
    [Trait("TestCase", "T557-4")]
    public async Task After_a_language_change_the_cached_texts_stay_until_a_reload_asks_the_store_in_the_new_language()
    {
        const string german = "Baue eine Stadt, die nicht zu klein ist und dir gefällt.";
        const string english = "Build a city that is not too small and that you will like.";
        _server.Serve("1", german);
        Scan("1");
        await RunToIdleAsync();
        Assert.Equal(["https://store.example/app/1?l=de"], _server.Requests.Select(r => r.Request.RequestUri!.AbsoluteUri));

        // Arcadia restarts in English: the German text loaded before stays, nothing is asked again.
        _language = CultureInfo.GetCultureInfo("en-US");
        Restart();
        Scan("1");
        await RunToIdleAsync();
        Assert.Equal(german, _service.Of(Key("1")).Description!.Value);
        Assert.Single(_server.Requests);

        // "Alle Metadaten neu laden" asks in English, and the English text replaces the German one.
        _server.Serve("1", english);
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
        Assert.Equal("https://store.example/app/1?l=en", _server.Requests[^1].Request.RequestUri!.AbsoluteUri);
        Assert.Equal(english, _service.Of(Key("1")).Description!.Value);

        // The cache keeps the language it was asked in: after the next start in English the text still shows.
        Restart();
        Scan("1");
        await RunToIdleAsync();
        Assert.Equal(english, _service.Of(Key("1")).Description!.Value);
    }

    [Fact]
    [Trait("TestCase", "T557-6")]
    public async Task The_first_age_rating_is_the_regions()
    {
        _server.Serve("1", "-");
        _source.Local["1"] = new GameMetadata
        {
            AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "16"), new AgeRating(AgeRatingBoard.Pegi, "18"), new AgeRating(AgeRatingBoard.Esrb, "M")],
        };
        _service.Dispose();
        _service = new MetadataService(new MetadataCache(CacheFolder), new HttpClient(_server), _time) { Language = _language, FirstAgeRatingBoard = AgeRatingBoard.Esrb };
        _service.Open();
        Scan("1");
        await RunToIdleAsync();

        Assert.Equal([AgeRatingBoard.Esrb, AgeRatingBoard.Usk, AgeRatingBoard.Pegi], _service.Of(Key("1")).AgeRatings.Select(r => r.Value.Board));
    }
}
