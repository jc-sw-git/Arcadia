using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>A QR sign-in under way: the code's text, and what the poll needs (never shown, never logged).</summary>
internal sealed record SteamQrSession(ulong ClientId, string ChallengeUrl, byte[] RequestId, TimeSpan Interval);

/// <summary>One poll of a QR sign-in: a new code to show, or the tokens once it is approved.</summary>
/// <param name="AccountName">The account's login name, for the UI only and never logged; the profile name is preferred.</param>
internal sealed record SteamPollResult(ulong? NewClientId, string? NewChallengeUrl, string? RefreshToken, string? AccessToken, string? AccountName);

/// <summary>A game of the account as <c>IPlayerService/GetOwnedGames</c> lists it.</summary>
internal sealed record SteamOwnedGame(uint AppId, string Name, long PlaytimeMinutes, long LastPlayedUnix);

/// <summary>
/// Steam answered, but with an error: <see cref="EResult"/> from its <c>x-eresult</c> header, or an HTTP status.
/// </summary>
internal sealed class SteamApiException(string message, int? eResult, HttpStatusCode? status) : Exception(message)
{
    // EResult values (Steam's enum): 15 AccessDenied, 26 Revoked, 27 Expired.
    private static readonly HashSet<int> SignInGone = [15, 26, 27];

    public int? EResult { get; } = eResult;

    public HttpStatusCode? Status { get; } = status;

    /// <summary>The stored sign-in no longer counts (expired or revoked): only signing in again helps.</summary>
    public bool IsSignInGone => EResult is { } result && SignInGone.Contains(result)
        || Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>
/// Steam's web API for the account (#83): the QR sign-in of <c>IAuthenticationService</c> (protobuf in, protobuf out),
/// new access tokens, the owned games and the profile name (JSON). HTTPS only; every request ends after
/// <see cref="Timeout"/>. Requests and answers hold tokens, so nothing of them is logged. Arcadia signs in as Steam's
/// phone app does: only that kind of sign-in may get new access tokens over the web API (<c>docs/launchers/steam.md</c>).
/// </summary>
internal sealed class SteamWebApi(HttpClient http)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string Base = "https://api.steampowered.com/";

    // EAuthTokenPlatformType.MobileApp, EOSType.AndroidUnknown; the device type is what Steam's app sends.
    private const int MobileAppPlatform = 3;
    private const int AndroidOs = -500;
    private const int PhoneDeviceType = 528;

    /// <summary>What the approval on the phone names as the device asking.</summary>
    private const string DeviceName = "Arcadia";

    private const int EResultOk = 1;

    public async Task<SteamQrSession> BeginQrSessionAsync(CancellationToken cancellationToken)
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
        var response = await CallAuthenticationAsync("BeginAuthSessionViaQR", request, cancellationToken).ConfigureAwait(false);
        if (response.UInt(1) is not { } clientId || response.String(2) is not { Length: > 0 } url || response.Bytes(3) is not { Length: > 0 } requestId)
        {
            throw new SteamApiException("BeginAuthSessionViaQR: Antwort unvollständig", null, null);
        }

        var interval = response.Float(4) is > 0 and < 60 and var seconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromSeconds(5);
        return new SteamQrSession(clientId, url, requestId, interval);
    }

    public async Task<SteamPollResult> PollAsync(ulong clientId, byte[] requestId, CancellationToken cancellationToken)
    {
        var request = new ProtoWriter().UInt(1, clientId).Bytes(2, requestId);
        var response = await CallAuthenticationAsync("PollAuthSessionStatus", request, cancellationToken).ConfigureAwait(false);
        return new SteamPollResult(response.UInt(1), Empty(response.String(2)), Empty(response.String(3)), Empty(response.String(4)), Empty(response.String(6)));
    }

    /// <summary>A new access token from the refresh token; it lives about a day.</summary>
    public async Task<string> GenerateAccessTokenAsync(string refreshToken, ulong steamId, CancellationToken cancellationToken)
    {
        var request = new ProtoWriter().String(1, refreshToken).Fixed64(2, steamId);
        var response = await CallAuthenticationAsync("GenerateAccessTokenForApp", request, cancellationToken).ConfigureAwait(false);
        return Empty(response.String(1)) ?? throw new SteamApiException("GenerateAccessTokenForApp: kein Token", null, null);
    }

    /// <summary>
    /// The account's games with their names, playtime and last start; free games the account added and played
    /// free-to-play games included, family-shared games not.
    /// </summary>
    public async Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync(
            $"IPlayerService/GetOwnedGames/v1/?access_token={Uri.EscapeDataString(accessToken)}&steamid={steamId}"
                + "&include_appinfo=true&include_played_free_games=true&include_free_sub=true&skip_unvetted_apps=false",
            cancellationToken).ConfigureAwait(false);
        var games = new List<SteamOwnedGame>();
        if (json.RootElement.TryGetProperty("response", out var response) && response.TryGetProperty("games", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var game in list.EnumerateArray())
            {
                if (game.TryGetProperty("appid", out var id) && id.TryGetUInt32(out var appId)
                    && game.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } title)
                {
                    games.Add(new SteamOwnedGame(appId, title, Number(game, "playtime_forever"), Number(game, "rtime_last_played")));
                }
            }
        }
        else if (!json.RootElement.TryGetProperty("response", out _))
        {
            throw new SteamApiException("GetOwnedGames: Antwort ohne response", null, null);
        }

        return games;
    }

    /// <summary>The profile name; null if Steam doesn't tell.</summary>
    public async Task<string?> GetProfileNameAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync($"ISteamUser/GetPlayerSummaries/v2/?access_token={Uri.EscapeDataString(accessToken)}&steamids={steamId}", cancellationToken).ConfigureAwait(false);
        return json.RootElement.TryGetProperty("response", out var response)
            && response.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Array
            && players.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } player
            && player.TryGetProperty("personaname", out var name) && name.GetString() is { Length: > 0 } text
                ? text
                : null;
    }

    private async Task<ProtoMessage> CallAuthenticationAsync(string method, ProtoWriter request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Base}IAuthenticationService/{method}/v1/")
        {
            Content = new FormUrlEncodedContent([new("input_protobuf_encoded", Convert.ToBase64String(request.ToArray()))]),
        };
        message.Headers.TryAddWithoutValidation("User-Agent", "okhttp/4.9.2");
        message.Headers.TryAddWithoutValidation("Cookie", "mobileClient=android; mobileClientVersion=777777 3.10.3");
        using var response = await http.SendAsync(message, timeout.Token).ConfigureAwait(false);
        Check(method, response);
        return ProtoMessage.Parse(await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false));
    }

    private async Task<JsonDocument> GetJsonAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var response = await http.GetAsync(Base + pathAndQuery, timeout.Token).ConfigureAwait(false);
        // The path holds the token: only the method goes into the message.
        Check(pathAndQuery[..pathAndQuery.IndexOf('?', StringComparison.Ordinal)], response);
        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            return await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        catch (JsonException e)
        {
            throw new SteamApiException($"{pathAndQuery[..pathAndQuery.IndexOf('/', StringComparison.Ordinal)]}: Antwort kein JSON ({e.Message})", null, null);
        }
    }

    private static void Check(string method, HttpResponseMessage response)
    {
        int? eResult = response.Headers.TryGetValues("x-eresult", out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
        if (!response.IsSuccessStatusCode)
        {
            throw new SteamApiException($"{method}: HTTP {(int)response.StatusCode}", eResult, response.StatusCode);
        }

        if (eResult is { } error and not EResultOk)
        {
            throw new SteamApiException($"{method}: EResult {error}", error, response.StatusCode);
        }
    }

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;

    private static string? Empty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
