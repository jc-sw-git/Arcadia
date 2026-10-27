using System.Net;
using System.Text;
using System.Text.Json;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam.Account;

namespace Arcadia.Core.Tests;

/// <summary>
/// Steam as the account sees it (#83): its store pages in Arcadia's browser (<see cref="IWebPages"/>: the login page, the
/// store page with the web API token, the user data page, the cookies) and its web API (the owned games, the profile
/// name). A test signs the user in on the shown page, takes Steam offline or lets the browser's sign-in end. Shared with
/// the acceptance tests.
/// </summary>
internal sealed class FakeSteamWeb : HttpMessageHandler, IWebPages
{
    public const ulong SteamId = 76561197960265728 + 100_000_001;

    public const string ProfileName = "Spieler Eins";

    private readonly Lock _lock = new();
    private bool _browserSignedIn;
    private int _tokens;

    public FakeSteamWeb(DateTimeOffset now) => Now = now;

    /// <summary>The clock the issued tokens expire by.</summary>
    public DateTimeOffset Now { get; set; }

    public ulong AccountSteamId { get; set; } = SteamId;

    /// <summary>Every request and page fails as if the PC were offline.</summary>
    public bool IsOffline { get; set; }

    /// <summary>The user signs in on the shown login page (now, or as soon as it shows).</summary>
    public bool IsApproved { get; set; }

    /// <summary>The app ids Steam's store lists as owned, besides <see cref="OwnedGames"/>: soundtracks, tools, DLC.</summary>
    public List<uint> StoreApps { get; } = [];

    /// <summary>The package ids (licenses) Steam's store lists as owned.</summary>
    public List<uint> StorePackages { get; } = [];

    /// <summary>The user data page fails to load.</summary>
    public bool StoreFails { get; set; }

    /// <summary>The profile name can't be loaded.</summary>
    public bool ProfileFails { get; set; }

    /// <summary>The owned games come back as <c>{"response":{}}</c>.</summary>
    public bool ListsNoGames { get; set; }

    /// <summary>The owned games' request throws something no transport would: a bug in Arcadia's own code.</summary>
    public bool FailsUnexpectedly { get; set; }

    /// <summary>Steam ended the browser's sign-in (e.g. the password was changed): the store page is signed out.</summary>
    public bool IsSignInRevoked { get; set; }

    public List<(uint AppId, string Name, long Minutes, long LastPlayed)> OwnedGames { get; } = [];

    public int OwnedGamesRequests { get; private set; }

    /// <summary>How often the store page was read for a new token.</summary>
    public int StorePageReads { get; private set; }

    /// <summary>How often the login page was shown.</summary>
    public int SignInPagesShown { get; private set; }

    /// <summary>How often Arcadia deleted Steam's cookies.</summary>
    public int CookiesForgotten { get; private set; }

    /// <summary>The login page is open right now.</summary>
    public bool IsSignInPageShown { get; private set; }

    /// <summary>The token issued last; the tests look for it in the log.</summary>
    public string? LastAccessToken { get; private set; }

    public static string Token(ulong steamId, DateTimeOffset expires, string kind)
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Part("""{"typ":"JWT","alg":"EdDSA"}""")}.{Part($$"""{"sub":"{{steamId}}","exp":{{expires.ToUnixTimeSeconds()}},"kind":"{{kind}}"}""")}.c2lnbmF0dXJl";
    }

    public async Task<WebPageResult> ShowAsync(string title, Uri page, string script, Func<string, bool> isDone, CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            SignInPagesShown++;
            IsSignInPageShown = true;
        }

        try
        {
            if (IsOffline)
            {
                return WebPageResult.Failed("offline");
            }

            Assert.Equal(SteamStorePage.LoginPage, page);
            while (!IsApproved && !_browserSignedIn)
            {
                await Task.Delay(1, cancellationToken);
            }

            lock (_lock)
            {
                _browserSignedIn = true;
                var text = StorePage(script);
                Assert.True(isDone(text));
                return WebPageResult.Done(text);
            }
        }
        catch (OperationCanceledException)
        {
            return WebPageResult.Cancelled;
        }
        finally
        {
            IsSignInPageShown = false;
        }
    }

    public async Task<WebPageResult> ReadAsync(Uri page, string script, Func<string, bool> isDone, CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (IsOffline)
        {
            return WebPageResult.Failed("offline");
        }

        lock (_lock)
        {
            if (page == SteamStorePage.StorePage)
            {
                StorePageReads++;
                return WebPageResult.Done(StorePage(script));
            }

            Assert.Equal(SteamStorePage.UserDataPage, page);
            Assert.Equal(SteamStorePage.TextScript, script);
            return StoreFails ? WebPageResult.Failed("Seite nicht geladen") : WebPageResult.Done(UserData());
        }
    }

    public async Task<WebPageResult> ForgetAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken)
    {
        await Task.Yield();
        lock (_lock)
        {
            Assert.Contains("steampowered.com", domains);
            CookiesForgotten++;
            _browserSignedIn = false;
            return WebPageResult.Done(string.Empty);
        }
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
                ("api.steampowered.com", "/IPlayerService/GetOwnedGames/v1/") => Owned(query["access_token"]),
                ("api.steampowered.com", "/IPlayerService/GetPlayerLinkDetails/v1/") => Profile(query["access_token"], query["steamids[0]"]),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }
    }

    /// <summary>
    /// What <see cref="SteamStorePage.AccountScript"/> returns on a store page: <c>#application_config</c>'s attributes,
    /// with a new token while the browser is signed in. Like Steam, the attributes are JSON inside the JSON.
    /// </summary>
    private string StorePage(string script)
    {
        Assert.Equal(SteamStorePage.AccountScript, script);
        var signedIn = _browserSignedIn && !IsSignInRevoked;
        if (signedIn)
        {
            LastAccessToken = Token(AccountSteamId, Now.AddDays(1), $"access{++_tokens}");
        }

        var user = signedIn
            ? JsonSerializer.Serialize(new { logged_in = true, steamid = AccountSteamId.ToString(System.Globalization.CultureInfo.InvariantCulture), country_code = "DE" })
            : JsonSerializer.Serialize(new { logged_in = false, country_code = "DE" });
        var config = signedIn
            ? JsonSerializer.Serialize(new { webapi_token = LastAccessToken, store_base_url = "https://store.steampowered.com/" })
            : JsonSerializer.Serialize(new { store_base_url = "https://store.steampowered.com/" });
        return JsonSerializer.Serialize(new { user, config });
    }

    private string UserData()
    {
        // Signed out, the store answers with empty lists.
        var signedIn = _browserSignedIn && !IsSignInRevoked;
        var apps = signedIn ? OwnedGames.Select(g => g.AppId).Concat(StoreApps).ToArray() : [];
        var packages = signedIn ? StorePackages.ToArray() : [];
        return JsonSerializer.Serialize(new { rgWishlist = Array.Empty<uint>(), rgOwnedPackages = packages, rgOwnedApps = apps });
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

        if (accessToken is null || accessToken != LastAccessToken || IsSignInRevoked)
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

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
