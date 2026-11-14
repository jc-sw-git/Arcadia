using System.Text.Json.Nodes;
using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>
/// #494: IGDB in the metadata run, with a fake store, a fake Twitch and IGDB and fake time: no request without access,
/// matching by AppID and by name, the order and the switch merged from the cache, doubtful titles, failures, the pace
/// and the progress.
/// </summary>
public sealed partial class MetadataServiceIgdbTests : IDisposable
{
    private static readonly LauncherInfo Steamish = new("madeup-steam", "Made-up Steam", "madeup-steam");
    private static readonly LauncherInfo Epicish = new("madeup-epic", "Made-up Epic", "madeup-epic");

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-metadata-igdb-").FullName;
    private readonly FakeTimeProvider _time = new();
    private readonly FakeStoreServer _store;
    private readonly FakeIgdb _igdb;
    private readonly FakeMetadataSource _steamSource;
    private readonly FakeMetadataSource _epicSource = new() { Store = null };
    private readonly List<string> _log = [];
    private MetadataSettings _settings = new();
    private SeveralHits _severalHits = SeveralHits.Ask;
    private IgdbAccess _access;
    private MetadataService _service;

    public MetadataServiceIgdbTests()
    {
        _store = new FakeStoreServer(_time);
        _igdb = new FakeIgdb(_time);
        _steamSource = new FakeMetadataSource { StoreIds = game => new ArtworkStoreId(ArtworkStoreId.Steam, game.Id) };
        _access = NewAccess();
        _service = NewService();
        _service.Open();
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private IgdbAccess NewAccess() => new(new IgdbAccessStore(Path.Combine(_root, "data"), null, null), new IgdbClient(new HttpClient(_igdb), _time), _time);

    private MetadataService NewService() => new(new MetadataCache(Path.Combine(_root, "metadata")), new HttpClient(_store), _time)
    {
        // A German PC (#557), whatever the machine running the tests speaks and wherever it stands.
        Language = System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
        FirstAgeRatingBoard = AgeRatingBoard.Usk,
        Log = line =>
        {
            lock (_log)
            {
                _log.Add(line);
            }
        },
        Igdb = _access,
        Settings = () => _settings,
        SeveralHits = () => _severalHits,
    };

    private async Task GrantAccessAsync()
    {
        await _access.SetClientIdAsync(FakeIgdb.ClientId);
        await _access.SetSecretAsync(FakeIgdb.Secret);
    }

    private static Game GameOf(LauncherInfo launcher, string id, string title) => new()
    {
        Id = id,
        LauncherId = launcher.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri("madeup://run/" + id),
    };

    private void Scan(IReadOnlyList<Game>? steam = null, IReadOnlyList<Game>? epic = null) => _service.Update(
    [
        new LibraryMetadata(new LauncherLibrary(Steamish, steam ?? []), _steamSource),
        new LibraryMetadata(new LauncherLibrary(Epicish, epic ?? []), _epicSource),
    ]);

    private async Task RunToIdleAsync()
    {
        for (var i = 0; i < 5000; i++)
        {
            var idle = _service.WhenIdleAsync();
            if (idle.IsCompleted)
            {
                await idle;
                return;
            }

            await Task.Delay(1);
            _time.Advance(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("The metadata run didn't end.");
    }

    private async Task<IReadOnlyCollection<string>> ChangedAfterAsync(Action change)
    {
        var changed = new TaskCompletionSource<IReadOnlyCollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, IReadOnlyCollection<string> keys) => changed.TrySetResult(keys);
        _service.Changed += OnChanged;
        change();
        try
        {
            return await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _service.Changed -= OnChanged;
        }
    }

    private static string Summary(string text) => $"\"summary\":\"{text}\"";

    [Fact]
    [Trait("TestCase", "T494-10")]
    [Trait("TestCase", "T494-32")]
    public async Task Without_access_twitch_and_igdb_get_not_one_request()
    {
        var epic = Enumerable.Range(1, 1000).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i)).ToList();
        Scan(epic: epic);
        await RunToIdleAsync();
        _service.ReloadAll();
        await RunToIdleAsync();
        await _access.SetClientIdAsync(FakeIgdb.ClientId);
        Scan(epic: epic);
        await RunToIdleAsync();

        Assert.Empty(_igdb.Snapshot());
        Assert.Equal(MetadataProgress.NotYet with { Total = 0 }, _service.Progress with { Total = 0 });
    }

    [Fact]
    [Trait("TestCase", "T494-13")]
    public async Task A_steam_game_is_asked_by_its_appid_only_and_an_unknown_appid_is_never_searched_by_name()
    {
        await GrantAccessAsync();
        _igdb.ServeSteam("100", FakeIgdb.Game(1, "Nordwind", extra: Summary("Made-up English text.")));
        Scan(steam: [GameOf(Steamish, "100", "Nordwind"), GameOf(Steamish, "200", "Nordwind")]);
        await RunToIdleAsync();

        var api = _igdb.Snapshot().Where(r => r.Address.Host == "api.igdb.com").ToList();
        Assert.Equal(2, api.Count);
        Assert.All(api, r => Assert.Contains("external_game_source = 1", r.Body));
        Assert.DoesNotContain(api, r => r.Body.StartsWith("search", StringComparison.Ordinal));
        Assert.Equal("IGDB", _service.Of(Game.KeyOf(Steamish.Id, "100")).Description?.Source);
        Assert.True(_service.Of(Game.KeyOf(Steamish.Id, "200")).Description is null);

        Scan(steam: [GameOf(Steamish, "100", "Nordwind"), GameOf(Steamish, "200", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal(2, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T494-14")]
    public async Task An_epic_game_is_searched_once_by_its_cleaned_title_and_dlc_and_mods_are_dropped_first()
    {
        await GrantAccessAsync();
        _igdb.ServeName("Nordwind", FakeIgdb.Game(2, "Nordwind – Season Pass", 1), FakeIgdb.Game(3, "Nordwind Remix", 5), FakeIgdb.Game(1, "Nordwind", extra: Summary("Made-up English text.")));
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind™ "), GameOf(Epicish, "e2", "™")]);
        await RunToIdleAsync();

        var api = Assert.Single(_igdb.Snapshot(), r => r.Address.Host == "api.igdb.com");
        Assert.StartsWith("search \"Nordwind\";", api.Body);
        Assert.Equal("Made-up English text.", _service.Of(Game.KeyOf(Epicish.Id, "e1")).Description?.Value);

        Scan(epic: [GameOf(Epicish, "e1", "Nordwind™ ")]);
        await RunToIdleAsync();
        Assert.Equal(1, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T538-8")]
    public async Task Demo_games_are_never_asked_never_wait_and_show_no_igdb_data_cached_before()
    {
        await GrantAccessAsync();
        _igdb.ServeName("Nordwind", FakeIgdb.Game(1, "Nordwind", extra: Summary("Made-up English text.")));
        _igdb.ServeName("Echo", FakeIgdb.Game(2, "Echo"), FakeIgdb.Game(3, "Echo"));
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        var key = Game.KeyOf(Epicish.Id, "e1");
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal("Made-up English text.", _service.Of(key).Description?.Value);
        var asked = _igdb.ApiRequests;

        // The same library as demo data: nothing asked, nothing waits, only the launcher's data shows.
        _service.Update([new LibraryMetadata(new LauncherLibrary(Epicish, [GameOf(Epicish, "e1", "Nordwind"), GameOf(Epicish, "e2", "Echo")]), _epicSource, MadeUp: true)]);
        await RunToIdleAsync();
        _service.ReloadAll();
        await RunToIdleAsync();

        Assert.Equal(asked, _igdb.ApiRequests);
        Assert.Empty(_service.Waiting);
        Assert.Null(_service.Of(key).Description);
        Assert.Equal("Made-up Studio", Assert.Single(_service.Of(key).Developers!.Value));
    }

    [Fact]
    [Trait("TestCase", "T494-15")]
    public async Task No_hit_keeps_the_launcher_data_and_searches_again_at_the_next_scan()
    {
        await GrantAccessAsync();
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal("Made-up Epic", _service.Of(Game.KeyOf(Epicish.Id, "e1")).Developers?.Source);

        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal(2, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T494-16")]
    public async Task Several_hits_with_selbst_auswaehlen_keep_the_launcher_data_and_are_asked_once()
    {
        await GrantAccessAsync();
        _igdb.ServeName("Nordwind", FakeIgdb.Game(1, "Nordwind", extra: "\"first_release_date\":1420070400"), FakeIgdb.Game(2, "Nordwind", extra: "\"first_release_date\":1609459200"));
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();

        Assert.Equal(1, _igdb.ApiRequests);
        Assert.True(_service.Of(Game.KeyOf(Epicish.Id, "e1")).IsEmpty);
    }

    [Fact]
    [Trait("TestCase", "T494-17")]
    public async Task Several_hits_with_ersten_nehmen_take_the_first_in_igdbs_order_and_replace_a_doubtful_title()
    {
        await GrantAccessAsync();
        _severalHits = SeveralHits.TakeFirst;
        _igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln", extra: Summary("First.")), FakeIgdb.Game(2, "Köln", extra: Summary("Second.")));
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();

        Assert.Equal("First.", _service.Of(Game.KeyOf(Epicish.Id, "e1")).Description?.Value);
        Assert.Equal("Köln", _service.TitleOf(Game.KeyOf(Epicish.Id, "e1")));
    }

    [Fact]
    [Trait("TestCase", "T494-25")]
    public async Task A_doubtful_title_is_replaced_in_both_orders_and_a_title_without_a_question_mark_stays()
    {
        await GrantAccessAsync();
        _igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln"));
        _igdb.ServeName("Nordwind", FakeIgdb.Game(2, "NORDWIND: Remastered"));
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        Scan(epic: [GameOf(Epicish, "e1", "K?ln"), GameOf(Epicish, "e2", "Nordwind")]);
        await RunToIdleAsync();

        var koeln = Game.KeyOf(Epicish.Id, "e1");
        Assert.Equal("Köln", _service.TitleOf(koeln));
        Assert.Equal(("K?ln", "Made-up Epic"), (_service.Of(koeln).LauncherTitle!.Value, _service.Of(koeln).LauncherTitle!.Source));
        Assert.Null(_service.TitleOf(Game.KeyOf(Epicish.Id, "e2")));

        await ChangedAfterAsync(() =>
        {
            _settings = _settings with { Order = [MetadataSourceKind.Igdb, MetadataSourceKind.Launcher] };
            _service.SettingsChanged();
        });
        Assert.Equal("Köln", _service.TitleOf(koeln));
        Assert.Null(_service.TitleOf(Game.KeyOf(Epicish.Id, "e2")));
    }

    [Fact]
    [Trait("TestCase", "T494-23")]
    public async Task Moving_igdb_first_changes_the_fields_at_once_without_a_request_and_the_german_description_stays()
    {
        await GrantAccessAsync();
        _steamSource.Local["100"] = new GameMetadata { Genres = [Genre.Strategy], Developers = ["Made-up Studio"] };
        _store.Serve("100", "Eine erfundene deutsche Beschreibung, die für dieses Spiel gilt und schön ist.");
        _igdb.ServeSteam("100", FakeIgdb.Game(1, "Nordwind", extra: Summary("A made-up English text about the game.") + ",\"genres\":[{\"name\":\"Adventure\"}],\"first_release_date\":1609459200"));
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();
        var key = Game.KeyOf(Steamish.Id, "100");
        Assert.Equal("Made-up Steam", _service.Of(key).Genres?.Source);
        var requests = _igdb.Snapshot().Count + _store.RequestCount;

        var changed = await ChangedAfterAsync(() =>
        {
            _settings = _settings with { Order = [MetadataSourceKind.Igdb, MetadataSourceKind.Launcher] };
            _service.SettingsChanged();
        });

        Assert.Contains(key, changed);
        var shown = _service.Of(key);
        Assert.Equal([Genre.Adventure], shown.Genres!.Value);
        Assert.Equal("IGDB", shown.Genres.Source);
        Assert.Equal("IGDB", shown.ReleaseDate?.Source);
        Assert.Equal("Made-up Steam", shown.Developers?.Source);
        Assert.Equal("Made-up Steam", shown.Description?.Source);
        Assert.Equal(requests, _igdb.Snapshot().Count + _store.RequestCount);
    }

    [Fact]
    [Trait("TestCase", "T494-24")]
    public async Task Igdb_switched_off_hides_its_fields_and_titles_and_on_brings_them_back_without_a_request()
    {
        await GrantAccessAsync();
        _igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln", extra: Summary("Made-up.")));
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();
        var key = Game.KeyOf(Epicish.Id, "e1");
        var requests = _igdb.Snapshot().Count;

        await ChangedAfterAsync(() =>
        {
            _settings = _settings with { Off = [MetadataSourceKind.Igdb] };
            _service.SettingsChanged();
        });
        Assert.Null(_service.TitleOf(key));
        Assert.True(_service.Of(key).IsEmpty);
        Scan(epic: [GameOf(Epicish, "e2", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal(requests, _igdb.Snapshot().Count);

        await ChangedAfterAsync(() =>
        {
            _settings = _settings with { Off = [] };
            _service.SettingsChanged();
        });
        Assert.Equal("Köln", _service.TitleOf(key));
        Assert.Equal(requests, _igdb.Snapshot().Count);
    }

    [Fact]
    [Trait("TestCase", "T541-2")]
    public async Task Igdb_adds_the_boards_steam_lacks_and_steams_own_board_stays_first()
    {
        await GrantAccessAsync();
        _steamSource.Local["100"] = new GameMetadata { AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "16")] };
        _igdb.ServeSteam("100", FakeIgdb.Game(1, "Nordwind", extra: Ratings("USK 18", "PEGI 18", "ESRB M")));
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();

        Assert.Equal(
            [
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "16"), "Made-up Steam"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Pegi, "18"), "IGDB"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Esrb, "M"), "IGDB"),
            ],
            _service.Of(Game.KeyOf(Steamish.Id, "100")).AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T541-2")]
    public async Task An_answer_cached_before_ratings_were_read_right_is_asked_again_and_adds_the_missing_boards()
    {
        await GrantAccessAsync();
        _steamSource.Local["100"] = new GameMetadata { AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "12", ByStore: true)] };
        _igdb.ServeSteam("100", FakeIgdb.Game(1, "Nordwind", extra: Ratings("PEGI 12", "ESRB E10")));
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();
        _service.Dispose();

        // A file as the version before wrote it: no "reading", and only what that version could read.
        var file = Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "metadata"), "*.json", SearchOption.AllDirectories));
        var stored = JsonNode.Parse(File.ReadAllText(file))!;
        stored["igdb"]!.AsObject().Remove("reading");
        stored["igdb"]!["data"]!["ageRatings"] = new JsonArray();
        File.WriteAllText(file, stored.ToJsonString());
        var requests = _igdb.ApiRequests;

        _service = NewService();
        _service.Open();
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();

        Assert.Equal(requests + 1, _igdb.ApiRequests);
        Assert.Equal(
            [
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "12", ByStore: true), "Made-up Steam"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Pegi, "12"), "IGDB"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Esrb, "E10+"), "IGDB"),
            ],
            _service.Of(Game.KeyOf(Steamish.Id, "100")).AgeRatings);
    }

    /// <summary>IGDB's age ratings as the query asks for them: the organization and the rating category's own text.</summary>
    private static string Ratings(params string[] ratings) =>
        "\"age_ratings\":[" + string.Join(',', ratings.Select(rating => rating.Split(' ') switch
        {
            ["ESRB", var value] => $"{{\"organization\":1,\"rating_category\":{{\"id\":1,\"rating\":\"{value}\"}}}}",
            ["PEGI", var value] => $"{{\"organization\":2,\"rating_category\":{{\"id\":2,\"rating\":\"{value}\"}}}}",
            [_, var value] => $"{{\"organization\":4,\"rating_category\":{{\"id\":3,\"rating\":\"{value}\"}}}}",
            _ => throw new ArgumentException(rating),
        })) + "]";

    [Fact]
    [Trait("TestCase", "T494-26")]
    public async Task An_epic_game_gets_igdbs_english_description_genres_year_and_usk_and_keeps_epics_developer()
    {
        await GrantAccessAsync();
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        _igdb.ServeName("Nordwind", FakeIgdb.Game(1, "Nordwind", extra: Summary("A made-up English text about the game.")
            + ",\"genres\":[{\"name\":\"Role-playing (RPG)\"}],\"first_release_date\":1609459200," + Ratings("USK 12")));
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind")]);
        await RunToIdleAsync();

        var shown = _service.Of(Game.KeyOf(Epicish.Id, "e1"));
        Assert.Equal(("IGDB", "IGDB", "IGDB", "Made-up Epic"), (shown.Description!.Source, shown.Genres!.Source, shown.ReleaseDate!.Source, shown.Developers!.Source));
        Assert.Equal([Genre.RolePlaying], shown.Genres.Value);
        Assert.Equal(new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "12"), "IGDB"), Assert.Single(shown.AgeRatings));
    }

    [Fact]
    [Trait("TestCase", "T494-12")]
    public async Task Igdb_gets_at_most_4_requests_a_second()
    {
        await GrantAccessAsync();
        Scan(epic: [.. Enumerable.Range(1, 40).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i))]);
        await RunToIdleAsync();

        var api = _igdb.Snapshot().Where(r => r.Address.Host == "api.igdb.com").Select(r => r.At).ToList();
        Assert.Equal(40, api.Count);
        for (var i = 4; i < api.Count; i++)
        {
            Assert.True(api[i] - api[i - 4] >= TimeSpan.FromSeconds(1), $"request {i} came too early");
        }
    }

    [Fact]
    [Trait("TestCase", "T494-12")]
    public async Task Without_network_igdb_is_left_out_for_the_run_with_one_log_line_and_tried_again_at_the_next_scan()
    {
        await GrantAccessAsync();
        _igdb.Offline = true;
        var games = Enumerable.Range(1, 20).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i)).ToList();
        Scan(epic: games);
        await RunToIdleAsync();
        Assert.Equal(1, _igdb.ApiRequests);

        _igdb.Offline = false;
        Scan(epic: games);
        await RunToIdleAsync();
        Assert.Equal(21, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T494-12")]
    public async Task A_server_error_or_a_broken_answer_stops_igdb_for_the_run()
    {
        await GrantAccessAsync();
        _igdb.ApiBody = "{broken";
        Scan(epic: [.. Enumerable.Range(1, 5).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i))]);
        await RunToIdleAsync();
        Assert.Equal(1, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T494-9")]
    public async Task An_access_rejected_during_a_fetch_stops_igdb_and_the_launcher_data_keeps_working()
    {
        await GrantAccessAsync();
        _igdb.RejectsTokens = true;
        _epicSource.Local["e1"] = new GameMetadata { Developers = ["Made-up Studio"] };
        Scan(epic: [.. Enumerable.Range(1, 5).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i))]);
        await RunToIdleAsync();

        Assert.Equal(IgdbAccessState.Rejected, _access.State);
        Assert.Equal(2, _igdb.ApiRequests);
        Assert.Equal("Made-up Epic", _service.Of(Game.KeyOf(Epicish.Id, "e1")).Developers?.Source);
    }

    [Fact]
    [Trait("TestCase", "T494-28")]
    public async Task Igdbs_answers_are_cached_and_shown_after_a_restart_without_a_request()
    {
        await GrantAccessAsync();
        _igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln", extra: Summary("Made-up &amp; cached.")));
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();

        _service.Dispose();
        _igdb.Offline = true;
        _service = NewService();
        _service.Open();
        Scan(epic: [GameOf(Epicish, "e1", "K?ln")]);
        await RunToIdleAsync();

        var key = Game.KeyOf(Epicish.Id, "e1");
        Assert.Equal("Made-up & cached.", _service.Of(key).Description?.Value);
        Assert.Equal("Köln", _service.TitleOf(key));
    }

    [Fact]
    [Trait("TestCase", "T494-29")]
    public async Task Closing_during_a_fetch_ends_at_once_and_the_next_start_goes_on_with_the_rest()
    {
        await GrantAccessAsync();
        _igdb.Hang = TimeSpan.FromHours(1);
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind"), GameOf(Epicish, "e2", "Sturmwind")]);
        for (var i = 0; i < 500 && _igdb.ApiRequests == 0; i++)
        {
            await Task.Delay(1);
        }

        var idle = _service.WhenIdleAsync();
        _service.Dispose();
        await idle.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp*", SearchOption.AllDirectories));

        _igdb.Hang = null;
        _service = NewService();
        _service.Open();
        Scan(epic: [GameOf(Epicish, "e1", "Nordwind"), GameOf(Epicish, "e2", "Sturmwind")]);
        await RunToIdleAsync();
        Assert.Equal(3, _igdb.ApiRequests);
    }

    [Fact]
    [Trait("TestCase", "T494-30")]
    public async Task The_progress_counts_the_games_igdb_still_has_to_answer_at_its_pace()
    {
        await GrantAccessAsync();
        _igdb.Hang = TimeSpan.FromHours(1);
        Scan(epic: [.. Enumerable.Range(1, 400).Select(i => GameOf(Epicish, "e" + i, "Made-up Game " + i))]);
        for (var i = 0; i < 500 && !_service.Progress.Running; i++)
        {
            await Task.Delay(1);
        }

        var progress = _service.Progress;
        Assert.Equal((400, 400, true), (progress.Missing, progress.Total, progress.Running));
        Assert.Equal(TimeSpan.FromSeconds(100), progress.Remaining);
    }
}
