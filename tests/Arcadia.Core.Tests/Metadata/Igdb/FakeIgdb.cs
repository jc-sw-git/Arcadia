using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>
/// Twitch's token service and IGDB's API as fakes (#494): made-up access values, games served per Steam AppID, name or
/// IGDB id as JSON; offline, a status, a hang, throttling; every request recorded with its body, headers and time.
/// </summary>
internal sealed partial class FakeIgdb(TimeProvider time) : HttpMessageHandler
{
    public const string ClientId = "madeupclientid0000000001";
    public const string Secret = "madeupsecret00000000000000000001";
    public const string Token = "madeuptoken000000000000000000a";

    private readonly Dictionary<string, string> _bySteam = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, string> _byId = [];

    public List<Recorded> Requests { get; } = [];

    /// <summary>The secret Twitch accepts; anything else gets HTTP 400 ("invalid client").</summary>
    public string AcceptedSecret { get; set; } = Secret;

    /// <summary>IGDB turns every token down (HTTP 401).</summary>
    public bool RejectsTokens { get; set; }

    public bool Offline { get; set; }

    public HttpStatusCode? ApiStatus { get; set; }

    public string? ApiBody { get; set; }

    public TimeSpan? Hang { get; set; }

    public int ThrottleNext { get; set; }

    public sealed record Recorded(Uri Address, string Body, IReadOnlyDictionary<string, string> Headers, DateTimeOffset At);

    public int TokenRequests => Snapshot().Count(r => r.Address.Host == "id.twitch.tv");

    public int ApiRequests => Snapshot().Count(r => r.Address.Host == "api.igdb.com");

    public IReadOnlyList<Recorded> Snapshot()
    {
        lock (Requests)
        {
            return [.. Requests];
        }
    }

    public void ServeSteam(string appId, params string[] games) => _bySteam[appId] = "[" + string.Join(',', games.Select(g => "{\"id\":1,\"game\":" + g + "}")) + "]";

    public void ServeName(string term, params string[] games) => _byName[term] = "[" + string.Join(',', games) + "]";

    public void ServeId(long id, string game) => _byId[id] = "[" + game + "]";

    /// <summary>A game as IGDB answers: id, name, type, optional fields as JSON members (e.g. <c>"summary":"…"</c>).</summary>
    public static string Game(long id, string name, int type = 0, string extra = "") =>
        $"{{\"id\":{id},\"name\":\"{name}\",\"game_type\":{type}{(extra.Length > 0 ? "," + extra : string.Empty)}}}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancel);
        lock (Requests)
        {
            Requests.Add(new Recorded(request.RequestUri!, body, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), time.GetUtcNow()));
        }

        if (Offline)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "No connection could be made.");
        }

        if (request.RequestUri!.Host == "id.twitch.tv")
        {
            return body.Contains("client_secret=" + AcceptedSecret, StringComparison.Ordinal) && body.Contains("client_id=" + ClientId, StringComparison.Ordinal)
                ? Json($"{{\"access_token\":\"{Token}\",\"expires_in\":5000000,\"token_type\":\"bearer\"}}")
                : new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"status\":400,\"message\":\"invalid client\"}") };
        }

        if (Hang is { } hang)
        {
            await Task.Delay(hang, time, cancel);
        }

        if (ThrottleNext > 0)
        {
            ThrottleNext--;
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        }

        if (RejectsTokens)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (ApiStatus is { } status)
        {
            return new HttpResponseMessage(status);
        }

        if (ApiBody is { } fixedBody)
        {
            return Json(fixedBody);
        }

        if (SteamUid().Match(body) is { Success: true } steam)
        {
            return Json(_bySteam.GetValueOrDefault(steam.Groups[1].Value, "[]"));
        }

        if (Search().Match(body) is { Success: true } search)
        {
            return Json(_byName.GetValueOrDefault(search.Groups[1].Value, "[]"));
        }

        if (ById().Match(body) is { Success: true } id)
        {
            return Json(_byId.GetValueOrDefault(long.Parse(id.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), "[]"));
        }

        return Json("[]");
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(text)) };

    [GeneratedRegex("uid = \"([0-9]+)\"")]
    private static partial Regex SteamUid();

    [GeneratedRegex("^search \"([^\"]*)\";")]
    private static partial Regex Search();

    [GeneratedRegex("where id = ([0-9]+);")]
    private static partial Regex ById();
}
