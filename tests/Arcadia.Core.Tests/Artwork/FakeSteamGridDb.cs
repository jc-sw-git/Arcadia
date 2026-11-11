using System.Net;
using System.Text;
using System.Text.Json;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// SteamGridDB's API for the tests (#489): answers <c>grids</c> and <c>heroes</c> by store and id as the real one does
/// (<c>{"success":true,"data":[…]}</c>), 401 for any key but <see cref="ValidKey"/>, 404 for a game it doesn't know; can
/// throttle, hang until cancelled or be offline. Records every request. The images it names are served by the test's
/// image server, not by this.
/// </summary>
internal sealed class FakeSteamGridDb : HttpMessageHandler
{
    /// <summary>A made-up key of SteamGridDB's shape.</summary>
    public const string Key = "0123456789abcdef0123456789abcdef";

    public const string Host = "www.steamgriddb.com";

    private readonly Dictionary<string, List<(string Url, int Width, int Height, int Score)>> _images = new(StringComparer.Ordinal);

    public string ValidKey { get; set; } = Key;

    public List<HttpRequestMessage> Requests { get; } = [];

    public bool Offline { get; set; }

    /// <summary>Every request waits until it is cancelled (no answer, the client's timeout).</summary>
    public bool Hangs { get; set; }

    /// <summary>Every answer comes this much later, in real time (a slow service for the speed tests).</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>The next requests answer 429, each with this Retry-After (null: none).</summary>
    public Queue<TimeSpan?> Throttle { get; } = new();

    /// <summary>Every request answers this status (e.g. 500); null: the normal answers.</summary>
    public HttpStatusCode? Failure { get; set; }

    /// <summary>Every answer's body, instead of the normal JSON; null: the normal answers.</summary>
    public string? Body { get; set; }

    public int Count
    {
        get
        {
            lock (Requests)
            {
                return Requests.Count;
            }
        }
    }

    /// <summary>A cover (grid) of the game, e.g. <c>Cover("steam", "4000001", url, 600, 900, 5)</c>; a known game without images: none.</summary>
    public FakeSteamGridDb Cover(string store, string id, string url, int width, int height, int score = 0) => Add("grids", store, id, url, width, height, score);

    public FakeSteamGridDb Hero(string store, string id, string url, int width, int height, int score = 0) => Add("heroes", store, id, url, width, height, score);

    /// <summary>A game SteamGridDB knows, without images of the kind.</summary>
    public FakeSteamGridDb Knows(string kind, string store, string id)
    {
        _images.TryAdd($"{kind}/{store}/{id}", []);
        return this;
    }

    private FakeSteamGridDb Add(string kind, string store, string id, string url, int width, int height, int score)
    {
        var key = $"{kind}/{store}/{id}";
        if (!_images.TryGetValue(key, out var list))
        {
            _images[key] = list = [];
        }

        list.Add((url, width, height, score));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        if (Offline)
        {
            throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.");
        }

        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Throttle.TryDequeue(out var wait))
        {
            var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (wait is { } delta)
            {
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
            }

            return throttled;
        }

        if (request.Headers.Authorization is not { Scheme: "Bearer" } auth || auth.Parameter != ValidKey)
        {
            return Json(HttpStatusCode.Unauthorized, """{"success":false,"errors":["Unauthorized"]}""");
        }

        if (Failure is { } failure)
        {
            return new HttpResponseMessage(failure);
        }

        if (Body is { } body)
        {
            return Json(HttpStatusCode.OK, body);
        }

        // /api/v2/<kind>/<store>/<id>
        var path = request.RequestUri!.AbsolutePath["/api/v2/".Length..];
        var game = Uri.UnescapeDataString(path);
        if (path == "heroes/steam/220")
        {
            // The key check's game: known, whatever the test set up.
            return Json(HttpStatusCode.OK, """{"success":true,"data":[]}""");
        }

        if (!_images.TryGetValue(game, out var images))
        {
            return Json(HttpStatusCode.NotFound, """{"success":false,"errors":["Game not found"]}""");
        }

        var data = images.Select((image, i) => new Dictionary<string, object>
        {
            ["id"] = i + 1,
            ["score"] = image.Score,
            ["style"] = "alternate",
            ["width"] = image.Width,
            ["height"] = image.Height,
            ["nsfw"] = false,
            ["humor"] = false,
            ["mime"] = "image/png",
            ["url"] = image.Url,
            ["thumb"] = image.Url,
        });
        return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { success = true, data }));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>The store and id each request asked for, e.g. <c>heroes/egs/abc</c>.</summary>
    public IReadOnlyList<string> Asked()
    {
        lock (Requests)
        {
            return [.. Requests.Select(r => Uri.UnescapeDataString(r.RequestUri!.AbsolutePath["/api/v2/".Length..]))];
        }
    }
}
