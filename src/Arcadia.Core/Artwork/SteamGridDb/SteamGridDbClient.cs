using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>What SteamGridDB answered (#489).</summary>
public enum SteamGridDbOutcome
{
    /// <summary>It knows the game; <see cref="SteamGridDbAnswer.Images"/> may still be empty.</summary>
    Found,

    /// <summary>It doesn't know the game by this id.</summary>
    NotFound,

    /// <summary>It turned the key down (HTTP 401 or 403).</summary>
    KeyRejected,

    /// <summary>No network, no answer within <see cref="SteamGridDbClient.Timeout"/>, or an answer it can't be read from.</summary>
    NoConnection,
}

/// <summary>One image SteamGridDB offers, with its players' rating.</summary>
public sealed record SteamGridDbImage(Uri Url, int Width, int Height, int Score);

public sealed record SteamGridDbAnswer(SteamGridDbOutcome Outcome, IReadOnlyList<SteamGridDbImage> Images)
{
    public static SteamGridDbAnswer Of(SteamGridDbOutcome outcome) => new(outcome, []);
}

/// <summary>A game SteamGridDB found by a name (#491): its own id, its name there and the year it came out, if known.</summary>
public sealed record SteamGridDbHit(int Id, string Name, int? Year);

/// <summary>What a search by name answered (#491); <see cref="Hits"/> in SteamGridDB's order.</summary>
public sealed record SteamGridDbSearch(SteamGridDbOutcome Outcome, IReadOnlyList<SteamGridDbHit> Hits);

/// <summary>
/// SteamGridDB's API v2 (#489): a game's covers (2 : 3 grids) and backgrounds (heroes) by its store id or SteamGridDB's own
/// id, best rated first, the search by name (#491) and a check of the key. Sends the key and the store id or the name,
/// nothing else (#13, privacy); the key goes only to
/// <see cref="Api"/>, never into a log line or an image download. Never throws for a network failure.
/// </summary>
/// <remarks>
/// SteamGridDB slowing Arcadia down (HTTP 429) is waited out, as long as it asks (<c>Retry-After</c>, at most
/// <see cref="LongestWait"/> per wait), then the request goes again.
/// </remarks>
public sealed class SteamGridDbClient(HttpClient http, TimeProvider time)
{
    public static readonly Uri Api = new("https://www.steamgriddb.com/api/v2/");

    /// <summary>"Schlüssel-Seite öffnen": where the player makes a key.</summary>
    public static readonly Uri KeyPage = new("https://www.steamgriddb.com/profile/preferences/api");

    /// <summary>The longest one request may take (#13), the key check included.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>A throttled request waits this long when SteamGridDB names no time.</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(60);

    private const int MaxThrottled = 5;

    // Covers only in 2 : 3 (SteamGridDB's grids are also wide and square), no animations, nothing marked NSFW or humour.
    private const string CoverQuery = "dimensions=600x900,342x482,660x930&types=static&nsfw=false&humor=false";
    private const string HeroQuery = "types=static&nsfw=false&humor=false";

    // A game that answers for a check: any game proves the key; Steam's AppID 220 is a long-lived one.
    private static readonly ArtworkStoreId CheckGame = new(ArtworkStoreId.Steam, "220");

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>The game's images of <paramref name="kind"/>, best rated first.</summary>
    public async Task<SteamGridDbAnswer> ImagesAsync(string key, ArtworkStoreId game, ArtworkKind kind, CancellationToken cancel) =>
        AddressOf(game, kind) is { } address ? await ImagesAtAsync(key, address, cancel) : SteamGridDbAnswer.Of(SteamGridDbOutcome.NotFound);

    /// <summary>The images of <paramref name="kind"/> of a game by SteamGridDB's own id (#491: found by its name).</summary>
    public async Task<SteamGridDbAnswer> ImagesOfGameAsync(string key, int gameId, ArtworkKind kind, CancellationToken cancel) =>
        AddressOf(gameId, kind) is { } address ? await ImagesAtAsync(key, address, cancel) : SteamGridDbAnswer.Of(SteamGridDbOutcome.NotFound);

    /// <summary>
    /// The games SteamGridDB finds by <paramref name="name"/> (#491), as its search ranks them; sends the name and the key,
    /// nothing else. <see cref="SteamGridDbOutcome.NotFound"/> for a name that can't be searched (empty or too long).
    /// </summary>
    public async Task<SteamGridDbSearch> SearchAsync(string key, string name, CancellationToken cancel)
    {
        if (SearchAddressOf(name) is not { } address)
        {
            return new SteamGridDbSearch(SteamGridDbOutcome.NotFound, []);
        }

        var (outcome, body) = await SendAsync(key, address, cancel);
        return outcome != SteamGridDbOutcome.Found ? new SteamGridDbSearch(outcome, [])
            : ReadHits(body!) is { } hits ? new SteamGridDbSearch(SteamGridDbOutcome.Found, hits)
            : new SteamGridDbSearch(SteamGridDbOutcome.NoConnection, []);
    }

    /// <summary>"Verbindung testen": asks once whether the key holds.</summary>
    public async Task<SteamGridDbOutcome> CheckAsync(string key, CancellationToken cancel) =>
        (await SendAsync(key, AddressOf(CheckGame, ArtworkKind.Hero)!, cancel)).Outcome switch
        {
            SteamGridDbOutcome.NotFound => SteamGridDbOutcome.Found,
            var outcome => outcome,
        };

    /// <summary>
    /// The request's address; null for a store or id that can't be one (a store must be a known word, the id is escaped),
    /// so nothing from a launcher's files can reach another path of the API.
    /// </summary>
    internal static Uri? AddressOf(ArtworkStoreId game, ArtworkKind kind) =>
        game.Store is not (ArtworkStoreId.Steam or ArtworkStoreId.Epic) || game.Id.Length is 0 or > 200 || IsDotSegment(game.Id)
            ? null
            : ImagesAddress(game.Store, Uri.EscapeDataString(game.Id), kind);

    /// <summary>The address of a game's images by SteamGridDB's own id; null for an id that can't be one.</summary>
    internal static Uri? AddressOf(int gameId, ArtworkKind kind) =>
        gameId <= 0 ? null : ImagesAddress("game", gameId.ToString(CultureInfo.InvariantCulture), kind);

    /// <summary>The search's address; the name is escaped as one path segment, so it can't reach another path of the API.</summary>
    internal static Uri? SearchAddressOf(string name)
    {
        var term = name.Trim();
        return term.Length is 0 or > MaxNameLength || IsDotSegment(term) ? null : new Uri(Api, "search/autocomplete/" + Uri.EscapeDataString(term));
    }

    /// <summary>Only dots: escaping leaves them, and an address resolves "." and ".." to another path of the API.</summary>
    private static bool IsDotSegment(string segment) => segment.Trim('.').Length == 0;

    /// <summary>A game's name longer than this isn't searched: no real title is, and the address stays short.</summary>
    public const int MaxNameLength = 200;

    private static Uri ImagesAddress(string platform, string escapedId, ArtworkKind kind)
    {
        var path = kind == ArtworkKind.Cover ? "grids" : "heroes";
        var query = kind == ArtworkKind.Cover ? CoverQuery : HeroQuery;
        return new Uri(Api, $"{path}/{platform}/{escapedId}?{query}");
    }

    private async Task<SteamGridDbAnswer> ImagesAtAsync(string key, Uri address, CancellationToken cancel)
    {
        var (outcome, body) = await SendAsync(key, address, cancel);
        return outcome != SteamGridDbOutcome.Found ? SteamGridDbAnswer.Of(outcome)
            : Read(body!) is { } images ? new SteamGridDbAnswer(SteamGridDbOutcome.Found, images)
            : SteamGridDbAnswer.Of(SteamGridDbOutcome.NoConnection);
    }

    /// <summary>One request; the body only with <see cref="SteamGridDbOutcome.Found"/>.</summary>
    private async Task<(SteamGridDbOutcome Outcome, byte[]? Body)> SendAsync(string key, Uri address, CancellationToken cancel)
    {
        for (var throttled = 0; ; throttled++)
        {
            using var timeout = new CancellationTokenSource(Timeout, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, address);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                        Log("SteamGridDB: Schlüssel abgelehnt");
                        return (SteamGridDbOutcome.KeyRejected, null);
                    case HttpStatusCode.NotFound:
                        return (SteamGridDbOutcome.NotFound, null);
                    case HttpStatusCode.TooManyRequests when throttled < MaxThrottled:
                        var wait = WaitOf(response);
                        Log($"SteamGridDB: gebremst, wartet {wait.TotalSeconds:0} s");
                        await Task.Delay(wait, time, cancel);
                        continue;
                    case var status when !response.IsSuccessStatusCode:
                        Log($"SteamGridDB: antwortet {(int)status}");
                        return (SteamGridDbOutcome.NoConnection, null);
                }

                return (SteamGridDbOutcome.Found, await response.Content.ReadAsByteArrayAsync(linked.Token));
            }
            catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
            {
                Log($"SteamGridDB: keine Antwort in {Timeout.TotalSeconds:0} s");
                return (SteamGridDbOutcome.NoConnection, null);
            }
            catch (HttpRequestException e)
            {
                Log($"SteamGridDB: keine Verbindung ({e.HttpRequestError})");
                return (SteamGridDbOutcome.NoConnection, null);
            }
            catch (IOException e)
            {
                Log($"SteamGridDB: Verbindung abgebrochen ({e.GetType().Name})");
                return (SteamGridDbOutcome.NoConnection, null);
            }
        }
    }

    private static TimeSpan WaitOf(HttpResponseMessage response)
    {
        var asked = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => DefaultWait,
        };
        return asked < TimeSpan.Zero ? TimeSpan.Zero : asked > LongestWait ? LongestWait : asked;
    }

    /// <summary>
    /// The images of an answer <c>{"success":true,"data":[{"url":…,"width":…,"height":…,"score":…}]}</c>, best rated first;
    /// an image without an https address or a size is left out. Null if the answer isn't such JSON.
    /// </summary>
    internal static IReadOnlyList<SteamGridDbImage>? Read(byte[] body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var images = new List<SteamGridDbImage>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
                    && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps
                    && Number(item, "width") is > 0 and var width
                    && Number(item, "height") is > 0 and var height)
                {
                    images.Add(new SteamGridDbImage(address, width, height, Number(item, "score")));
                }
            }

            // Stable: alike ratings keep SteamGridDB's order.
            return [.. images.OrderByDescending(i => i.Score)];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The games of a search's answer <c>{"success":true,"data":[{"id":…,"name":…,"release_date":…}]}</c>, in its order; a
    /// game without an id or a name is left out, at most <see cref="MaxHits"/>. Null if the answer isn't such JSON.
    /// </summary>
    internal static IReadOnlyList<SteamGridDbHit>? ReadHits(byte[] body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var hits = new List<SteamGridDbHit>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && Number(item, "id") is > 0 and var id
                    && item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && name.GetString()!.Trim() is { Length: > 0 and <= MaxNameLength } title
                    && hits.All(h => h.Id != id))
                {
                    hits.Add(new SteamGridDbHit(id, title, YearOf(item)));
                }

                if (hits.Count == MaxHits)
                {
                    break;
                }
            }

            return hits;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The most games a search offers (#491): two rows of the waiting list.</summary>
    public const int MaxHits = 8;

    // release_date is seconds since 1970; SteamGridDB leaves it out or sends 0 for a game without one.
    private static int? YearOf(JsonElement item) =>
        item.TryGetProperty("release_date", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds)
            && seconds is > 0 and < 32503680000
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).Year
            : null;

    private static int Number(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    /// <summary>For log lines: never the key, only its last four characters.</summary>
    public static string Ending(string key) => key.Length >= 4 ? key[^4..].ToString(CultureInfo.InvariantCulture) : string.Empty;
}
