using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// An app of the account: a game as <c>IPlayerService/GetOwnedGames</c> lists it, with name, playtime and last start, or
/// any other app only Steam's store lists (soundtracks, tools, games the web API leaves out), with no name: Steam's
/// <c>appinfo.vdf</c> names and types it.
/// </summary>
internal sealed record SteamOwnedGame(uint AppId, string? Name, long PlaytimeMinutes, long LastPlayedUnix);

/// <summary>
/// What the store's user data page says the account owns: its apps, and its packages (licenses). Free tools (dedicated servers, SDKs) and
/// soundtracks from free packages are in no app list, only in a package (owner's account, 2026-10-27).
/// </summary>
internal sealed record SteamStoreOwnership(IReadOnlyList<uint> Apps, IReadOnlyList<uint> Packages);

/// <summary>The profile's name and its avatar's hash (hex, as Steam's image server names the file); either may be missing.</summary>
internal sealed record SteamProfile(string? Name, string? AvatarHash);

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
/// Steam's web API for the account (#83): the owned games and the profile name, with the web API token of a signed-in
/// store page (<see cref="SteamStorePage"/>). HTTPS only; every request ends after <see cref="Timeout"/>. Requests and
/// answers hold the token, so nothing of them is logged (<c>docs/launchers/steam/account.md</c>).
/// </summary>
internal sealed class SteamWebApi(HttpClient http)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string Base = "https://api.steampowered.com/";

    private const string AvatarBase = "https://avatars.steamstatic.com/";

    /// <summary>A full avatar is some 10 KB; far more is no avatar.</summary>
    public const int MaxAvatarBytes = 512 * 1024;

    private const int EResultOk = 1;

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
    /// The profile name and the hash of the avatar; either null if Steam doesn't tell. <c>ISteamUser</c> wants a web API
    /// key, this one the access token.
    /// </summary>
    public async Task<SteamApiResult<SteamProfile>> GetProfileAsync(string accessToken, ulong steamId, CancellationToken cancellationToken)
    {
        using var answer = await GetJsonAsync(
            "GetPlayerLinkDetails",
            $"{Base}IPlayerService/GetPlayerLinkDetails/v1/?access_token={Uri.EscapeDataString(accessToken)}&steamids%5B0%5D={steamId}",
            cancellationToken).ConfigureAwait(false);
        if (answer.Json is not { } json)
        {
            return answer.Error!;
        }

        if (!(json.RootElement.TryGetProperty("response", out var response)
            && response.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array
            && accounts.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } account
            && account.TryGetProperty("public_data", out var data) && data.ValueKind == JsonValueKind.Object))
        {
            return SteamApiResult<SteamProfile>.Ok(new SteamProfile(null, null));
        }

        return SteamApiResult<SteamProfile>.Ok(new SteamProfile(Text(data, "persona_name"), AvatarHash(Text(data, "sha_digest_avatar"))));
    }

    /// <summary>
    /// The avatar with the hash <paramref name="avatarHash"/> (184 × 184) from Steam's image server; a JPEG or PNG of at
    /// most <see cref="MaxAvatarBytes"/>, anything else is refused.
    /// </summary>
    public async Task<SteamApiResult<byte[]>> GetAvatarAsync(string avatarHash, CancellationToken cancellationToken)
    {
        if (avatarHash.Length != 40 || !avatarHash.All(char.IsAsciiHexDigitLower))
        {
            return new SteamApiError("Avatar: ungültiger Hash");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{AvatarBase}{avatarHash}_full.jpg");
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new SteamApiError($"Avatar: HTTP {(int)response.StatusCode}", Status: response.StatusCode);
            }

            if (response.Content.Headers.ContentLength > MaxAvatarBytes)
            {
                return new SteamApiError("Avatar: zu groß");
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var picture = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await body.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (picture.Length + read > MaxAvatarBytes)
                {
                    return new SteamApiError("Avatar: zu groß");
                }

                picture.Write(buffer, 0, read);
            }

            var bytes = picture.ToArray();
            return IsJpegOrPng(bytes) ? SteamApiResult<byte[]>.Ok(bytes) : new SteamApiError("Avatar: kein Bild");
        }
        catch (Exception e) when (Failure("Avatar", e, cancellationToken) is { } error)
        {
            return error;
        }
    }

    private static string? Text(JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    /// <summary>Steam gives the avatar's SHA-1 in Base64; its image server names the file by it in hex.</summary>
    private static string? AvatarHash(string? base64)
    {
        if (base64 is null)
        {
            return null;
        }

        var hash = new byte[20];
        return Convert.TryFromBase64String(base64, hash, out var length) && length == hash.Length && hash.Any(b => b != 0)
            ? Convert.ToHexStringLower(hash)
            : null;
    }

    private static bool IsJpegOrPng(byte[] bytes) =>
        bytes is [0xFF, 0xD8, 0xFF, ..] or [0x89, (byte)'P', (byte)'N', (byte)'G', ..];

    /// <param name="method">What the error names: the URL holds the token.</param>
    private async Task<JsonAnswer> GetJsonAsync(string method, string url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Get, url);

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

    /// <summary>A JSON answer to dispose of, or why there is none.</summary>
    private sealed record JsonAnswer(JsonDocument? Json, SteamApiError? Error) : IDisposable
    {
        public void Dispose() => Json?.Dispose();
    }
}
