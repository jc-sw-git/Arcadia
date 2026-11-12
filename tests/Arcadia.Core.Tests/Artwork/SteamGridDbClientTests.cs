using System.Net;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#489: what Arcadia sends SteamGridDB and how it reads the answers (T489-11, T489-18, T489-21, T489-22).</summary>
public sealed class SteamGridDbClientTests
{
    private readonly FakeSteamGridDb _server = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly SteamGridDbClient _client;

    public SteamGridDbClientTests() => _client = new SteamGridDbClient(new HttpClient(_server), _time)
    {
        Log = line =>
        {
            lock (_log)
            {
                _log.Add(line);
            }
        },
    };

    private static readonly ArtworkStoreId Game = new(ArtworkStoreId.Steam, "4000001");

    [Fact]
    [Trait("TestCase", "T489-11")]
    public async Task A_request_goes_over_https_to_the_api_with_the_key_only_in_its_header()
    {
        _server.Cover("steam", "4000001", "https://cdn.example/grid/a.png", 600, 900);

        await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);
        await _client.ImagesAsync(FakeSteamGridDb.Key, new ArtworkStoreId(ArtworkStoreId.Epic, "a1b2c3"), ArtworkKind.Hero, default);

        Assert.Collection(_server.Requests,
            request =>
            {
                Assert.Equal("https://www.steamgriddb.com/api/v2/grids/steam/4000001", request.RequestUri!.GetLeftPart(UriPartial.Path));
                Assert.Contains("dimensions=600x900", request.RequestUri.Query, StringComparison.Ordinal);
                Assert.Contains("types=static", request.RequestUri.Query, StringComparison.Ordinal);
            },
            request => Assert.Equal("https://www.steamgriddb.com/api/v2/heroes/egs/a1b2c3", request.RequestUri!.GetLeftPart(UriPartial.Path)));
        Assert.All(_server.Requests, request =>
        {
            Assert.Equal(("Bearer", FakeSteamGridDb.Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
            Assert.DoesNotContain(FakeSteamGridDb.Key, request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            Assert.False(request.Headers.Contains("Cookie"));
        });
    }

    [Theory]
    [Trait("TestCase", "T489-11")]
    [InlineData("gog", "1")]
    [InlineData("../grids", "1")]
    [InlineData("steam", "")]
    [InlineData("steam", ".")]
    [InlineData("epic", "..")]
    public async Task An_unknown_store_or_an_empty_id_sends_nothing(string store, string id)
    {
        var answer = await _client.ImagesAsync(FakeSteamGridDb.Key, new ArtworkStoreId(store, id), ArtworkKind.Cover, default);

        Assert.Equal(SteamGridDbOutcome.NotFound, answer.Outcome);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    [Trait("TestCase", "T489-11")]
    public async Task An_id_is_escaped_so_it_cant_reach_another_path()
    {
        await _client.ImagesAsync(FakeSteamGridDb.Key, new ArtworkStoreId(ArtworkStoreId.Epic, "../../search/x?y=1"), ArtworkKind.Hero, default);

        var asked = Assert.Single(_server.Requests).RequestUri!;
        Assert.StartsWith("/api/v2/heroes/egs/", asked.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(4, asked.AbsolutePath.Count(c => c == '/') - 1);
    }

    [Fact]
    [Trait("TestCase", "T489-18")]
    public async Task Images_come_best_rated_first_and_only_with_https_and_a_size()
    {
        _server.Cover("steam", "4000001", "https://cdn.example/grid/low.png", 600, 900, score: 1)
            .Cover("steam", "4000001", "https://cdn.example/grid/high.png", 600, 900, score: 9)
            .Cover("steam", "4000001", "http://cdn.example/grid/plain.png", 600, 900, score: 20)
            .Cover("steam", "4000001", "https://cdn.example/grid/nosize.png", 0, 900, score: 20);

        var answer = await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);

        Assert.Equal(SteamGridDbOutcome.Found, answer.Outcome);
        Assert.Equal(["https://cdn.example/grid/high.png", "https://cdn.example/grid/low.png"], answer.Images.Select(i => i.Url.AbsoluteUri));
    }

    [Theory]
    [Trait("TestCase", "T489-22")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"success":true,"data":{"url":"https://x"}}""")]
    public async Task An_answer_that_isnt_the_apis_json_counts_as_no_connection(string body)
    {
        _server.Body = body;

        Assert.Equal(SteamGridDbOutcome.NoConnection, (await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default)).Outcome);
    }

    [Fact]
    [Trait("TestCase", "T489-22")]
    public async Task Items_with_wrong_types_are_left_out()
    {
        _server.Body = """{"success":true,"data":[{"url":5,"width":600,"height":900},{"url":"https://cdn.example/a.png","width":"600","height":900},{"url":"https://cdn.example/b.png","width":600,"height":900,"score":"x"}]}""";

        var answer = await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);

        Assert.Equal(["https://cdn.example/b.png"], answer.Images.Select(i => i.Url.AbsoluteUri));
    }

    [Fact]
    public async Task A_game_it_doesnt_know_is_not_found_and_a_wrong_key_is_rejected()
    {
        Assert.Equal(SteamGridDbOutcome.NotFound, (await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default)).Outcome);
        Assert.Equal(SteamGridDbOutcome.KeyRejected, (await _client.ImagesAsync("ffffffffffffffffffffffffffffffff", Game, ArtworkKind.Cover, default)).Outcome);
        Assert.Equal(SteamGridDbOutcome.KeyRejected, await _client.CheckAsync("ffffffffffffffffffffffffffffffff", default));
        Assert.Equal(SteamGridDbOutcome.Found, await _client.CheckAsync(FakeSteamGridDb.Key, default));
    }

    [Theory]
    [Trait("TestCase", "T489-21")]
    [InlineData(3)]
    [InlineData(null)]
    public async Task Throttling_waits_as_long_as_asked_and_then_goes_on(int? seconds)
    {
        _server.Cover("steam", "4000001", "https://cdn.example/grid/a.png", 600, 900);
        _server.Throttle.Enqueue(seconds is { } s ? TimeSpan.FromSeconds(s) : null);
        var wait = seconds is { } given ? TimeSpan.FromSeconds(given) : SteamGridDbClient.DefaultWait;

        var asking = _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);
        await WaitUntilAsync(() => _server.Count == 1);
        _time.Advance(wait - TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        Assert.Equal(1, _server.Count);
        _time.Advance(TimeSpan.FromMilliseconds(1));

        var answer = await asking;
        Assert.Equal((SteamGridDbOutcome.Found, 2), (answer.Outcome, _server.Count));
        Assert.Single(answer.Images);
    }

    [Fact]
    [Trait("TestCase", "T489-21")]
    public async Task No_answer_within_ten_seconds_is_no_connection()
    {
        _server.Hangs = true;

        var asking = _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);
        await WaitUntilAsync(() => _server.Count == 1);
        _time.Advance(SteamGridDbClient.Timeout);

        Assert.Equal(SteamGridDbOutcome.NoConnection, (await asking).Outcome);
        Assert.Contains(_log, line => line.Contains("keine Antwort in 10 s", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("TestCase", "T489-21")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task A_server_error_or_no_network_is_no_connection(HttpStatusCode status)
    {
        _server.Failure = status;
        Assert.Equal(SteamGridDbOutcome.NoConnection, (await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default)).Outcome);

        _server.Offline = true;
        Assert.Equal(SteamGridDbOutcome.NoConnection, (await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default)).Outcome);
    }

    [Fact]
    [Trait("TestCase", "T489-10")]
    public async Task No_log_line_holds_the_key()
    {
        _server.Throttle.Enqueue(TimeSpan.Zero);
        await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);
        await _client.ImagesAsync("ffffffffffffffffffffffffffffffff", Game, ArtworkKind.Cover, default);
        _server.Offline = true;
        await _client.ImagesAsync(FakeSteamGridDb.Key, Game, ArtworkKind.Cover, default);

        Assert.NotEmpty(_log);
        Assert.DoesNotContain(_log, line => line.Contains(FakeSteamGridDb.Key, StringComparison.Ordinal) || line.Contains("ffffffffffffffffffffffffffffffff", StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(done());
    }
}
