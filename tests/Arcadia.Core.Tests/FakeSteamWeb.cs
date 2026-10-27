using System.Net;
using System.Text;
using System.Text.Json;
using Arcadia.Launchers.Steam.Account;

namespace Arcadia.Core.Tests;

/// <summary>
/// Steam's web API as the account sees it (#83): the QR sign-in, new access tokens, the owned games and the profile name.
/// A test approves or rejects the code, takes Steam offline or lets the sign-in expire. Shared with the acceptance tests.
/// </summary>
internal sealed class FakeSteamWeb : HttpMessageHandler
{
    public const ulong SteamId = 76561197960265728 + 100_000_001;

    public const string ProfileName = "Spieler Eins";

    public const string LoginName = "geheimer_login";

    public const ulong ClientId = 4242;

    private readonly Lock _lock = new();
    private int _codes;
    private string? _pendingCodeRenewal;
    private bool _scanReported;
    private bool _sessionEnded;

    public FakeSteamWeb(DateTimeOffset now) => Now = now;

    /// <summary>The clock the issued tokens expire by.</summary>
    public DateTimeOffset Now { get; set; }

    public ulong AccountSteamId { get; set; } = SteamId;

    /// <summary>Every request fails as if the PC were offline.</summary>
    public bool IsOffline { get; set; }

    /// <summary>The next poll returns the tokens.</summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// The phone scans the code and rejects the sign-in: one poll says the code was scanned, the next fails as Steam does.
    /// </summary>
    public bool IsRejected { get; set; }

    /// <summary>Polls fail with this EResult, e.g. 10 (busy), as Steam does when it has a hiccup.</summary>
    public int? PollEResult { get; set; }

    /// <summary>The app ids Steam's store lists as owned, besides <see cref="OwnedGames"/>: soundtracks, tools, DLC.</summary>
    public List<uint> StoreApps { get; } = [];

    /// <summary>The store's list fails as if the store didn't take the sign-in.</summary>
    public bool StoreFails { get; set; }

    /// <summary>The profile name can't be loaded.</summary>
    public bool ProfileFails { get; set; }

    /// <summary>The owned games come back as <c>{"response":{}}</c>.</summary>
    public bool ListsNoGames { get; set; }

    /// <summary>The owned games' request throws something no transport would: a bug in Arcadia's own code.</summary>
    public bool FailsUnexpectedly { get; set; }

    /// <summary>Steam no longer takes the refresh token.</summary>
    public bool IsSignInRevoked { get; set; }

    public List<(uint AppId, string Name, long Minutes, long LastPlayed)> OwnedGames { get; } = [];

    public int OwnedGamesRequests { get; private set; }

    public int AccessTokenRequests { get; private set; }

    public int Polls { get; private set; }

    /// <summary>How many codes Steam handed out, new sign-ins and renewals.</summary>
    public int CodesIssued => _codes;

    /// <summary>The access token issued last; the tests look for it in the log.</summary>
    public string? LastAccessToken { get; private set; }

    public string? LastRefreshToken { get; private set; }

    public static string CodeUrl(int number) => $"https://s.team/q/1/{number}";

    /// <summary>The next poll fails as Steam does when a session nobody scanned ran out.</summary>
    public void EndSession()
    {
        lock (_lock)
        {
            _sessionEnded = true;
        }
    }

    /// <summary>The next poll hands out a new code, as Steam does after a while.</summary>
    public void RenewCode()
    {
        lock (_lock)
        {
            _pendingCodeRenewal = CodeUrl(++_codes);
        }
    }

    public static string Token(ulong steamId, DateTimeOffset expires, string kind)
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Part("""{"typ":"JWT","alg":"EdDSA"}""")}.{Part($$"""{"sub":"{{steamId}}","exp":{{expires.ToUnixTimeSeconds()}},"kind":"{{kind}}"}""")}.c2lnbmF0dXJl";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (IsOffline)
        {
            throw new HttpRequestException("offline");
        }

        var path = request.RequestUri!.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        lock (_lock)
        {
            return (request.RequestUri.Host, path) switch
            {
                ("api.steampowered.com", "/IAuthenticationService/BeginAuthSessionViaQR/v1/") => Begin(),
                ("api.steampowered.com", "/IAuthenticationService/PollAuthSessionStatus/v1/") => Poll(),
                ("api.steampowered.com", "/IAuthenticationService/GenerateAccessTokenForApp/v1/") => AccessToken(),
                ("api.steampowered.com", "/IPlayerService/GetOwnedGames/v1/") => Owned(query["access_token"]),
                ("api.steampowered.com", "/IPlayerService/GetPlayerLinkDetails/v1/") => Profile(query["access_token"], query["steamids[0]"]),
                ("store.steampowered.com", "/dynamicstore/userdata/") => Store(request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join("; ", cookies) : null),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }
    }

    private HttpResponseMessage Begin()
    {
        _scanReported = false;
        _sessionEnded = false;
        return Proto(new ProtoWriter().UInt(1, ClientId).String(2, CodeUrl(++_codes)).Bytes(3, [1, 2, 3]));
    }

    private HttpResponseMessage Poll()
    {
        Polls++;
        if (PollEResult is { } result)
        {
            return EResult(result);
        }

        if (_sessionEnded || (IsRejected && _scanReported))
        {
            return EResult(9);
        }

        var response = new ProtoWriter();
        if (IsRejected)
        {
            response.UInt(5, 1);
            _scanReported = true;
        }

        if (_pendingCodeRenewal is { } renewed)
        {
            response.UInt(1, ClientId + 1).String(2, renewed);
            _pendingCodeRenewal = null;
        }

        if (IsApproved)
        {
            LastRefreshToken = Token(AccountSteamId, Now.AddDays(200), "refresh");
            LastAccessToken = Token(AccountSteamId, Now.AddDays(1), "access");
            response.String(3, LastRefreshToken).String(4, LastAccessToken).String(6, LoginName);
        }

        return Proto(response);
    }

    private HttpResponseMessage AccessToken()
    {
        AccessTokenRequests++;
        if (IsSignInRevoked)
        {
            return EResult(15);
        }

        LastAccessToken = Token(AccountSteamId, Now.AddDays(1), $"access{AccessTokenRequests}");
        return Proto(new ProtoWriter().String(1, LastAccessToken));
    }

    private HttpResponseMessage Owned(string? accessToken)
    {
        OwnedGamesRequests++;
        if (FailsUnexpectedly)
        {
            throw new InvalidOperationException("kaputt");
        }

        if (ListsNoGames)
        {
            return Json("""{"response":{}}""");
        }

        if (accessToken is null || accessToken != LastAccessToken)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var games = OwnedGames.Select(g => new { appid = g.AppId, name = g.Name, playtime_forever = g.Minutes, rtime_last_played = g.LastPlayed });
        return Json(JsonSerializer.Serialize(new { response = new { game_count = OwnedGames.Count, games } }));
    }

    private HttpResponseMessage Profile(string? accessToken, string? steamId)
    {
        if (ProfileFails || accessToken is null || accessToken != LastAccessToken || steamId != AccountSteamId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        return Json(JsonSerializer.Serialize(new { response = new { accounts = new[] { new { public_data = new { steamid = steamId, persona_name = ProfileName } } } } }));
    }

    private HttpResponseMessage Store(string? cookie)
    {
        // The store takes the phone app's access token as its session cookie; without it, every list is empty.
        var signedIn = !StoreFails && LastAccessToken is { } token
            && cookie == $"steamLoginSecure={Uri.EscapeDataString($"{AccountSteamId}||{token}")}";
        var apps = signedIn ? OwnedGames.Select(g => g.AppId).Concat(StoreApps).ToArray() : [];
        return Json(JsonSerializer.Serialize(new { rgWishlist = Array.Empty<uint>(), rgOwnedPackages = Array.Empty<uint>(), rgOwnedApps = apps }));
    }

    private static HttpResponseMessage Proto(ProtoWriter message)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(message.ToArray()) };
        response.Headers.Add("x-eresult", "1");
        return response;
    }

    private static HttpResponseMessage EResult(int result)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        response.Headers.Add("x-eresult", result.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return response;
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
