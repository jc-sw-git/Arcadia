using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>A QR sign-in under way: the code's text, and what the poll needs (never shown, never logged).</summary>
internal sealed record SteamQrSession(ulong ClientId, string ChallengeUrl, byte[] RequestId, TimeSpan Interval);

/// <summary>One poll of a QR sign-in: a new code to show, or the tokens once it is approved.</summary>
/// <param name="AccountName">The account's login name, for the UI only and never logged; the profile name is preferred.</param>
/// <param name="HadRemoteInteraction">The code was scanned on a phone: a session that ends now was rejected there.</param>
internal sealed record SteamPollResult(ulong? NewClientId, string? NewChallengeUrl, string? RefreshToken, string? AccessToken, string? AccountName, bool HadRemoteInteraction = false);

/// <summary>
/// An app of the account: a game as <c>IPlayerService/GetOwnedGames</c> lists it, with name, playtime and last start, or
/// any other app only Steam's store lists (soundtracks, tools, games the web API leaves out), with no name: Steam's
/// <c>appinfo.vdf</c> names and types it.
/// </summary>
internal sealed record SteamOwnedGame(uint AppId, string? Name, long PlaytimeMinutes, long LastPlayedUnix);

/// <summary>
/// What the store says the account owns: its apps, and its packages (licenses). Free tools (dedicated servers, SDKs) and
/// soundtracks from free packages are in no app list, only in a package (owner's account, 2026-10-27).
/// </summary>
internal sealed record SteamStoreOwnership(IReadOnlyList<uint> Apps, IReadOnlyList<uint> Packages);

/// <summary>
/// Why Steam gave no answer: not reachable (<see cref="IsNetwork"/>), cancelled, or an error answer with
/// <see cref="EResult"/> from its <c>x-eresult</c> header or an HTTP status. Errors are values, not exceptions: Steam being
/// away is normal, and a debugger shouldn't stop at it.
/// </summary>
internal sealed record SteamApiError(string Message, int? EResult = null, HttpStatusCode? Status = null, bool IsNetwork = false, bool IsCancelled = false)
{
    // EResult values (Steam's enum): 15 AccessDenied, 26 Revoked, 27 Expired.
    private static readonly HashSet<int> SignInGone = [15, 26, 27];

    // 3 NoConnection, 10 Busy, 16 Timeout, 20 ServiceUnavailable, 84 RateLimitExceeded.
    private static readonly HashSet<int> Temporary = [3, 10, 16, 20, 84];

    /// <summary>
    /// Not reachable, or a hiccup on Steam's side (busy, unavailable, too many requests, an HTTP error, an unusable answer):
    /// trying again may work.
    /// </summary>
    public bool IsTemporary => IsNetwork || (Status is { } status && (int)status is 429 or >= 500)
        || EResult is not { } result || Temporary.Contains(result);

    /// <summary>The stored sign-in no longer counts (expired or revoked): only signing in again helps.</summary>
    public bool IsSignInGone => (EResult is { } result && SignInGone.Contains(result))
        || Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>An answer of Steam's web API: <see cref="Value"/>, or why there is none.</summary>
internal sealed record SteamApiResult<T>(T? Value, SteamApiError? Error)
{
    public static implicit operator SteamApiResult<T>(SteamApiError error) => new(default, error);

    public static SteamApiResult<T> Ok(T value) => new(value, null);
}

/// <summary>
/// Steam's web API for the account (#83): the QR sign-in of <c>IAuthenticationService</c> (protobuf in, protobuf out),
/// new access tokens, the owned games, the profile name (JSON) and the store's list of owned apps. HTTPS only; every
/// request ends after <see cref="Timeout"/>. Requests and answers hold tokens, so nothing of them is logged. Arcadia signs
/// in as Steam's phone app does: only that kind of sign-in may get new access tokens over the web API, and its access
/// token is also the store's session cookie (<c>docs/launchers/steam.md</c>).
/// </summary>
internal sealed class SteamWebApi(HttpClient http)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string Base = "https://api.steampowered.com/";

    private const string StoreUserData = "https://store.steampowered.com/dynamicstore/userdata/";

    // EAuthTokenPlatformType.MobileApp, EOSType.AndroidUnknown; the device type is what Steam's app sends.
    private const int MobileAppPlatform = 3;
    private const int AndroidOs = -500;
    private const int PhoneDeviceType = 528;

    /// <summary>What the approval on the phone names as the device asking.</summary>
    private const string DeviceName = "Arcadia";

    private const int EResultOk = 1;

    public async Task<SteamApiResult<SteamQrSession>> BeginQrSessionAsync(CancellationToken cancellationToken)
    {
        var request = new ProtoWriter()
            .String(1, DeviceName)
            .UInt(2, MobileAppPlatform)
            .Message(3, new ProtoWriter()
                .String(1, DeviceName)
                .UInt(2, MobileAppPlatform)
                .Int32(3, AndroidOs)
                .UInt(4, PhoneDeviceType))
            .String(4, "Mobile");
        var answer = await CallAuthenticationAsync("BeginAuthSessionViaQR", request, cancellationToken).ConfigureAwait(false);
        if (answer.Value is not { } response)
        {
            return answer.Error!;
        }

        if (response.UInt(1) is not { } clientId || response.String(2) is not { Length: > 0 } url || response.Bytes(3) is not { Length: > 0 } requestId)
        {
            return new SteamApiError("BeginAuthSessionViaQR: Antwort unvollständig");
        }

        var interval = response.Float(4) is > 0 and < 60 and var seconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromSeconds(5);
        return SteamApiResult<SteamQrSession>.Ok(new SteamQrSession(clientId, url, requestId, interval));
    }

    public async Task<SteamApiResult<SteamPollResult>> PollAsync(ulong clientId, byte[] requestId, CancellationToken cancellationToken)
    {
        var request = new ProtoWriter().UInt(1, clientId).Bytes(2, requestId);
        var answer = await CallAuthenticationAsync("PollAuthSessionStatus", request, cancellationToken).ConfigureAwait(false);
        return answer.Value is { } response
            ? SteamApiResult<SteamPollResult>.Ok(new SteamPollResult(
                response.UInt(1), Empty(response.String(2)), Empty(response.String(3)), Empty(response.String(4)), Empty(response.String(6)), response.UInt(5) is > 0))
            : answer.Error!;
    }

    /// <summary>A new access token from the refresh token; it lives about a day.</summary>
    public async Task<SteamApiResult<string>> GenerateAccessTokenAsync(string refreshToken, ulong steamId, CancellationToken cancellationToken)
    {
        var request = new ProtoWriter().String(1, refreshToken).Fixed64(2, steamId);
        var answer = await CallAuthenticationAsync("GenerateAccessTokenForApp", request, cancellationToken).ConfigureAwait(false);
        return answer.Value is not { } response ? answer.Error!
            : Empty(response.String(1)) is { } token ? SteamApiResult<string>.Ok(token)
            : new SteamApiError("GenerateAccessTokenForApp: kein Token");
    }

    /// <summary>
    /// The account's games with their names, playtime and last start; free games the account added and played
    /// free-to-play games included, family-shared games, soundtracks and tools not.
    /// </summary>
    public async Task<SteamApiResult<IReadOnlyList<SteamOwnedGame>>> GetOwnedGamesAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        using var answer = await GetJsonAsync(
            "GetOwnedGames",
            $"{Base}IPlayerService/GetOwnedGames/v1/?access_token={Uri.EscapeDataString(accessToken)}&steamid={steamId}"
                + "&include_appinfo=true&include_played_free_games=true&include_free_sub=true&skip_unvetted_apps=false",
            cookie: null,
            cancellationToken).ConfigureAwait(false);
        if (answer.Json is not { } json)
        {
            return answer.Error!;
        }

        if (!json.RootElement.TryGetProperty("response", out var response) || !response.TryGetProperty("games", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            // Steam answers {"response":{}} also when it doesn't list the games right now: the cached list stays.
            return new SteamApiError("GetOwnedGames: Antwort ohne Spiele");
        }

        var games = new List<SteamOwnedGame>();
        foreach (var game in list.EnumerateArray())
        {
            if (game.TryGetProperty("appid", out var id) && id.TryGetUInt32(out var appId)
                && game.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } title)
            {
                games.Add(new SteamOwnedGame(appId, title, Number(game, "playtime_forever"), Number(game, "rtime_last_played")));
            }
        }

        return SteamApiResult<IReadOnlyList<SteamOwnedGame>>.Ok(games);
    }

    /// <summary>
    /// The ids of every app the account owns as Steam's store lists them (<c>rgOwnedApps</c>): games, soundtracks, tools,
    /// DLC. The phone app's access token is the store's session cookie. An empty list means the store didn't take the
    /// sign-in, so it is an error.
    /// </summary>
    public async Task<SteamApiResult<SteamStoreOwnership>> GetOwnedAppsAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        var cookie = $"steamLoginSecure={Uri.EscapeDataString($"{steamId}||{accessToken}")}";
        using var answer = await GetJsonAsync("dynamicstore/userdata", StoreUserData, cookie, cancellationToken).ConfigureAwait(false);
        if (answer.Json is not { } json)
        {
            return answer.Error!;
        }

        var apps = Ids(json.RootElement, "rgOwnedApps");
        return apps.Count > 0
            ? SteamApiResult<SteamStoreOwnership>.Ok(new SteamStoreOwnership(apps, Ids(json.RootElement, "rgOwnedPackages")))
            : new SteamApiError("dynamicstore/userdata: keine Apps");
    }

    private static List<uint> Ids(JsonElement root, string property)
    {
        var ids = new List<uint>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.TryGetUInt32(out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>The profile name; null if Steam doesn't tell. <c>ISteamUser</c> wants a web API key, this one the access token.</summary>
    public async Task<SteamApiResult<string?>> GetProfileNameAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        using var answer = await GetJsonAsync(
            "GetPlayerLinkDetails",
            $"{Base}IPlayerService/GetPlayerLinkDetails/v1/?access_token={Uri.EscapeDataString(accessToken)}&steamids%5B0%5D={steamId}",
            cookie: null,
            cancellationToken).ConfigureAwait(false);
        if (answer.Json is not { } json)
        {
            return answer.Error!;
        }

        return SteamApiResult<string?>.Ok(
            json.RootElement.TryGetProperty("response", out var response)
            && response.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array
            && accounts.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } account
            && account.TryGetProperty("public_data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("persona_name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { Length: > 0 } text
                ? text
                : null);
    }

    private async Task<SteamApiResult<ProtoMessage>> CallAuthenticationAsync(string method, ProtoWriter request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Base}IAuthenticationService/{method}/v1/")
        {
            Content = new FormUrlEncodedContent([new("input_protobuf_encoded", Convert.ToBase64String(request.ToArray()))]),
        };
        message.Headers.TryAddWithoutValidation("User-Agent", "okhttp/4.9.2");
        message.Headers.TryAddWithoutValidation("Cookie", "mobileClient=android; mobileClientVersion=777777 3.10.3");
        try
        {
            using var response = await http.SendAsync(message, timeout.Token).ConfigureAwait(false);
            if (Check(method, response) is { } error)
            {
                return error;
            }

            return SteamApiResult<ProtoMessage>.Ok(ProtoMessage.Parse(await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false)));
        }
        catch (Exception e) when (Failure(method, e, cancellationToken) is { } error)
        {
            return error;
        }
    }

    /// <param name="method">What the error names: the URL holds the token.</param>
    private async Task<JsonAnswer> GetJsonAsync(string method, string url, string? cookie, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        if (cookie is not null)
        {
            message.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        try
        {
            using var response = await http.SendAsync(message, timeout.Token).ConfigureAwait(false);
            if (Check(method, response) is { } error)
            {
                return new JsonAnswer(null, error);
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return new JsonAnswer(await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false), null);
        }
        catch (Exception e) when (Failure(method, e, cancellationToken) is { } error)
        {
            return new JsonAnswer(null, error);
        }
    }

    /// <summary>
    /// Steam not reachable, the request timed out or was cancelled, or the answer unusable, as an error; null for anything
    /// else, which is a bug and stays an exception.
    /// </summary>
    private static SteamApiError? Failure(string method, Exception e, CancellationToken cancellationToken) => e switch
    {
        OperationCanceledException when cancellationToken.IsCancellationRequested => new SteamApiError($"{method}: abgebrochen", IsCancelled: true),
        OperationCanceledException => new SteamApiError($"{method}: Zeitüberschreitung", IsNetwork: true),
        HttpRequestException or IOException => new SteamApiError($"{method}: nicht erreichbar ({e.GetType().Name})", IsNetwork: true),
        FormatException or JsonException => new SteamApiError($"{method}: Antwort unbrauchbar ({e.Message})"),
        _ => null,
    };

    private static SteamApiError? Check(string method, HttpResponseMessage response)
    {
        int? eResult = response.Headers.TryGetValues("x-eresult", out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
        if (!response.IsSuccessStatusCode)
        {
            return new SteamApiError($"{method}: HTTP {(int)response.StatusCode}", eResult, response.StatusCode);
        }

        return eResult is { } error and not EResultOk ? new SteamApiError($"{method}: EResult {error}", error, response.StatusCode) : null;
    }

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;

    private static string? Empty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    /// <summary>A JSON answer to dispose of, or why there is none.</summary>
    private sealed record JsonAnswer(JsonDocument? Json, SteamApiError? Error) : IDisposable
    {
        public void Dispose() => Json?.Dispose();
    }
}
