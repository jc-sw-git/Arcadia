using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>A Twitch access token for IGDB; kept in memory only.</summary>
public sealed record IgdbToken(string Value);

/// <summary>
/// Twitch's token service and IGDB's API v4 (#494). Sends the client id and secret only to <see cref="TokenAddress"/> (in
/// the body of a POST), the client id and token only in IGDB's headers, and to IGDB only a Steam AppID, an IGDB id or a
/// game's name, nothing else; no cookies (the App's client). At most <see cref="Limit"/> requests a second to IGDB, 10 s each;
/// HTTP 429 waits and goes on. Never throws for a network failure, never logs a value of the access.
/// </summary>
public sealed class IgdbClient(HttpClient http, TimeProvider time)
{
    public static readonly Uri TokenAddress = new("https://id.twitch.tv/oauth2/token");

    public static readonly Uri Api = new("https://api.igdb.com/v4/");

    /// <summary>"Zugang anlegen": Twitch's developer console, where the player registers an application.</summary>
    public static readonly Uri ConsolePage = new("https://dev.twitch.tv/console/apps");

    /// <summary>IGDB's limit: 4 requests per second.</summary>
    public static readonly global::Arcadia.Launchers.Abstractions.RequestLimit Limit = new(4, TimeSpan.FromSeconds(1));

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>A throttled request waits this long when IGDB names no time.</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(60);

    private const int MaxThrottled = 10;

    /// <summary>The largest answer read; 50 games with their fields are well below.</summary>
    public const int MaxAnswer = 2 * 1024 * 1024;

    private readonly SemaphoreSlim _pace = new(1, 1);
    private DateTimeOffset _next;

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>A new token for the access; <see cref="IgdbOutcome.Rejected"/> when Twitch turns it down.</summary>
    public async Task<(IgdbOutcome Outcome, IgdbToken? Token)> TokenAsync(string clientId, string secret, CancellationToken cancel)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenAddress)
            {
                Content = new FormUrlEncodedContent(
                [
                    new("client_id", clientId),
                    new("client_secret", secret),
                    new("grant_type", "client_credentials"),
                ]),
            };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Log("IGDB: Zugang von Twitch abgelehnt");
                return (IgdbOutcome.Rejected, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                Log($"IGDB: Twitch antwortet {(int)response.StatusCode}");
                return (IgdbOutcome.NoConnection, null);
            }

            return ReadToken(await ReadAsync(response, linked.Token)) is { } token
                ? (IgdbOutcome.Found, token)
                : (IgdbOutcome.NoConnection, null);
        }
        catch (Exception e) when (IsNoConnection(e, cancel))
        {
            Log($"IGDB: Twitch nicht erreichbar ({e.GetType().Name})");
            return (IgdbOutcome.NoConnection, null);
        }
    }

    /// <summary>
    /// The games of a query to <paramref name="endpoint"/> (<c>games</c> or <c>external_games</c>, fixed in code);
    /// <see cref="IgdbOutcome.Rejected"/> when IGDB turns the token down (HTTP 401 or 403).
    /// </summary>
    public async Task<IgdbLookup> QueryAsync(string clientId, IgdbToken token, IgdbQuery query, CancellationToken cancel)
    {
        for (var throttled = 0; ; throttled++)
        {
            await PaceAsync(cancel);
            using var timeout = new CancellationTokenSource(Timeout, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Api, query.Endpoint))
                {
                    Content = new StringContent(query.Body, Encoding.UTF8, "text/plain"),
                };
                request.Headers.Add("Client-ID", clientId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                        return IgdbLookup.Of(IgdbOutcome.Rejected);
                    case HttpStatusCode.TooManyRequests when throttled < MaxThrottled:
                        await Task.Delay(WaitOf(response), time, cancel);
                        continue;
                    case var status when !response.IsSuccessStatusCode:
                        Log($"IGDB: antwortet {(int)status}");
                        return IgdbLookup.Of(IgdbOutcome.NoConnection);
                }

                return IgdbAnswer.Read(await ReadAsync(response, linked.Token), query.Nested) is { } games
                    ? new IgdbLookup(IgdbOutcome.Found, games)
                    : IgdbLookup.Of(IgdbOutcome.NoConnection);
            }
            catch (Exception e) when (IsNoConnection(e, cancel))
            {
                Log($"IGDB: keine Antwort ({e.GetType().Name})");
                return IgdbLookup.Of(IgdbOutcome.NoConnection);
            }
        }
    }

    /// <summary>For log lines and the rows: never a value of the access, only its last four characters.</summary>
    public static string Ending(string value) => value.Length >= 4 ? value[^4..] : string.Empty;

    private static bool IsNoConnection(Exception e, CancellationToken cancel) =>
        e is HttpRequestException or IOException or InvalidDataException || (e is OperationCanceledException && !cancel.IsCancellationRequested);

    /// <summary>Requests to IGDB keep <see cref="Limit"/>'s gap, also across runs and the test of the access.</summary>
    private async Task PaceAsync(CancellationToken cancel)
    {
        await _pace.WaitAsync(cancel);
        try
        {
            var now = time.GetUtcNow();
            if (_next > now)
            {
                await Task.Delay(_next - now, time, cancel);
                now = time.GetUtcNow();
            }

            _next = now + Limit.Gap;
        }
        finally
        {
            _pace.Release();
        }
    }

    private TimeSpan WaitOf(HttpResponseMessage response)
    {
        var asked = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - time.GetUtcNow(),
            _ => DefaultWait,
        };
        return asked < TimeSpan.Zero ? TimeSpan.Zero : asked > LongestWait ? LongestWait : asked;
    }

    /// <summary>The body, at most <see cref="MaxAnswer"/>; a longer one is no answer Arcadia reads.</summary>
    private static async Task<byte[]> ReadAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength > MaxAnswer)
        {
            throw new InvalidDataException("answer too large");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancel);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(chunk, cancel)) > 0)
        {
            if (buffer.Length + read > MaxAnswer)
            {
                throw new InvalidDataException("answer too large");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Twitch's <c>{"access_token":"…","expires_in":…,"token_type":"bearer"}</c>; null if it isn't one.</summary>
    internal static IgdbToken? ReadToken(byte[] body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String
                && IgdbAccess.Normalize(token.GetString()) is { } value
                ? new IgdbToken(value)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
