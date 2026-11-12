using System.Net;
using System.Text;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>
/// A made-up launcher with local data and a store page (#493): the local data per game id as the test sets it, the store
/// page at <c>https://store.example/app/&lt;id&gt;</c>, whose answer is the description as UTF-8 text ("-" for "no page").
/// The HTTP side is <see cref="FakeStoreServer"/>.
/// </summary>
internal sealed class FakeMetadataSource : ILauncherMetadata
{
    public Dictionary<string, GameMetadata> Local { get; } = new(StringComparer.Ordinal);

    public int Looks { get; private set; }

    public IReadOnlySet<MetadataField> Fields { get; } = new HashSet<MetadataField>(Enum.GetValues<MetadataField>());

    public string Summary => "Made-up summary";

    public string About => "Made-up about";

    public IMetadataStore? Store { get; init; } = new FakeStorePage();

    public IMetadataLookup Look(IReadOnlyCollection<Game> games)
    {
        Looks++;
        return new Lookup(new Dictionary<string, GameMetadata>(Local));
    }

    private sealed class Lookup(Dictionary<string, GameMetadata> local) : IMetadataLookup
    {
        public GameMetadata? Of(Game game) => local.GetValueOrDefault(game.Id);
    }
}

internal sealed class FakeStorePage : IMetadataStore
{
    public RequestLimit Limit { get; init; } = new(60, TimeSpan.FromMinutes(1));

    public Uri? AddressOf(Game game) => game.Id.All(char.IsAsciiDigit) ? new Uri("https://store.example/app/" + game.Id) : null;

    public GameMetadata? Parse(Game game, ReadOnlySpan<byte> answer)
    {
        var text = Encoding.UTF8.GetString(answer);
        return text switch
        {
            "-" => GameMetadata.Empty,
            "broken" => null,
            _ => new GameMetadata { Description = text, AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "17"), new AgeRating(AgeRatingBoard.Usk, "12")] },
        };
    }
}

/// <summary>The store's HTTP side: an answer per game id, a status, a hang, no network; records each request with its time.</summary>
internal sealed class FakeStoreServer(TimeProvider time) : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<CancellationToken, Task<HttpResponseMessage>>>> _once = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = new(StringComparer.Ordinal);

    public List<(HttpRequestMessage Request, DateTimeOffset At)> Requests { get; } = [];

    public bool Offline { get; set; }

    public TaskCompletionSource Hanging { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int RequestCount
    {
        get
        {
            lock (Requests)
            {
                return Requests.Count;
            }
        }
    }

    public IReadOnlyList<string> AskedIds
    {
        get
        {
            lock (Requests)
            {
                return [.. Requests.Select(r => IdOf(r.Request.RequestUri!))];
            }
        }
    }

    /// <summary>The game's id in an address: Steam's <c>appids=</c>, else the last segment of the path.</summary>
    public static string IdOf(Uri address) =>
        System.Text.RegularExpressions.Regex.Match(address.Query, @"[?&]appids=([^&]*)") is { Success: true } appIds ? appIds.Groups[1].Value : address.Segments[^1];

    public void Serve(string id, string answer) => _answers[id] = _ => Task.FromResult(Ok(answer));

    public void Fail(string id, HttpStatusCode status) => _answers[id] = _ => Task.FromResult(new HttpResponseMessage(status));

    /// <summary>The next request for <paramref name="id"/> gets HTTP 429, with <paramref name="retryAfter"/> if given; later ones the usual answer.</summary>
    public void ThrottleOnce(string id, TimeSpan? retryAfter = null) => Once(id, _ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (retryAfter is { } wait)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
        }

        return Task.FromResult(response);
    });

    public void Hang(string id) => _answers[id] = async cancel =>
    {
        Hanging.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancel);
        throw new InvalidOperationException("unreachable");
    };

    private void Once(string id, Func<CancellationToken, Task<HttpResponseMessage>> answer)
    {
        if (!_once.TryGetValue(id, out var queue))
        {
            _once[id] = queue = new();
        }

        queue.Enqueue(answer);
    }

    private static HttpResponseMessage Ok(string answer) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(answer)) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add((request, time.GetUtcNow()));
        }

        if (Offline)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "No connection could be made.");
        }

        var id = IdOf(request.RequestUri!);
        if (_once.TryGetValue(id, out var queue) && queue.Count > 0)
        {
            return queue.Dequeue()(cancellationToken);
        }

        return _answers.TryGetValue(id, out var answer) ? answer(cancellationToken) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
